using UnityEngine;

namespace TH.Resource
{
    // 초기화에 타입 데이터 (TypeSO) 컴포넌트에서 구현
    public interface ITypeDependent
    {
        void ReceiveType(ScriptableObject typeInfo);
    }
    // 제네릭 버전
    public interface ITypeDependent<in T> : ITypeDependent where T : BaseTypeSO
    {
        void ReceiveType(T typeInfo);
    }
}

