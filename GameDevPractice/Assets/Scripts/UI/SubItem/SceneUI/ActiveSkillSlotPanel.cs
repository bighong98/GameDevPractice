using System;
using System.Collections.Generic;
using TH.Resource;
using TH.UI;
using UnityEngine;
using UnityEngine.EventSystems;

// 액티브 스킬 슬롯 UI 컬렉션 렌더링 및 호버 이벤트 전달 패널
public sealed class ActiveSkillSlotPanel : BaseUI, IHoverableStorageUI, IPointerMoveHandler, IPointerExitHandler
{
    // BaseUI 오브젝트 바인딩 키 정의
    private enum GameObjects
    {
        slots,
    }

    // 슬롯 UI 직렬화 컬렉션
    [SerializeField] private readonly List<ActiveSkillSlotUI> slotUIs = new();
    // 외부 노출용 읽기 전용 슬롯 컬렉션 캐시
    private IReadOnlyList<ActiveSkillSlotUI> readonlySlotUIs;

    // 마지막 호버 슬롯 인터페이스 캐시
    private ISlotUI lastHoveredSlot;

    // 슬롯 호버 진입 이벤트
    public event Action<int> OnSlotHovered;
    // 슬롯 호버 이탈 이벤트
    public event Action<int> OffSlotHovered;

    // 읽기 전용 슬롯 목록 접근 프로퍼티
    public IReadOnlyList<ActiveSkillSlotUI> SlotUIs
    {
        get
        {
            readonlySlotUIs ??= slotUIs.AsReadOnly();
            return readonlySlotUIs;
        }
    }

    // 현재 패널 슬롯 총 개수
    public int SlotCount => slotUIs.Count;

    // 초기 바인딩 및 슬롯 수집 실행 구간
    protected override void Awake()
    {
        base.Awake();
        BindObject(typeof(GameObjects));
        CollectSlotUIs();
    }

    // 인덱스 기반 슬롯 UI 조회
    public ActiveSkillSlotUI GetSlotUI(int index)
    {
        if (!IsValidSlotIndex(index))
            return null;

        return slotUIs[index];
    }

    // 인덱스 슬롯 스킬 아이콘 정보 반영
    public void DrawSkill(int index, SkillTypeSO skill)
    {
        if (!IsValidSlotIndex(index))
            return;

        slotUIs[index].SetSkill(skill);
    }

    // 인덱스 슬롯 쿨다운 표시 반영
    public void DrawCooldown(int index, float remainingCooldown, float totalCooldown)
    {
        if (!IsValidSlotIndex(index))
            return;

        slotUIs[index].SetCooldown(remainingCooldown, totalCooldown);
    }

    // 인덱스 슬롯 시퀀스 타임아웃 표시 반영
    public void DrawSequenceTimeout(int index, float remainingTimeout, float totalTimeout)
    {
        if (!IsValidSlotIndex(index))
            return;

        slotUIs[index].SetSequenceTimeout(remainingTimeout, totalTimeout);
    }


    // 인덱스 슬롯 키 바인딩 텍스트 반영
    public void SetSlotKeyText(int index, string keyText)
    {
        if (!IsValidSlotIndex(index))
            return;

        slotUIs[index].SetSlotKeyText(keyText);
    }

    // 기본 타입 하이라이트 적용
    public void HighlightSlot(int index)
    {
        if (!IsValidSlotIndex(index))
            return;

        slotUIs[index].Highlight();
    }

    // 타입 지정 하이라이트 적용
    public void HighlightSlot(int index, int highlightType)
    {
        if (!IsValidSlotIndex(index))
            return;

        slotUIs[index].Highlight(highlightType);
    }


    // 기본 하이라이트 해제
    public void UnHighlightSlot(int index)
    {
        if (!IsValidSlotIndex(index))
            return;

        slotUIs[index].UnHighlight();
    }

    public void UnHighlightSlot(int index, int highlightType)
    {
        if (!IsValidSlotIndex(index))
            return;
        slotUIs[index].UnHighlight(highlightType);
    }

    // 타입 지정 하이라이트 페이드 해제
    public void UnHighlightSlotWithFade(int index, int highlightType, float duration = 0.5f)
    {
        if (!IsValidSlotIndex(index))
            return;

        slotUIs[index].UnHighlightWithFade(highlightType, duration);
    }


    // 단일 슬롯 비우기
    public void ClearSlot(int index)
    {
        if (!IsValidSlotIndex(index))
            return;

        slotUIs[index].Clear();
    }

    // 전체 슬롯 비우기
    public void ClearAllSlots()
    {
        for (int i = 0; i < slotUIs.Count; i++)
        {
            slotUIs[i].Clear();
        }
    }

    // 포인터 이동 기반 슬롯 호버 전환 감지 루틴
    public void OnPointerMove(PointerEventData eventData)
    {
        switch (eventData.pointerEnter)
        {
            // 새 슬롯 진입 감지 시 이전 슬롯 이탈 이벤트 후 신규 슬롯 진입 이벤트 전달
            case { } target when target.TryGetComponent(out ISlotUI slotUI) && slotUI != lastHoveredSlot:
                if (lastHoveredSlot is { Index: { } lastHoveredIndex })
                    OffSlotHovered?.Invoke(lastHoveredIndex);

                lastHoveredSlot = slotUI;
                OnSlotHovered?.Invoke(slotUI.Index);
                break;

            // 포인터 대상 상실 시 마지막 호버 슬롯 이탈 처리
            case null when lastHoveredSlot != null:
                OffSlotHovered?.Invoke(lastHoveredSlot.Index);
                lastHoveredSlot = null;
                break;
        }
    }

    // 포인터 패널 이탈 시 호버 상태 해제 루틴
    public void OnPointerExit(PointerEventData eventData)
    {
        if (lastHoveredSlot == null)
            return;

        OffSlotHovered?.Invoke(lastHoveredSlot.Index);
        lastHoveredSlot = null;
    }

    // 슬롯 UI 컬렉션 구성 및 인덱스 부여 루틴
    private void CollectSlotUIs()
    {
        // 수동 할당 슬롯 목록 존재 시 인덱스만 재정렬
        if (slotUIs.Count > 0)
        {
            for (int i = 0; i < slotUIs.Count; i++)
            {
                if (slotUIs[i] != null)
                    slotUIs[i].SetIndex(i);
            }

            return;
        }

        // 슬롯 루트 오브젝트 미획득 가드
        if (GetObject((int)GameObjects.slots) is not { } slotsRoot)
            return;

        // 하위 슬롯 컴포넌트 자동 수집 및 순차 인덱스 할당
        int slotIndex = 0;
        foreach (var slotUI in slotsRoot.GetComponentsInChildren<ActiveSkillSlotUI>(true))
        {
            slotUI.SetIndex(slotIndex++);
            slotUIs.Add(slotUI);
        }
    }

    // 슬롯 인덱스 범위 검증 유틸리티
    private bool IsValidSlotIndex(int index)
    {
        return index >= 0 && index < slotUIs.Count;
    }
}

