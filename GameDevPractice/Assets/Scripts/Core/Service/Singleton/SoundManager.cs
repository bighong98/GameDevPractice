using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TH.Resource;
using UnityEngine;
using UnityEngine.Scripting;

namespace TH.Core.Service
{
    [Preserve]
    public class SoundManager : Singleton<SoundManager>, ISingleton
    {
        private readonly AudioSource[] audioSources = new AudioSource[(int)Enums.AudioType.Max];
        private readonly Dictionary<string, AudioClip> audioClips = new ();
        
        private const string SoundRootName = "Sounds";
        private const string SoundSuffix = ".wav";
        private const string VolumeSuffix = "Volume";
        private const float DefaultVolume = 0.2f;
        private const float DefaultPitch = 1.0f;
        
        private SoundManager()
        {
            var soundRoot = CreateRoot();
            SetAudioSource(soundRoot);
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
                // PlayerPrefs.GetFloat($"{soundTypeNames[i]}{VolumeSuffix}", DefaultVolume); // PlayerPrefs로부터 볼륨 사용자 설정 불러오기. 저장된 설정이 없으면 DefaultVolume 적용
                audioSources[i].volume = DefaultVolume;
                go.transform.SetParent(soundRoot); 
            }

            audioSources[(int)Enums.AudioType.Bgm].loop = true; // Bgm, SubBgm의 기본 설정: 반복 재생
            audioSources[(int)Enums.AudioType.SubBgm].loop = true;
        }

        #endregion

        public void ChangeSoundVolume(Enums.AudioType type, float value)
        {
            audioSources[(int)type].volume = Mathf.Clamp(value, 0f, 1f);
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
