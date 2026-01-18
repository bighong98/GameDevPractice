using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TH.Cinematic.Service;
using TH.Core.Service;
using TH.SceneManagement;
using TH.Utils;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TH.UI.Service
{
    public class HUDCullingSystem: IHUDCullingSystem, IDisposable
    {
        private const int DefaultCapacity = 64;
        private const float DefaultMaxDistance = 25f;
        private readonly ISceneLoader _sceneLoader;
        private readonly ICameraHolder _cameraHolder;
        private readonly IPlayerHolder _playerHolder;
        private bool _disposed;
        
        private CullingGroup cullingGroup;

        private Camera _camera;
        private Transform playerTrs; 

        private BoundingSphere[] _spheres;
        private int _usedCount;

        // 인덱스 -> HUD 엔트리
        private readonly List<CullingEntry> _entries = new();
        private readonly Stack<CullingEntry> _entryPool = new();
        private readonly Stack<int> _freeIndices = new();
        // “현재 표시 대상”만 빠르게 순회하기 위한 집합
        private readonly HashSet<int> _activeVisible = new();
        private readonly float[] _distanceBands = { DefaultMaxDistance };

        private bool IsReady => _camera != null && playerTrs != null;
        private bool culling;
        private CancellationTokenSource cullingCTS;
        private CancellationToken cullingToken;

        public HUDCullingSystem(ISceneLoader sceneLoader, ICameraHolder cameraHolder, IPlayerHolder playerHolder)
        {
            _sceneLoader = sceneLoader;
            _cameraHolder = cameraHolder;
            _playerHolder = playerHolder;

            _spheres = new BoundingSphere[DefaultCapacity];

            _sceneLoader.OnAfterSceneChanged += this.OnAfterSceneLoaded;
            _sceneLoader.OnBeforeSceneChanged += this.OnBeforeSceneLoaded;
            Application.quitting += OnApplicationQuitting;
#if UNITY_EDITOR
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
#endif

            // 인스턴스 생성 시점에 카메라와 플레이어 객체가 씬에 생성되지 않았을 수 있음

            if (_cameraHolder.TryGetMainCamera(out var cam)) UpdateCamera(cam);
            else _cameraHolder.OnCameraInstanceUpdated += OnCameraInstanceUpdated;

            _playerHolder.OnPlayerInstanceUpdated += OnPlayerInstanceUpdated;
        }

        private void OnApplicationQuitting()
        {
            Dispose();
        }

#if UNITY_EDITOR
        private void OnBeforeAssemblyReload()
        {
            Dispose();
        }
#endif

        private void OnCameraInstanceUpdated(Camera camera)
        {
            UpdateCamera(camera);
        }

        private void OnPlayerInstanceUpdated(object playerInstance)
        {
            UpdatePlayerInstance(playerInstance);
        }

        private async UniTask OnBeforeSceneLoaded(CancellationToken externalToken)
        {
            externalToken.ThrowIfCancellationRequested();
            try
            {
                await StopCullingAsync(externalToken);
                ClearEntries();
            }
            catch (Exception e) { Debug.LogError($"HUDCullingSystem.OnBeforeSceneLoaded - {e}"); }
        }

        private async UniTask OnAfterSceneLoaded(CancellationToken externalToken)
        {
            externalToken.ThrowIfCancellationRequested();
            try
            {
                if (_cameraHolder.TryGetMainCamera(out var cam))
                    UpdateCamera(cam);

                var playerInstance = _playerHolder.GetPlayerInstance;
                if (playerInstance != null)
                    UpdatePlayerInstance(playerInstance);

                TryStartCulling();
                await UniTask.CompletedTask;
            }
            catch (Exception e) { Debug.LogError($"HUDCullingSystem.OnAfterSceneLoaded - {e}"); }
        }

        private void UpdateCamera(Camera cam)
        {
            if (cam == null)
            {
                this.LogWarning("UpdateCamera() - invalid Camera instance");
                cam = Camera.main;
            }

            if (cam == null)
            {
                _camera = null;
                return;
            }

            _camera = cam;
            _distanceBands[0] = Mathf.Min(DefaultMaxDistance, _camera.farClipPlane);

            if (cullingGroup != null)
            {
                cullingGroup.targetCamera = _camera;
                cullingGroup.SetBoundingDistances(_distanceBands);
            }

            TryStartCulling();
        }

        private void UpdatePlayerInstance(object playerInstance)
        {
            if (playerInstance is not Component p)
            {
                this.LogWarning("OnPlayerInstanceUpdated - failed to update player instance");
                return;
            }

            playerTrs = p.transform;
            if (playerTrs == null)
            {
                this.LogWarning("OnPlayerInstanceUpdated - invalid player transform");
                return;
            }

            if (cullingGroup != null)
                cullingGroup.SetDistanceReferencePoint(playerTrs);

            TryStartCulling();
        }

        private void TryStartCulling()
        {
            if (culling || !IsReady) return;
            EnsureCullingToken();
            StartCullingAsync(cullingToken).Forget();
        }

        private void EnsureCullingToken()
        {
            if (cullingCTS != null && !cullingCTS.IsCancellationRequested) return;

            cullingCTS?.Dispose();
            cullingCTS = new CancellationTokenSource();
            cullingToken = cullingCTS.Token;
        }

        private void RefreshCullingGroupData()
        {
            if (cullingGroup == null) return;

            if (_entries.Count > 0)
                EnsureCapacity(_entries.Count - 1);

            int maxIndex = -1;
            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (e == null || !e.IsRegistered || e.Target == null)
                {
                    _spheres[i] = new BoundingSphere(new Vector3(0, -999999f, 0), 0f);
                    continue;
                }

                _spheres[i] = new BoundingSphere(e.Target.position, e.Radius);
                e.View?.SetVisible(false);
                if (i > maxIndex) maxIndex = i;
            }

            _activeVisible.Clear();
            int newCount = Math.Max(_usedCount, maxIndex + 1);
            if (newCount != _usedCount)
                _usedCount = newCount;
            cullingGroup.SetBoundingSpheres(_spheres);
            cullingGroup.SetBoundingSphereCount(_usedCount);
        }

        private void HideAllRegistered()
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                entry?.View?.SetVisible(false);
            }
        }

        private void ClearEntries()
        {
            HideAllRegistered();
            for (int i = 0; i < _entries.Count; i++)
                ReleaseEntry(_entries[i]);

            _entries.Clear();
            _freeIndices.Clear();
            _activeVisible.Clear();
            _usedCount = 0;

            if (_spheres != null)
                Array.Clear(_spheres, 0, _spheres.Length);

            // boundingSphereCount는 유지: 최적화 단계에서만 축소 처리
        }

        public CullingHandle Register(ICullingTargetView view, float sphereRadius = 0.5f)
        {
            if (view == null || view.Target == null)
                return default;

            int index = AllocateIndex();

            EnsureCapacity(index);

            var entry = GetEntry(view, view.Target, sphereRadius);

            if (index < _entries.Count) _entries[index] = entry;
            else
            {
                while (_entries.Count < index) _entries.Add(null);
                _entries.Add(entry);
            }

            // 초기 sphere 세팅
            _spheres[index] = new BoundingSphere(entry.Target.position, entry.Radius);

            // usedCount는 직접 관리
            if (index >= _usedCount)
            {
                _usedCount = index + 1;
                if (cullingGroup != null)
                    cullingGroup.SetBoundingSphereCount(_usedCount);
            }

            // 초기에는 숨김 처리(콜백 오기 전 안전)
            entry.View.SetVisible(false);

            TryStartCulling();

            return new CullingHandle(this, index);
        }

        public void Unregister(in CullingHandle handle)
        {
            if (!handle.IsValid) return;
            Unregister(handle.Index);
        }

        private void Unregister(int index)
        {
            if (index < 0 || index >= _entries.Count) return;

            var entry = _entries[index];
            if (entry == null || !entry.IsRegistered) return;

            // HUD 숨김
            entry.View?.SetVisible(false);

            // 상태 정리
            _activeVisible.Remove(index);

            _entries[index] = null;

            // sphere 무효화(반경 0, 멀리 치우기 등)
            _spheres[index] = new BoundingSphere(new Vector3(0, -999999f, 0), 0f);

            // 인덱스 재사용
            _freeIndices.Push(index);

            ReleaseEntry(entry);

            // boundingSphereCount를 줄이는 최적화는 “마지막 인덱스부터 비어있을 때만” 가능.
            // 스케치에서는 단순화를 위해 count는 유지합니다.
        }

        /// <summary>
        /// LateUpdate 등에서 호출: sphere 중심 갱신 + 보이는 것만 스크린 좌표 업데이트
        /// </summary>
        public void Tick()
        {
            if (!IsReady || _camera == null || _spheres == null) return;

            // 1) 등록된 모든 엔트리의 sphere 중심 갱신
            // 배열은 참조이므로 값만 갱신하면 됨 :contentReference[oaicite:5]{index=5}
            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (e == null || !e.IsRegistered || e.Target == null) continue;
                _spheres[i].position = e.Target.position;
                // radius는 고정이면 생략 가능
            }

            // 2) “표시 후보(activeVisible)”만 스크린 좌표 갱신
            foreach (var idx in _activeVisible)
            {
                var e = _entries[idx];
                if (e == null || !e.IsRegistered || e.Target == null || e.View == null) continue;

                // Overlay Canvas 기준: screen point 그대로 사용
                Vector3 sp = _camera.WorldToScreenPoint(e.Target.position);
                e.View.SetScreenPosition(sp);
            }
        }

        private async UniTask StartCullingAsync(CancellationToken token)
        {
            if (culling || !IsReady) return;

            culling = true;
            try
            {
                if (cullingGroup == null)
                    cullingGroup = InitializeCullingGroup(_camera);
                else
                {
                    cullingGroup.targetCamera = _camera;
                    cullingGroup.SetDistanceReferencePoint(playerTrs);
                    cullingGroup.SetBoundingDistances(_distanceBands);
                    cullingGroup.SetBoundingSpheres(_spheres);
                }

                if (cullingGroup == null)
                    return;

                RefreshCullingGroupData();

                while (!token.IsCancellationRequested)
                {
                    Tick();
                    await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, token).SuppressCancellationThrow();
                }
            }
            catch (Exception e) { Debug.LogError($"HUDCullingSystem.StartCullingAsync - {e}"); }
            finally
            {
                culling = false;
            }
        }

        private async UniTask StopCullingAsync(CancellationToken token)
        {
            if (cullingCTS == null)
            {
                await UniTask.SwitchToMainThread();
                ClearCullingGroup();
                return;
            }

            if (!cullingCTS.IsCancellationRequested)
                cullingCTS.Cancel();

            try
            {
                await UniTask.WaitWhile(() => culling, cancellationToken: token).SuppressCancellationThrow();
            }
            catch (Exception e)
            {
                Debug.LogError($"HUDCullingSystem.StopCullingAsync - {e}");
            }
            finally
            {
                cullingCTS.Dispose();
                cullingCTS = null;
                cullingToken = default;

                HideAllRegistered();
                _activeVisible.Clear();
                await UniTask.SwitchToMainThread();
                ClearCullingGroup();
            }
        }

        private void OnStateChanged(CullingGroupEvent evt)
        {
            int idx = evt.index;
            if (idx < 0 || idx >= _entries.Count) return;

            var e = _entries[idx];
            if (e == null || !e.IsRegistered || e.View == null) return;

            // 거리 밴드: 0이면 maxDistance 이내, 1이면 초과(우리 설계 기준)
            bool inRange = evt.currentDistance == 0;
            bool visible = evt.isVisible; // 프러스텀 가시성

            e.InRange = inRange;
            e.IsVisible = visible;

            bool finalVisible = inRange && visible;

            e.View.SetVisible(finalVisible);

            if (finalVisible) _activeVisible.Add(idx);
            else _activeVisible.Remove(idx);
        }

        private int AllocateIndex()
        {
            if (_freeIndices.Count > 0) return _freeIndices.Pop();
            return _entries.Count;
        }

        private CullingEntry GetEntry(ICullingTargetView view, Transform target, float radius)
        {
            var entry = _entryPool.Count > 0 ? _entryPool.Pop() : new CullingEntry();
            entry.Reset(view, target, radius);
            return entry;
        }

        private void ReleaseEntry(CullingEntry entry)
        {
            if (entry == null) return;
            entry.Reset(null, null, 0f);
            _entryPool.Push(entry);
        }

        private void EnsureCapacity(int index)
        {
            if (_spheres == null)
                _spheres = new BoundingSphere[DefaultCapacity];

            if (index < _spheres.Length) return;

            int newCap = Mathf.NextPowerOfTwo(index + 1);
            Array.Resize(ref _spheres, newCap);

            if (cullingGroup != null)
                cullingGroup.SetBoundingSpheres(_spheres);
        }

        private CullingGroup InitializeCullingGroup(Camera camera)
        {
            if (camera == null)
            {
                Logg.LogWarning("InitializeCullingGroup() - invalid camera instance");
                camera = Camera.main;
            }

            if (camera == null)
            {
                Logg.LogWarning("InitializeCullingGroup() - failed to resolve camera");
                return null;
            }

            _usedCount = 0;

            var newCullingGroup = new CullingGroup { targetCamera = camera };
            if (playerTrs != null)
                newCullingGroup.SetDistanceReferencePoint(playerTrs);
            newCullingGroup.SetBoundingDistances(_distanceBands);
            newCullingGroup.SetBoundingSpheres(_spheres);
            newCullingGroup.SetBoundingSphereCount(0);
            newCullingGroup.onStateChanged = OnStateChanged;

            return newCullingGroup;
        }

        private void ClearCullingGroup()
        {
            if (cullingGroup == null) return;

            cullingGroup.onStateChanged = null;
            cullingGroup.Dispose();
            cullingGroup = null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            Application.quitting -= OnApplicationQuitting;
#if UNITY_EDITOR
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
#endif
            DisposeAsync().Forget();

            if (Util.IsQuitting) return;
        }

        private async UniTask DisposeAsync()
        {
            try
            {
                await UniTask.SwitchToMainThread();
                await StopCullingAsync(CancellationToken.None);
            }
            catch (Exception e)
            {
                Debug.LogError($"HUDCullingSystem.DisposeAsync - {e}");
            }
        }

        private sealed class CullingEntry
        {
            public ICullingTargetView View;
            public Transform Target;
            public float Radius;
            public bool InRange;
            public bool IsVisible;
            public bool IsRegistered;

            public void Reset(ICullingTargetView view, Transform target, float radius)
            {
                View = view;
                Target = target;
                Radius = radius;
                InRange = false;
                IsVisible = false;
                IsRegistered = view != null && target != null;
            }
        }

        public readonly struct CullingHandle
        {
            private readonly HUDCullingSystem _owner;
            public readonly int Index;
            public bool IsValid => _owner != null && Index >= 0;

            public CullingHandle(HUDCullingSystem owner, int index)
            {
                _owner = owner;
                Index = index;
            }

            public void Release() => _owner?.Unregister(Index);
        }
    }

    public interface ICullingTargetView
    {
        // HUD가 추적할 월드 타겟
        Transform Target { get; }

        // 최종 표시 on/off (거리+가시성+추가 조건 반영된 결과)
        void SetVisible(bool visible);

        // Overlay Canvas 기준 스크린 좌표 적용 (RectTransform.position 등)
        void SetScreenPosition(Vector2 screenPos);
    }
}
