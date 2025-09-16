using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

// 타입 데이터(TypeSO), 원본 프리팹 객체(Origin)을 포함하는 MonoBehaviour 기반 컴포넌트
// 가능한 오브젝트 풀링해서 사용할 것 (PoolingManager.cs 참조)
public class TypeHolder<T> : MonoBehaviour, ITypeHolder, IPoolObject where T : BaseTypeSO
{
    [SerializeField] private T _type;
    public T type;
    public AssetReferenceT<T> typeRef;
    public GameObject Origin { get; set; } // 오브젝트 풀링 적용시 원본 프리팹 참조 저장 목적. setter가 있지만 PoolingManager 이외
    public BaseTypeSO BaseType => type;

    public event Action OnCreate;
    public event Action OnGet;
    public event Action OnRelease;
    public event Action OnDestroy;

    private bool isInit;

    private void Awake()
    {
        if (isInit) return;

        ResourceManager.Instance.ReserveOperation(() =>
        {
            isInit = true;
            OnCreateFromPool();
            OnGetFromPool();
        });
    }

    private async UniTaskVoid GetTypeFromRef()
    {
        if (_type != null || !typeRef.RuntimeKeyIsValid()) return;
        type = _type = await Util.ExtractAssetRefAsync(typeRef);
        Util.Log($"[{gameObject.name}.{nameof(GetTypeFromRef)}] type: {_type}", Util.LoggingMode.InProgress);
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

    #region Pool Method (IPoolObject)

    public virtual void OnCreateFromPool()
    {
        if (isInit) return;
        isInit = true;
        GetTypeFromRef().Forget();
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
