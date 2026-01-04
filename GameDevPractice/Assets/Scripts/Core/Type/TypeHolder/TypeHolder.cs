using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using TH.Core.Pool;
using TH.Utils;
using TH.Core.Service;

namespace TH.Resource
{
    // ScriptableObject 기반 타입 데이터(TypeSO)와 원본 프리팹(Origin)을 함께 관리하는 컴포넌트
    // Addressables 기반으로 타입 데이터를 비동기 로드 -> 로드 완료 후 종속 컴포넌트(ITypeDependent)에 전달
    // 오브젝트 풀링(PoolManager)과 연동되어 객체 생성/획득/반환/파괴 이벤트를 관리
    // 씬에 배치 후 오브젝트 풀에 편입 기능 지원
    public class TypeHolder<T> : MonoBehaviour, ITypeHolder<T>, IPoolObject where T : BaseTypeSO
    {
        // 실제 ScriptableObject 타입 데이터 (직접 참조 또는 Addressable 로드 결과)
        // 인스펙터로 type을 직접 등록할 경우 typeRef에 의한 비동기 로드가 작동하지 않음에 주의 (덮어쓰기 x)
        [SerializeField] private T type; 
        public AssetReferenceT<T> typeRef; // Addressable 에셋 참조 (런타임 로드용)
        
        public GameObject Origin { get; set; } // 오브젝트 풀링 적용시 원본 프리팹 참조 저장 목적. setter가 있지만 PoolingManager 이외

        public T Type => type;
        public BaseTypeSO BaseType => Type; // BaseTypeSO 인터페이스 접근용

        private bool isInit; // 최초 1회 초기화 여부 (OnCreateFromPool()에서 갱신)
        [SerializeField] [Tooltip("씬에 배치되어 생성된 경우, 자동으로 오브젝트 풀에 등록할지 여부 (원본 프리팹과 동일한 경우에만 사용)")] 
        private bool addToPool;
        
        private CancellationToken token; // 게임오브젝트 파괴 감지 토큰
        
        // IPoolObject Event
        public event Action OnCreate;
        public event Action OnGet;
        public event Action OnRelease;
        public event Action OnDestroy;
        
        private void Awake()
        {
            if (isInit) return; // 이미 OnCreateFromPool()이 실행된 경우 실행x
            token = destroyCancellationToken; // 오브젝트 파괴와 연동된 CTS 토큰 캐싱

            InitializeType();
            
            if (addToPool)
                AddToPool(); // 필요시 자동으로 오브젝트 풀에 등록
            // 오브젝트 풀 이벤트 수동 호출
            OnCreateFromPool();
            OnGetFromPool();
            
            // 종속 컴포넌트(ITypeDependent)에 타입 데이터 전달
            DeliverTypeData();
        }
        
        // Awake 시점에 typeRef 등록이 되어있지 않은 경우 수동으로 AssetReference 기반 비동기 로드 실행 
        // TypeHolder를 상속받은 클래스에서 Start() 실행이 필요할 경우
        // -> 별도로 await InitializeTypeAsync(); 호출 필요 (void 타입은 await 불가능)
        protected virtual async void Start()
        {
            try { await InitializeTypeAsync(); }
            catch (Exception e ) { this.LogError($"{e}"); }
        }

        private void InitializeType()
        {
            if (type != null) return; // 이미 type 로드가 완료된 경우 실행x
            if (typeRef == null || !typeRef.RuntimeKeyIsValid())
                throw new InvalidOperationException(
                    $"[{gameObject.name}.{nameof(InitializeTypeAsync)}] invalid AssetReference for type data");
            
            ResourceManager.Instance.TryLoad(typeRef, out type);
        }

        // Addressables에서 ScriptableObject 타입 데이터를 비동기 로드
        // 동일한 참조를 중복 로드하지 않도록 내부적으로 캐싱함
        protected async UniTask InitializeTypeAsync()
        {
            if (type != null) return; // 이미 type 로드가 완료된 경우 실행x
            if (typeRef == null || !typeRef.RuntimeKeyIsValid())
                throw new InvalidOperationException(
                    $"[{gameObject.name}] InitializeTypeAsync - invalid AssetReference for type data");
            // 타입 데이터 비동기 로드 시작
            type = await ResourceManager.Instance.ExtractAssetRefAsync<T>(typeRef, token);

            if (token.IsCancellationRequested) return;
            Logg.Log($"[{gameObject.name}] InitializeTypeAsync - " +
                     $"type: {type}", Logg.LoggingMode.Completed);
        }

