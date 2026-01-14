using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TH.Resource;
using TH.Utils;
using UnityEngine;
using UnityEngine.Scripting;

namespace TH.Core.Service
{
    [Preserve]
    public class SoundManager : Singleton<SoundManager>, ISingleton
    {
        private readonly AudioSource[] audioSources = new AudioSource[(int)Enums.AudioType.Max];
        private readonly Dictionary<string, AudioClip> audioClips = new ();
        private readonly float[] baseVolumes = new float[(int)Enums.AudioType.Max];
        private readonly Dictionary<Enums.VolumeGroup, float> groupVolumes = new ();
        private readonly Dictionary<Enums.VolumeGroup, Enums.AudioType[]> groupToAudioTypes = new ();
        private readonly Dictionary<Enums.AudioType, Enums.VolumeGroup> audioTypeToGroup = new ();
        private readonly UniTaskCompletionSource<SoundVolumeGroupMapSO> volumeGroupMapResolveTCS = new ();
        private readonly UniTask<SoundVolumeGroupMapSO> volumeGroupMapResolved;
        
        private const string SoundRootName = "Sounds";
        private const string SoundSuffix = ".wav";
        private const string VolumeSuffix = "Volume";
        private const float DefaultVolume = 0.2f;
        private const float DefaultPitch = 1.0f;
        private const string VolumeGroupMapKey = "SoundVolumeGroupMapSO";
        
        private SoundVolumeGroupMapSO volumeGroupMap;
        
        private SoundManager()
        {
            var soundRoot = CreateRoot();
            SetAudioSource(soundRoot);
            InitializeGroupVolumes();
            BuildDefaultMappings();
            volumeGroupMapResolved = volumeGroupMapResolveTCS.Task.Preserve();
            LoadVolumeGroupMapAsync().Forget();
            ApplyAllVolumes();
        }

        #region Initialization

        private static Transform CreateRoot()
        {
            GameObject root = new GameObject(SoundRootName);
            UnityEngine.Object.DontDestroyOnLoad(root);
            var soundRoot = root.transform;
            return soundRoot;
        }
        
        private void SetAudioSource(Transform soundRoot)
        {
            string[] soundTypeNames = System.Enum.GetNames(typeof(Enums.AudioType));
            for (int i = 0; i < soundTypeNames.Length - 1; i++) // 마지막 타입은 Max이기 때문에 Length -1
            {
                GameObject go = new GameObject { name = soundTypeNames[i] };
                audioSources[i] = go.AddComponent<AudioSource>(); // 오디오 재생용 컴포넌트 부착
                audioSources[i].spatialBlend = 0; // todo: 거리 비례 사운드 전달 필요시 수정 필요
                baseVolumes[i] = PlayerPrefs.GetFloat($"{soundTypeNames[i]}{VolumeSuffix}", DefaultVolume);
                audioSources[i].volume = baseVolumes[i];
                go.transform.SetParent(soundRoot); 
            }

            audioSources[(int)Enums.AudioType.Bgm].loop = true; // Bgm, SubBgm의 기본 설정: 반복 재생
            audioSources[(int)Enums.AudioType.SubBgm].loop = true;
        }

        private void InitializeGroupVolumes()
        {
            foreach (Enums.VolumeGroup group in Enum.GetValues(typeof(Enums.VolumeGroup)))
            {
                groupVolumes[group] = PlayerPrefs.GetFloat($"{group}{VolumeSuffix}", 1f);
            }
        }

        private async UniTask LoadVolumeGroupMapAsync()
        {
            try
            {
                var resourceLoader = ServiceLocator.Get<IResourceLoader>();
                if (resourceLoader == null || string.IsNullOrEmpty(VolumeGroupMapKey))
                {
                    volumeGroupMapResolveTCS.TrySetResult(null);
                    Logg.Log("[SoundManager] LoadVolumeGroupMapAsync completed - using default mapping (no loader/key)", Logg.LoggingMode.Completed);
                    return;
                }

                volumeGroupMap = await resourceLoader.LoadAsync<SoundVolumeGroupMapSO>(VolumeGroupMapKey);
                volumeGroupMapResolveTCS.TrySetResult(volumeGroupMap);

                BuildMappings(volumeGroupMap);
                ApplyAllVolumes();
                Logg.Log($"[SoundManager] LoadVolumeGroupMapAsync completed - map: {(volumeGroupMap != null ? volumeGroupMap.name : "NULL")}", Logg.LoggingMode.Completed, context: volumeGroupMap);
            }
            catch (Exception e)
            {
                volumeGroupMapResolveTCS.TrySetException(e);
                BuildDefaultMappings();
                ApplyAllVolumes();
                Logg.LogWarning($"[SoundManager] LoadVolumeGroupMapAsync completed - fallback to default mapping ({e.GetType().Name})");
#if UNITY_EDITOR
                throw;
#endif
            }
        }

        public async UniTask WaitForVolumeGroupMap(CancellationToken token = default)
        {
            if (volumeGroupMap != null) return;
            await volumeGroupMapResolved.AttachExternalCancellation(token);
        }

        private void BuildMappings(SoundVolumeGroupMapSO map)
        {
            groupToAudioTypes.Clear();
            audioTypeToGroup.Clear();

            if (map == null || map.Entries == null || map.Entries.Count == 0)
            {
                BuildDefaultMappings();
                return;
            }

            foreach (var entry in map.Entries)
            {
                if (entry == null || entry.audioTypes == null || entry.audioTypes.Count == 0) continue;

                var types = new Enums.AudioType[entry.audioTypes.Count];
                for (int i = 0; i < entry.audioTypes.Count; i++)
                {
                    types[i] = entry.audioTypes[i];
                }

                groupToAudioTypes[entry.group] = types;
                foreach (var audioType in types)
                {
                    audioTypeToGroup[audioType] = entry.group;
                }
            }
        }

