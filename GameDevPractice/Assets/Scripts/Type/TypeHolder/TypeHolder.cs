using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using TH.Core.Pool;

// 타입 데이터(TypeSO), 원본 프리팹 객체(Origin)을 포함하는 MonoBehaviour 기반 컴포넌트
// 가능한 오브젝트 풀링해서 사용할 것 (PoolingManager.cs 참조)
public class TypeHolder<T> : MonoBehaviour, ITypeHolder, IPoolObject where T : BaseTypeSO
{
    [SerializeField] private T _type;
    [Tooltip("임시 사용, 추후 프로퍼티로 변경 예정")]public T type;
    public AssetReferenceT<T> typeRef;
    public GameObject Origin { get; set; } // 오브젝트 풀링 적용시 원본 프리팹 참조 저장 목적. setter가 있지만 PoolingManager 이외
    // public PoolKey PoolKey { get; set; }
    public BaseTypeSO BaseType => type;

    public event Action OnCreate;
    public event Action OnGet;
    public event Action OnRelease;
    public event Action OnDestroy;

    private bool isInit; // 최초 1회 초기화 여부 (OnCreateFromPool()에서 갱신)
    [SerializeField] [Tooltip("씬에 배치되어 생성된 경우, 자동으로 오브젝트 풀에 등록할지 여부 (원본 프리팹과 동일한 경우에만 사용)")] private bool addToPool;

    private void Awake()
    {
        if (isInit) return; // 이미 OnCreateFromPool()이 실행된 경우 실행x

        ResourceManager.Instance.ReserveOperation(() =>
        { // ResourceManager에게 초기화 작업 예약
            GetTypeFromRef().ContinueWith(() =>
            {
                if (addToPool)
                    AddToPool(); // 필요시 수동으로 오브젝트 풀에 등록
                OnCreateFromPool();
                OnGetFromPool();
            });
        });
    }

    private async UniTask GetTypeFromRef()
    {
        if (_type != null || !typeRef.RuntimeKeyIsValid()) return;
        type = _type = await Util.ExtractAssetRefAsync(typeRef);
        Util.Log($"[{gameObject.name}.{nameof(GetTypeFromRef)}] type: {_type}", Util.LoggingMode.Completed);
        SetMinimapSprite();
    }

    public async UniTask SetTypeRef(AssetReferenceT<T> typeReference)
    {
        typeRef = typeReference;
        type = _type = await Util.ExtractAssetRefAsync(typeReference);
        SetMinimapSprite();
    }

    public async UniTask<T> GetSafeType()
    {
        if (typeRef == null)
        {
            return null;
        }
        if (_type == null)
        {
            _type = await Util.ExtractAssetRefAsync(typeRef);
        }
        
        return _type;
    }

    private void AddToPool()
    {
        if (gameObject is not {activeSelf: true} ) 
            return; // NRE 방어
        if (_type is not { prefab: {} prefabData } ) 
            return; // typeSO에 프리팹 데이터가 존재하는지 확인
        
        if (Origin == null) Origin = prefabData;
        Util.Log($"[{typeof(TypeHolder<T>).Name}.{nameof(AddToPool)}()] Origin == prefabData: {Origin == prefabData}");
        PoolManager.Instance.GetPool(prefab: prefabData); // 오브젝트 풀 생성 시도
        // PoolingManager.Instance.ReserveOperation(() =>
        // {
        //     if (gameObject is not {activeSelf: true}) return; // NRE 방어
        //     if (_type is not { prefab: {} prefabData }) return; // typeSO에 프리팹 데이터가 존재하는지 확인
        //     if (Origin == null) Origin = prefabData;
        //     Util.Log($"[{typeof(TypeHolder<T>).Name}.{nameof(AddToPool)}()] Origin == prefabData: {Origin == prefabData}");
        //     PoolingManager.Instance.GetPool<TypeHolder<T>>(prefab: prefabData); // 오브젝트 풀 생성 시도
        // });
    }

    #region Pool Method (IPoolObject)

    public virtual void OnCreateFromPool()
    {
        if (isInit) return;
        isInit = true;
        
        OnCreate?.Invoke();
    }

    public virtual void OnGetFromPool()
    {
        OnGet?.Invoke();
    }

    public virtual void OnReleaseFromPool()
    {
        OnRelease?.Invoke();
    }

    public virtual void OnDestroyFromPool()
    {
        OnDestroy?.Invoke();
    }
    
    public virtual void ReleaseSelf()
    {
        // 제네릭 문제로 반드시 하위 클래스에서 다음과 같이 오버라이드해서 사용할 것
        // if (gameObject.activeSelf && Origin != null)
        //     PoolingManager.Instance.ReleaseFromPool(this);
        if (gameObject.activeSelf && Origin != null)
            PoolManager.Instance.ReleaseFromPool(this);
    }

    #endregion
    
    private void SetMinimapSprite()
    {
        if (_type.showInMinimap)
        {
            const string minimapName = "minimapSprite";
    
            Transform trs = transform.Find(minimapName);
            GameObject go = trs != null ? trs.gameObject : new GameObject(minimapName);
    
            if (trs == null)
            {
                go.transform.SetParent(transform);
                go.transform.localPosition = Vector3.zero;
            }

            go.layer = LayerMask.NameToLayer("Minimap");

            var minimapSpriteRenderer = go.GetOrAddComponent<SpriteRenderer>();
            minimapSpriteRenderer.sprite = type.minimapSprite;
        }
    }
}
