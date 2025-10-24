using System;
using UnityEngine;
using Cysharp.Threading.Tasks;

// BaseTypeHolder를 상속받는 ~TypeHolder의 데이터(BaseType)와 원본프리팹(Origin) 참조에 접근 목적 인터페이스 
public interface ITypeHolder
{
    BaseTypeSO BaseType { get; }
    GameObject Origin { get; set; }
    UniTask<BaseTypeSO> GetTypeAsync();
    void DeliverTypeData();
}

public interface ITypeHolder<T> where T : BaseTypeSO
{
    UniTask<T> GetTypeAsync();
}
