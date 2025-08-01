using UnityEngine;
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

public class SoundManager : Singleton<SoundManager>
{
    private readonly AudioSource[] audioSources = new AudioSource[(int)Enums.AudioType.Max];
    private readonly Dictionary<string, AudioClip> audioClips = new Dictionary<string, AudioClip>();

    [SerializeField] private Transform soundRoot;
    
    private const string SoundSuffix = ".wav";
    private const string VolumeSuffix = "Volume";
    
    protected override void Awake()
    {
        base.Awake();
        // if (IsInvalidInstance()) return; // 중복 인스턴스인 경우 Init() 실행x
        // Init();
    }

    #region Initialization

    protected override void InitOnce()
    {
        if (soundRoot == null)
        {
            var trans = transform.Find("Sounds");
            if (trans == null)
            {
                GameObject go = new GameObject("Sounds");
                go.transform.SetParent(transform); // 인스펙터로 연결된 대상이 없을 경우 매니저 하위에 임의로 생성
                soundRoot = go.transform;
            }
            else
            {
                soundRoot = trans;
            }
        }
        string[] soundTypeNames = System.Enum.GetNames(typeof(Enums.AudioType));
        for (int i = 0; i < soundTypeNames.Length - 1; i++) // 마지막 타입은 Max이기 때문에 Length -1
        {
            GameObject go = new GameObject { name = soundTypeNames[i] };
            audioSources[i] = go.AddComponent<AudioSource>(); // 오디오 재생용 컴포넌트 부착
            audioSources[i].spatialBlend = 0; // 2D 게임이기 때문에 0
            PlayerPrefs.GetFloat($"{soundTypeNames[i]}{VolumeSuffix}", 1.0f); // PlayerPrefs로부터 볼륨 사용자 설정 불러오기. 저장된 설정이 없으면 1.0f 적용
            
            go.transform.SetParent(soundRoot); 
        }

        audioSources[(int)Enums.AudioType.Bgm].loop = true; // Bgm, SubBgm의 기본 설정: 반복 재생
        audioSources[(int)Enums.AudioType.SubBgm].loop = true;
        
        // ResourceManager.Instance.SubscribePreLoad(InitAfterLoad);
        //todo: 필요하다면, 씬 이동 전 정리작업 등록
        // GameSceneManager.Instance.RegisterCleanupTask(async () =>
        // {
        //     await Clear();  
        // });
    }

    protected override void InitOnceAfterPreLoad(bool isLoadCompleted)
    {
        
    }

    protected override void Init()
    {
        // if (soundRoot == null)
        // {
        //     var trans = transform.Find("Sounds");
        //     if (trans == null)
        //     {
        //         GameObject go = new GameObject("Sounds");
        //         go.transform.SetParent(transform); // 인스펙터로 연결된 대상이 없을 경우 매니저 하위에 임의로 생성
        //         soundRoot = go.transform;
        //     }
        //     else
        //     {
        //         soundRoot = trans;
        //     }
        // }
        // string[] soundTypeNames = System.Enum.GetNames(typeof(Enums.AudioType));
        // for (int i = 0; i < soundTypeNames.Length - 1; i++) // 마지막 타입은 Max이기 때문에 Length -1
        // {
        //     GameObject go = new GameObject { name = soundTypeNames[i] };
        //     audioSources[i] = go.AddComponent<AudioSource>(); // 오디오 재생용 컴포넌트 부착
        //     audioSources[i].spatialBlend = 0; // 2D 게임이기 때문에 0
        //     PlayerPrefs.GetFloat($"{soundTypeNames[i]}{VolumeSuffix}", 1.0f); // PlayerPrefs로부터 볼륨 사용자 설정 불러오기. 저장된 설정이 없으면 1.0f 적용
        //     
        //     go.transform.SetParent(soundRoot); 
        // }
        //
        // audioSources[(int)Enums.AudioType.Bgm].loop = true; // Bgm, SubBgm의 기본 설정: 반복 재생
        // audioSources[(int)Enums.AudioType.SubBgm].loop = true;
        //
        // // ResourceManager.Instance.SubscribePreLoad(InitAfterLoad);
        // //todo: 필요하다면, 씬 이동 전 정리작업 등록
        // // GameSceneManager.Instance.RegisterCleanupTask(async () =>
        // // {
        // //     await Clear();  
        // // });
    }

    protected override void InitAfterPreLoad(bool isDone)
    {
        //todo: 필요한 리소스 가져오기, 초기화
        TestBGM();
    }

    private void TestBGM()
    {
        var bgmClip = ResourceManager.Instance.Load<AudioClip>("Music.wav");
        Play(Enums.AudioType.Bgm, bgmClip);
    }

    private void SetVolume()
    {
        
    }
    
    public void ChangeSoundVolume(Enums.AudioType type, float value)
    {
        audioSources[(int)type].volume = Mathf.Clamp(value, 0f, 1f);
    }

    #endregion
    
    #region Play

    public void Play(Enums.AudioType type) // 이미 등록된 오디오클립 재생 (변경x). Effect 타입은 사용하지 말것
    {
        audioSources[(int)type]?.Play();
    }

    public void Play(Enums.AudioType type, string key, float pitch = 1.0f)
    { // key를 통해 오디오클립을 찾아서 재생
        LoadAudioClip(type, key, pitch, Play);
    }

    public void Play(Enums.AudioType type, AudioClip audioClip, float pitch = 1.0f)
    { // 오디오클립을 직접 전달하여 재생. pitch는 Effect 타입에만 사용
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

    protected override UniTask Clear()
    {
        base.Clear();
        foreach (var source in audioSources)
        {
            source.Stop();
        }
        audioClips.Clear();

        return UniTask.CompletedTask;
    }

    #endregion

    #region Frequently used audio Calls

    //todo: 자주 사용되는 오디오 클립 따로 등록해서 사용
    public void PlayGameOverSound()
    {
        Play(Enums.AudioType.Effect, "GameOver.wav");
    }

    public void PlayEnemyWaveStartingSound()
    {
        Play(Enums.AudioType.Effect, "EnemyWaveStarting.wav");
    }

    public void PlayMusic()
    {
        Play(Enums.AudioType.Bgm, "Music.wav");
    }

    public void PlayBuildingDamagedSound()
    {
        Play(Enums.AudioType.Effect, "BuildingDamaged.wav");
    }
    
    public void PlayBuildingDestroyedSound()
    {
        Play(Enums.AudioType.Effect, "BuildingDestroyed.wav");
    }

    public void PlayBuildingPlacedSound()
    {
        Play(Enums.AudioType.Effect, "BuildingPlaced.wav");
    }

    public void PlayEnemyDieSound()
    {
        Play(Enums.AudioType.Effect, "EnemyDie.wav");
    }
    
    public void PlayEnemyHitSound()
    {
        Play(Enums.AudioType.Effect, "EnemyHit.wav");
    }
    
    #endregion

    
}
