using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

public class TypeHolder<T> : MonoBehaviour, ITypeHolder, IPoolObject where T : BaseTypeSO
{
    private T _type;
    public T type;
    public AssetReferenceT<T> typeRef;
    public GameObject Origin { get; set; } // 오브젝트 풀링 적용시 원본 프리팹 참조 저장 목적. setter가 있지만 수동으로 수정하지 않도록
    public BaseTypeSO BaseType => type;

    public event Action OnCreate;
    public event Action OnGet;
    public event Action OnRelease;
    public event Action OnDestroy;

    private async UniTaskVoid GetTypeFromRef()
    {
        if (_type != null || !typeRef.RuntimeKeyIsValid()) return;
        _type = await Util.ExtractAssetRefAsync(typeRef);
        SetMinimapSprite();
    }

    public async UniTask SetTypeRef(AssetReferenceT<T> typeReference)
    {
        typeRef = typeReference;
        _type = await Util.ExtractAssetRefAsync(typeReference);
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

    public virtual void OnCreateFromPool()
    {
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

    // 제네릭 문제로 반드시 하위 클래스에서 오버라이드해서 사용할 것
    public virtual void ReleaseSelf()
    {
        // if (gameObject.activeSelf && Origin != null)
        //     PoolingManager.Instance.ReleaseFromPool(this);
    }

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
            minimapSpriteRenderer.sprite = type.minimapSprite;
        }
    }
}
