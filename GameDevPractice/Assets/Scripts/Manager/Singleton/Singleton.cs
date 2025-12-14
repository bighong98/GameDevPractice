using System;
using System.Reflection;

// 제네릭 + 리플렉션 + Lazy 싱글톤
// 상속 클래스는 반드시 [Preserve] 어트리뷰트 적용 필요 (IL2CPP 스트리핑 대응)
// using UnityEngine.Scripting;
// [Preserve]
public abstract class Singleton<T> where T : class
{
    // Lazy + ExecutionAndPublication 기반 지연 초기화 + 멀티 스레드 세이프 보장
    private static readonly Lazy<T> _instance = new Lazy<T>(CreateInstance, System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
    // 외부 접근 프로퍼티
    public static T Instance => _instance.Value;

    private static T CreateInstance()
    {
        var ctor = typeof(T).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null);

        if (ctor == null)
            throw new InvalidOperationException($"[Singleton] {typeof(T).Name} 클래스에 private 생성자가 없습니다.");

        return (T)ctor.Invoke(null);
    }
}