        // 간단한 오브젝트에 데이터 주입 목적으로 사용
        // or 런타임에 외부에서 새로운 AssetReferenceT로 타입을 교체해야만 때 사용
        // 기존 참조가 존재하는 경우 덮어쓰기 허용 x
        public async UniTask SetTypeAsync(AssetReferenceT<T> typeReference)
        {
            // 이미 typeRef가 존재한다면 overwrite 허용x
            if (typeRef != null && !token.IsCancellationRequested) {
                Logg.LogError($"[{gameObject.name}.{nameof(InitializeTypeAsync)}.SetTypeRef()] " +
                              $"typeReference overwriting is not accepted");
                return; 
            }
            // typeRef 변경 후 즉시 런타임 타입 데이터 로드&캐싱
            typeRef = typeReference;
            await InitializeTypeAsync();
        }

        // 타입 데이터 비동기 반환
        // 외부에서 오브젝트 생성 직후 직접 타입 에셋 참조 반환이 필요한 경우 사용
        public async UniTask<T> GetTypeAsync()
        {
            if (type != null) return type; // 런타임에 캐싱된 타입이 있다면 즉시 반환
            if (typeRef != null) return null; // type AssetReference도 없다면 null 반환

            await InitializeTypeAsync();
            return type;
        }

        // 로드된 타입 데이터를 ITypeDependent 인터페이스를 구현한 모든 컴포넌트에 전달
        private void DeliverTypeData()
        {
            foreach (var dependent in GetComponents<ITypeDependent>())
            {
                dependent.ReceiveType(type);
            }
        }

        // TypeSO에 지정된 prefab을 기반으로 오브젝트 풀을 생성/등록
        // addToPool 옵션이 true일 때 자동 호출
        private void AddToPool()
        {
            // NRE 방어
            if (token.IsCancellationRequested 
                || !gameObject.IsNotNull() 
                || gameObject is not { activeSelf: true }) return;
            // typeSO에 프리팹 데이터가 존재하는지 확인
            if (type is not { prefab: {} prefabData } ) return; 
            
            // 오브젝트 풀 생성 시도
            if (Origin == null) Origin = prefabData;
            PoolManager.Instance.GetPool(prefab: prefabData);
            Logg.Log($"[{GetType().Name}.{nameof(AddToPool)}()] " +
                     $"Origin == prefabData: {Origin == prefabData}", Logg.LoggingMode.Completed);
        }

        #region Pool Method (IPoolObject)

        public virtual void OnCreateFromPool()
        {
            if (isInit || token.IsCancellationRequested) return;
            isInit = true;
            
            OnCreate?.Invoke();
        }

        public virtual void OnGetFromPool()
        {
            if (token.IsCancellationRequested) return;
            OnGet?.Invoke();
        }

        public virtual void OnReleaseFromPool()
        {
            if (token.IsCancellationRequested) return;
            OnRelease?.Invoke();
        }

        public virtual void OnDestroyFromPool()
        {
            if (token.IsCancellationRequested) return;
            OnDestroy?.Invoke();
        }
        
        public virtual void ReleaseSelf()
        {
            if (token.IsCancellationRequested) return;
            if (gameObject.activeSelf && Origin != null)
                PoolManager.Instance.ReleaseFromPool(this);
        }

        #endregion
    }
}


#region Deprecated

// private void SetMinimapSprite()
// {
//     if (type.showInMinimap)
//     {
//         const string minimapName = "minimapSprite";
//     
//         Transform trs = transform.Find(minimapName);
//         GameObject go = trs != null ? trs.gameObject : new GameObject(minimapName);
//     
//         if (trs == null)
//         {
//             go.transform.SetParent(transform);
//             go.transform.localPosition = Vector3.zero;
//         }
//
//         go.layer = LayerMask.NameToLayer("Minimap");
//
//         var minimapSpriteRenderer = go.GetOrAddComponent<SpriteRenderer>();
//         minimapSpriteRenderer.sprite = Type.minimapSprite;
//     }
// }

#endregion

