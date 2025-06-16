using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BaseController : MonoBehaviour
{
    public Enums.ObjectType ObjType;
    public int poolIndex;
    private bool _init = false; // 초기화 여부
    protected bool IsInit => _init; // 자식 오브젝트에서만 사용 가능한 초기화 여부 확인 목적 프로퍼티

    public virtual bool Init()
    {
        if (_init)
            return false;

        _init = true;
        return true;
    }
}