        private void BuildDefaultMappings()
        {
            groupToAudioTypes.Clear();
            audioTypeToGroup.Clear();

            var bgmTypes = new[] { Enums.AudioType.Bgm, Enums.AudioType.SubBgm };
            var effectTypes = new[] { Enums.AudioType.Effect };

            groupToAudioTypes[Enums.VolumeGroup.Bgm] = bgmTypes;
            groupToAudioTypes[Enums.VolumeGroup.Effect] = effectTypes;

            foreach (var t in bgmTypes)
            {
                audioTypeToGroup[t] = Enums.VolumeGroup.Bgm;
            }

            foreach (var t in effectTypes)
            {
                audioTypeToGroup[t] = Enums.VolumeGroup.Effect;
            }
        }

        private void ApplyAllVolumes()
        {
            for (int i = 0; i < (int)Enums.AudioType.Max; i++)
            {
                ApplyFinalVolume((Enums.AudioType)i);
            }
        }

        private void ApplyGroupVolume(Enums.VolumeGroup group)
        {
            if (group == Enums.VolumeGroup.Master)
            {
                ApplyAllVolumes();
                return;
            }

            if (groupToAudioTypes.TryGetValue(group, out var types))
            {
                foreach (var type in types)
                {
                    ApplyFinalVolume(type);
                }

                return;
            }

            foreach (var pair in audioTypeToGroup)
            {
                if (pair.Value == group)
                {
                    ApplyFinalVolume(pair.Key);
                }
            }
        }

        private Enums.VolumeGroup ResolveGroup(Enums.AudioType type)
        {
            return audioTypeToGroup.TryGetValue(type, out var group) ? group : Enums.VolumeGroup.Master;
        }

        private void ApplyFinalVolume(Enums.AudioType type)
        {
            float master = groupVolumes.TryGetValue(Enums.VolumeGroup.Master, out var masterVolume) ? masterVolume : 1f;
            float group = groupVolumes.TryGetValue(ResolveGroup(type), out var groupVolume) ? groupVolume : 1f;
            audioSources[(int)type].volume = Mathf.Clamp01(baseVolumes[(int)type] * master * group);
        }

        #endregion

        public void ChangeSoundVolume(Enums.AudioType type, float value)
        {
            baseVolumes[(int)type] = Mathf.Clamp01(value);
            ApplyFinalVolume(type);
        }

        public void SetGroupVolume(Enums.VolumeGroup group, float value)
        {
            groupVolumes[group] = Mathf.Clamp01(value);
            ApplyGroupVolume(group);
        }
        
        #region Play

        public void Play(Enums.AudioType type) // 이미 등록된 오디오클립 재생 (변경x). Effect 타입은 사용하지 말것
        {
            audioSources[(int)type]?.Play();
        }

        // key를 통해 오디오클립을 찾아서 재생
        public void Play(Enums.AudioType type, string key, float pitch = DefaultPitch)
        { 
            LoadAudioClip(type, key, pitch, Play);
        }
        // 오디오클립을 직접 전달하여 재생
        // pitch는 Effect 타입에만 사용
        public void Play(Enums.AudioType type, AudioClip audioClip, float pitch = DefaultPitch)
        { 
            AudioSource audioSource = audioSources[(int)type];
            switch (type)
            {
                case Enums.AudioType.Bgm:
                    if (audioSource.isPlaying)
                        audioSource.Stop(); // 재생중인 bgm이 있었다면 정지
                    audioSource.clip = audioClip; 
                    audioSource.Play(); // 새로운 bgm 재생
                    break;
                case Enums.AudioType.SubBgm:
                    if (audioSource.isPlaying)
                        audioSource.Stop(); // 재생중인 subBgm이 있었다면 정지
                    audioSource.clip = audioClip; 
                    audioSource.Play(); // 새로운 subBgm 재생
                    break;
                case Enums.AudioType.Effect:
                    audioSource.pitch = pitch;
                    audioSource.PlayOneShot(audioClip); // Effect용 소리효과 한번만 재생
                    break;
                default:
                    break;
            }
        }

        #endregion

        #region Stop

        public void Stop(Enums.AudioType type)
        {
            audioSources[(int)type]?.Stop();
        }

        #endregion

        #region Load

        private void LoadAudioClip(Enums.AudioType type, string key, float pitch, Action<Enums.AudioType, AudioClip, float> callback)
        {
            if (! audioClips.TryGetValue(key, out var audioClip))
            {
                audioClip = ResourceManager.Instance.Load<AudioClip>(key); // 캐싱된 오디오클립이 없으면 리소스 매니저에서 key로 탐색
                audioClips[key] = audioClip; // 새로운 오디오클립 저장
            }
            callback?.Invoke(type, audioClip, pitch);
        }

        #endregion

        #region Clear

        private UniTask Clear(CancellationToken externalToken)
        {
            externalToken.ThrowIfCancellationRequested();
            
            foreach (var source in audioSources)
            {
                source.Stop();
            }
            
            return UniTask.CompletedTask;
        }

        #endregion

        #region ISingleton

        public UniTask BeforeSceneLoad(CancellationToken externalToken)
        {
            externalToken.ThrowIfCancellationRequested();
            
            Clear(externalToken);
            return UniTask.CompletedTask;
        }

        public UniTask AfterSceneLoad(CancellationToken externalToken)
        {
            externalToken.ThrowIfCancellationRequested();
            //todo: 씬 전환 시마다 수행할 작업 추가
            return UniTask.CompletedTask;
        }

        #endregion
        
    }
}
