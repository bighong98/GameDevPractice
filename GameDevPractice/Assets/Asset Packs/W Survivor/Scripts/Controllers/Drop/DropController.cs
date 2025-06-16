// using System;
// using System.Collections;
// using System.Collections.Generic;
// using Redcode.Pools;
// using UnityEngine;
//
// public abstract class DropController : BaseController
// {
//     public virtual int DataId { get; set; }
//     public SpriteRenderer Sprite { get; set; }
//     
//     private void Awake()
//     {
//         Init();
//     }
//
//     public override bool Init()
//     {
//         base.Init();
//         Sprite = Util.GetOrAddComponent<SpriteRenderer>(gameObject);
//         
//         return true;
//     }
//
//     public virtual void OnAcquired()
//     {
//         // 습득시 호출
//     }
//
// }
