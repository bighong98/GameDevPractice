using UnityEngine;

public interface ITypeHolder
{
    BaseTypeSO BaseType { get; }
    GameObject Origin { get; set; }
}
