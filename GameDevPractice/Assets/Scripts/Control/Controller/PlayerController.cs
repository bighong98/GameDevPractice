using UnityEngine;
using UnityEngine.AI;
using TH.Utils;
using TH.Control.Movement;
using TH.Core;
using TH.Core.Service;
using TH.Combat;
using TH.Attribute;


namespace TH.Control
{
    public interface IPlayerController : IRaycastHolder {}
    
    public class PlayerController : MonoBehaviour, IPlayerController, ISightHandler
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private bool _enableInteractionOutline = true;

        private GameObject _outlinedTarget;
        private Renderer _outlinedRenderer;
        private int _outlinedOriginalLayer = -1;
        
        private IMover mover;
        private IFighter fighter;
        private Health health;
        private ISkillController skillController;

        public ComponentProvider Components { get; private set; }

        private Vector2 wasdInput = Vector2.zero;
        private bool isWASDMoving = false;
        private bool fightEnabled = true;
        private const float WasdInputThresholdSqr = 0.01f;
        private const float MaxNavMeshProjectionDistance = 1f;

        public float SightThreshold { get; } = 30f * 30f;
        
        private void Awake()
        {
            Components = new ComponentProvider(gameObject);
            
            TryGetComponent(out mover);
            TryGetComponent(out fighter);
            TryGetComponent(out health);
            TryGetComponent(out skillController);

            ServiceLocator.Get<IPlayerHolder>().SetPlayer(this);
        }

        private void Start()
        {
            _camera = _camera != null ? _camera : Camera.main;
        }

        private void OnEnable()
        {
            InputManager.Instance.OnSelected += OnPointerPressed;
            InputManager.Instance.OnMoved += OnWASDInput;

            if (fighter.IsNotNull())
                fighter.OnTargetSet += OnFighterTargetSet;
        }

        private void OnDisable()
        {
            InputManager.Instance.OnSelected -= OnPointerPressed;
            InputManager.Instance.OnMoved -= OnWASDInput;

            if (fighter.IsNotNull())
                fighter.OnTargetSet -= OnFighterTargetSet;
        }

        private void Update()
        {
            if (!CanProcessInput())
            {
                return;
            }

            // WASD 입력이 있으면 지속적으로 이동 처리
            if (isWASDMoving && wasdInput.sqrMagnitude > WasdInputThresholdSqr)
            {
                HandleWASDMovement();
            }
        }

        private bool CanProcessInput()
        {
            return Time.timeScale > float.Epsilon && health?.IsDead != true;
        }

        private void OnWASDInput(Vector2 input)
        {
            wasdInput = input;
            isWASDMoving = input.sqrMagnitude > WasdInputThresholdSqr;
            
            // WASD 입력이 시작되면 즉시 기존 이동/전투 취소
            if (isWASDMoving)
            {
                mover.Stop();
            }
        }

