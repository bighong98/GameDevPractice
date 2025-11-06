using System;
using System.Collections.Generic;
using TH.Utils;

namespace TH.Core.Service
{
    public static class ServiceLocator
    {
        // 생성 완료된 <서비스 타입 - 서비스 인스턴스> 목록 
        private static readonly Dictionary<Type, object> services = new(); 
        // <서비스 타입 - 서비스 생성자 델리게이트> 목록 (서비스 인스턴스 생성자 파라미터 대응 및 지연 생성 지원)
        private static readonly Dictionary<Type, Func<IServiceProvider, object>> factories = new(); 
        private static readonly object _gate = new(); // 딕셔너리 동기화 유지용
        // 현재 resolve(type to instance) 진행 중인 목록 (순환 의존 감지 목적)
        [ThreadStatic] private static HashSet<Type> _resolving; 

        // Register
        // 서비스 등록 메서드
        // Bootstrapper.cs 등 초기화 전용 클래스에서만 사용 권장 
        
        // 별도의 외부 의존성을 필요로하지 않는 서비스 클래스 등록 (즉시 생성)
        public static void Register<TService>(TService service) where TService : class
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            var t = typeof(TService);

            lock (_gate)
            {
                // 중복 등록 시 에러 메시지 전달 및 종료
                if (services.ContainsKey(t) || factories.ContainsKey(t))
                {
                    Logg.LogError($"[ServiceLocator] service '{t.Name}' is already registered");
                    return;
                }
                // 신규 서비스 등록
                services[t] = service;
            }
        }

        // 1개 이상의 외부 의존성을 필요로하는 서비스 클래스 등록 
        // 즉시 인스턴스를 생성하는 대신 인스턴스 생성자 델리게이트를 등록함 (lazy initialization)
        // 서비스 최초 사용(Get) 시점에서 실제 서비스 인스턴스가 생성/등록됨
        // -> 등록 시점에 즉시 초기화 필요한 경우 추가 호출이 필요
        public static void Register<TService>(Func<IServiceProvider, TService> factory) 
            where TService : class
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            var t = typeof(TService);

            lock (_gate)
            {
                if (services.ContainsKey(t) || factories.ContainsKey(t))
                {
                    Logg.LogError($"[ServiceLocator] service '{t.Name}' is already registered");
                    return;
                }
            }

            factories[t] = factory;
        }

        // 등록된 서비스 존재 여부 확인
        private static bool IsRegistered<TService>() where TService : class
        {
            var t = typeof(TService);
            lock (_gate)
                return services.ContainsKey(t) || factories.ContainsKey(t);
        }
        // 등록된 서비스 접근용 public 메서드 (+ 제네릭)
        public static TService Get<TService>() where TService : class
            => (TService)Get(typeof(TService));
        
        // 등록된 서비스 접근용 public 메서드
        // 추후 리플렉션 기반 서비스 생성 등 비제네릭 조회 메서드가 필요해질 경우 public으로 전환
        private static object Get(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            // 1) 기존 서비스 조회 및 즉시 반환
            lock (_gate)
            {
                // 타입에 대응하는 서비스 목록 조회
                if (services.TryGetValue(type, out var cached))
                    return cached; // 기존 서비스가 존재할 경우 즉시 반환
            }
            // 2) 서비스 생성자 델리게이트 준비
            Func<IServiceProvider, object> factory;
            
            lock (_gate)
            {
                // 2-a) 타입에 대응하는 델리게이트 목록 조회
                if (!factories.TryGetValue(type, out factory))
                    throw new KeyNotFoundException(
                        $"[ServiceLocator] service '{type.Name}' is not registered");
            }
            // 2-b) 현재 처리 중인(resolving) 서비스 타입 등록
            // 이미 동일 타입에 대한 인스턴스 생성이 진행 중인 경우 -> 순환 참조로 간주하고 throw
            _resolving ??= new HashSet<Type>();
            if (!_resolving.Add(type)) 
                throw new InvalidOperationException(
                    $"Circular dependency detected while resolving {type.Name}.");

            object service = null;
            try
            {
                // 델리게이트를 통해 생성자 호출 -> 서비스 인스턴스 생성
                service = factory(InternalProvider.Instance);
                // 인스턴스가 null일 경우 throw
                if (service == null)
                    throw new InvalidOperationException($"Factory for {type.Name} failed.");

                lock (_gate)
                {
                    // 서비스 목록 재조회(중복 생성 방지)
                    if (services.TryGetValue(type, out var existing))
                        return existing;
                    // 기존 서비스 인스턴스가 없다면 신규 등록
                    services[type] = service;
                }
            }
            finally { _resolving.Remove(type); } // resolve 목록에서 현재 서비스 타입 제거

            return service; // 서비스 인스턴스 반환
        }
        
        // 서비스 내부 의존성 Resolving 처리용 Service Provider
        private sealed class InternalProvider : IServiceProvider
        {
            public static readonly InternalProvider Instance = new();

            public T Get<T>() where T : class => ServiceLocator.Get<T>();
            public object Get(Type type) => ServiceLocator.Get(type);
            public bool IsRegistered<T>() where T : class => ServiceLocator.IsRegistered<T>();
        }
    }
}


