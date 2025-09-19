using UnityEngine;

public interface ITypeDependent
{
    void ReceiveType(ScriptableObject typeInfo);
}

public interface ITypeDependent<in T> : ITypeDependent where T : BaseTypeSO
{
    new void ReceiveType(T typeInfo);
}
