using Cysharp.Threading.Tasks;
using UnityEngine;

public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;
    public static T Instance
    {
        get
        {
            if (_instance != null)
                return _instance;

            // if (Util.IsQuitting)
            //     return null;
            
            _instance = FindFirstObjectByType<T>();
            if (_instance == null)
            {
                GameObject obj = new GameObject(typeof(T).Name);
                _instance = obj.AddComponent<T>();
                DontDestroyOnLoad(obj);
            }

            return _instance;
        }
    }

    protected virtual void Awake()
    {
        if (_instance == null)
        {
            _instance = this as T;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Util.Log($"{typeof(T).Name}: 중복 인스턴스가 존재하여 파괴됩니다.");
            Destroy(gameObject);
        }
    }

    protected virtual void Start()
    {
        if (IsInvalidInstance()) return; // 중복 인스턴스일 경우, 이벤트 구독하지 않음
        GameSceneManager.Instance.notifySceneLoaded += OnSceneLoaded;
    }

    protected bool IsInvalidInstance()
    {
        return _instance != this;
    }

    protected virtual void OnSceneLoaded(bool dummy)
    {
        // 씬 이동/재시작마다 초기화가 필요한 참조, 데이터가 있을 경우 오버라이드해서 사용
    }

    #region Deprecated

    // private static GameObject CreateManagerRoot()
    // {
    //     GameObject go = new GameObject("Managers");
    //     try
    //     {
    //         go.tag = "Manager"; // 태그가 등록되지 않았다면 Unity 에디터에서 경고 발생할 수 있음
    //     }
    //     catch
    //     {
    //         Util.Log("There is no 'Manager' tag");
    //     }
    //
    //     return go;
    // }

    #endregion
    
}
