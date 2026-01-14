using UnityEngine;

public static class Enums
{
    public enum Scene
    {
        MainMenuScene,
        SelectScene
    }
    public enum GameScene
    {
        InGameScene,
        Scene_2,
        Scene_3
    }
    
    public enum AudioType
    {
        Bgm,
        SubBgm,
        Effect,
        Max
    }
    
    public enum VolumeGroup
    {
        Master,
        Bgm,
        Effect
    }

    public enum UIRenderType
    {
        ScreenOverlay,
        ScreenCamera,
        WorldSpace,
    }

    public enum OptionCategory
    {
        Sound,
        Blank,
    }
    
    public enum UIEvent
    {
        Click, // 클릭 중 계속 실행
        PointerDown, // 클릭 시작 시 실행
        PointerUp, // 클릭 종료 시 실행
        PointerEnter, // UI 위에 포인터가 올려진 경우 실행
        PointerExit, // 포인터가 UI를 벗어난 경우 실행
        BeginDrag, // 드래그 시작 시 실행
        EndDrag, // 드래그 종료 시 실행
        Select,
        Deselect,
    }

    public enum UIAnimationType
    {
        PopIn, // 커지면서 등장
        PopOut, // 작아지면서 퇴장
        FadeIn, // 선명해지면서 등장
        FadeOut, // 흐릿해지면서 퇴장   
    }

    public enum TooltipErrorType // error messages for each type are included in TooltipUI.cs
    {
        Empty, // 빈 에러타입. 에러가 없는데 에러 타입을 전달해야하는 경우 사용
        Occupied, // 다른 건물/유닛이 해당 지점을 점유하고 있는 경우
        Limited, // 특정 건물/유닛의 생산 한계를 넘은 경우
        OutOfRange, // 특정 건물/유닛의 생산이 지정 범위 밖에서 시도된 경우
        Insufficient, // 특정 건물/유닛 생산에 필요한 자원이 부족한 경우
    }

    public enum StatType
    {
        Attack,
    }
    
    public enum GameDifficulty
    {
        Easy,
        Normal,
        Hard


    }

    public enum ObjectType
    {
        
    }

    public enum EquippedItemSlotType
    {
        Weapon,
        Head,
        Body,
        Hand,
        Foot,
        Max
    }

    public enum EquipmentType
    {
        Armor,
        Weapon,
    }

    public enum ItemType
    {
        Default,
        Equipment,
        Countable,
        Single,
        Special,
    }

    public enum ArmorType
    {
        
    }

    public enum WeaponType
    {
        
    }
}

