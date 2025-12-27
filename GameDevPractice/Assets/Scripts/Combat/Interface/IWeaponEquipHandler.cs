using System;
using TH.Resource;
using UnityEngine;

namespace TH.Combat
{
    // 임시 사용 중 (-> 추후 무기를 포함한 별도의 장비 관리 시스템 구현 시 인터페이스 수정)
    // 장착하는 무기 관련 정보 
    public interface IWeaponEquipHandler
    {
        event Action<WeaponTypeSO, Animator> OnEquipWeapon;
        
        public bool IsEquippingWeapon { get; }
        public (WeaponTypeSO weapon, Animator animator) GetWeaponEquipperInfo { get; }
    }
}

