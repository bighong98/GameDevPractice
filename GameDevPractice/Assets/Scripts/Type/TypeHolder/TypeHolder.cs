using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using TH.Core.Pool;
using TH.Resource;
using TH.Utils;

// 타입 데이터(TypeSO), 원본 프리팹 객체(Origin)을 포함하는 MonoBehaviour 기반 컴포넌트
// 가능한 오브젝트 풀링해서 사용할 것 (PoolingManager.cs 참조)
public class TypeHolder<T> : MonoBehaviour, ITypeHolder<T>, IPoolObject where T : BaseTypeSO
{
    [SerializeField] private T type;
    public T Type => type;
    public AssetReferenceT<T> typeRef;
    public GameObject Origin { get; set; } // 오브젝트 풀링 적용시 원본 프리팹 참조 저장 목적. setter가 있지만 PoolingManager 이외
    public BaseTypeSO BaseType => Type;

    public event Action OnCreate;
    public event Action OnGet;
    public event Action OnRelease;
    public event Action OnDestroy;

    private bool isInit; // 최초 1회 초기화 여부 (OnCreateFromPool()에서 갱신)
    [SerializeField] [Tooltip("씬에 배치되어 생성된 경우, 자동으로 오브젝트 풀에 등록할지 여부 (원본 프리팹과 동일한 경우에만 사용)")] private bool addToPool;
    private CancellationToken token;
    
    private void Awake()
    {
        if (isInit) return; // 이미 OnCreateFromPool()이 실행된 경우 실행x
        token = destroyCancellationToken;

        ResourceManager.Instance.WaitForPreLoadOnlyOnce(() =>
        { // ResourceManager에게 초기화 작업 예약
            GetTypeFromRef().ContinueWith(() =>
            {
                if (addToPool)
                    AddToPool(); // 필요시 수동으로 오브젝트 풀에 등록
                OnCreateFromPool();
                OnGetFromPool();
            });

            DeliverTypeData();
        });
    }

    private async UniTask GetTypeFromRef()
    {
        if (type != null || !typeRef.RuntimeKeyIsValid()) return;
        // type = await Util.ExtractAssetRefAsync<T>(typeRef);
        type = await ResourceManager.Instance.ExtractAssetRefAsync<T>(typeRef, token);
        Logg.Log($"[{gameObject.name}.{nameof(GetTypeFromRef)}] type: {type}", Logg.LoggingMode.Completed);
        SetMinimapSprite();
    }

    public async UniTask SetTypeRef(AssetReferenceT<T> typeReference)
    {
        typeRef = typeReference;
        // type = await Util.ExtractAssetRefAsync(typeReference);
        type = await ResourceManager.Instance.ExtractAssetRefAsync<T>(typeReference, token);
        SetMinimapSprite();
    }

    public async UniTask<T> GetTypeAsync()
    {
        if (typeRef == null) return null;
        
        if (type == null)
        {
            type = await ResourceManager.Instance.ExtractAssetRefAsync<T>(typeRef, token);
        }
        
        return type;
    }

    private void DeliverTypeData()
    {
        GetTypeAsync().ContinueWith((data) =>
        {
            if (data == null)
            {
                Logg.LogError($"[{name}.{nameof(GetType)}.{nameof(DeliverTypeData)}] failed to load from assetRefT '{typeRef}'");
                return;
            }
            
            foreach (var dependent in GetComponents<ITypeDependent>())
            {
                dependent.ReceiveType(data);
            }
        });
    }

    private void AddToPool()
    {
        if (gameObject is not {activeSelf: true} ) return; // NRE 방어
        if (type is not { prefab: {} prefabData } ) return; // typeSO에 프리팹 데이터가 존재하는지 확인
        
        if (Origin == null) Origin = prefabData;
        Logg.Log($"[{GetType().Name}.{nameof(AddToPool)}()] Origin == prefabData: {Origin == prefabData}", Logg.LoggingMode.Completed);
        PoolManager.Instance.GetPool(prefab: prefabData); // 오브젝트 풀 생성 시도
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
        if (gameObject.activeSelf && Origin != null)
            PoolManager.Instance.ReleaseFromPool(this);
    }

    #endregion
    
    private void SetMinimapSprite()
    {
        if (type.showInMinimap)
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
            minimapSpriteRenderer.sprite = Type.minimapSprite;
        }
    }
}