        private void HandleWASDMovement()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera == null) return;
            }
            // 카메라 방향 기준으로 입력 변환
            Vector3 cameraForward = _camera.transform.forward;
            Vector3 cameraRight = _camera.transform.right;
            
            // Y축 제거 (수평 이동만)
            cameraForward.y = 0;
            cameraRight.y = 0;
            cameraForward.Normalize();
            cameraRight.Normalize();

            // 이동 방향 계산
            Vector3 moveDirection = (cameraForward * wasdInput.y + cameraRight * wasdInput.x).normalized;
            
            if (moveDirection.sqrMagnitude > WasdInputThresholdSqr)
            {
                // 현재 위치에서 이동 방향으로 목표 지점 설정 (더 짧은 거리)
                Vector3 targetPosition = transform.position + moveDirection * 2f; // 10f -> 2f로 변경
                
                // NavMesh 위의 유효한 위치로 변환
                if (NavMesh.SamplePosition(targetPosition, out NavMeshHit navMeshHit, MaxNavMeshProjectionDistance, NavMesh.AllAreas))
                {
                    mover.SetDestination(navMeshHit.position);
                }
            }
        }


        private void OnPointerPressed(Vector2 pos)
        {
            Logg.Log($"[{nameof(PlayerController)}.{nameof(OnPointerPressed)}()] triggered", Logg.LoggingMode.Completed);
            
            if (!CanProcessInput())
            {
                return; // 게임이 일시정지 중이거나 플레이어가 사망한 경우 반응 없음
            }
            
            // WASD 이동 중이면 포인터 입력 무시 (WASD 우선)
            if (isWASDMoving)
            {
                return;
            }
            
            if (fightEnabled && TryInteractWithComponent(pos))
            {
                return; // 우선순위: 전투 > 이동
            }
            
            SetCursor(CursorType.None);
        }

        private const int MaxRaycastHitNum = 100;
        private readonly RaycastHit[] hitResults = new RaycastHit[MaxRaycastHitNum];

        private bool TryInteractWithComponent(Vector2 pointerPos)
        {
            if (!TryGetPointerRay(pointerPos, out var ray) ||
                Physics.RaycastNonAlloc(ray, hitResults) is not (int hitLength and > 0))
            {
                SetInteractionOutline(null);
                return false;
            }
            
            for (int i = 0; i < hitLength; i++) // todo: 거리순서로 정렬된 배열을 사용하는 것을 고려
            {
                if (hitResults[i].transform.GetComponents<IRaycastable>() is not { } raycastables) continue;
                
                foreach (var raycastable in raycastables)
                {
                    if (!raycastable.HandleRaycast(this)) continue;
                    
                    SetInteractionOutline(((Component)raycastable).gameObject);
                    SetCursor(raycastable.GetCursorType());
                    return true;
                }
            }
            
            SetInteractionOutline(null);
            return false;
        }
        
        
        private void SetInteractionOutline(GameObject target)
        {
            if (!_enableInteractionOutline)
            {
                ClearInteractionOutline();
                return;
            }

            if (_outlinedTarget == target) return;

            ClearInteractionOutline();
            _outlinedTarget = target;

            if (_outlinedTarget == null) return;

            var renderers = _outlinedTarget.GetComponentsInChildren<Renderer>(false);
            if (renderers == null || renderers.Length == 0) return;

            SkinnedMeshRenderer skinnedRenderer = null;
            Renderer meshRenderer = null;
            foreach (var renderer in renderers)
            {
                if (renderer is SkinnedMeshRenderer skinned)
                {
                    skinnedRenderer ??= skinned;
                    continue;
                }

                if (renderer is MeshRenderer)
                {
                    meshRenderer ??= renderer;
                }
            }

            var targetRenderer = (Renderer)skinnedRenderer ?? meshRenderer;
            if (targetRenderer == null) return;

            int outlineLayer = LayerMask.NameToLayer("Outline");
            if (outlineLayer < 0) return;

            _outlinedRenderer = targetRenderer;
            _outlinedOriginalLayer = targetRenderer.gameObject.layer;
            targetRenderer.gameObject.layer = outlineLayer;
        }

        private void ClearInteractionOutline()
        {
            if (_outlinedRenderer != null)
            {
                _outlinedRenderer.gameObject.layer = _outlinedOriginalLayer;
            }

            _outlinedRenderer = null;
            _outlinedOriginalLayer = -1;
            _outlinedTarget = null;
        }

        private bool TryGetPointerRay(Vector2 pos, out Ray ray)
        {
            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera == null)
                {
                    ray = default;
                    return false;
                }
            }

            ray = _camera.ScreenPointToRay(pos);
            return true;
        }

        private void SetCursor(CursorType cursor)
        {
            // TODO: Cursor 시스템 미개발 상태
        }

        private Health fighterTargetBuffer;
        private void OnFighterTargetSet(Health targetHealth)
        {
            if (fighterTargetBuffer != null && ReferenceEquals(fighterTargetBuffer, targetHealth))
            {
                skillController.TryRequestActiveSkill();
            }

            fighterTargetBuffer = targetHealth;
        }
    }
}
