using System;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace TH.Resource
{
    // TypeHolder의 데이터(BaseType)와 원본프리팹(Origin) 참조에 접근 제공 목적 인터페이스
    public interface ITypeHolder
    {
        BaseTypeSO BaseType { get; }
        GameObject Origin { get; set; }
        UniTask<BaseTypeSO> GetTypeAsync();
        void DeliverTypeData();
    }
    // 제네릭 버전
    public interface ITypeHolder<T> where T : BaseTypeSO
    {
        UniTask<T> GetTypeAsync();
    }
}

