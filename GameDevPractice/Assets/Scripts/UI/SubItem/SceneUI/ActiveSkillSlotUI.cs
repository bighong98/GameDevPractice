using System.Collections;
using TH.Resource;
using TH.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 단일 액티브 스킬 슬롯 표시 및 하이라이트 연출 UI 컴포넌트
public class ActiveSkillSlotUI : BaseSlotUI, IHighlightableSlotUI
{
    // 쿨다운 원형 게이지 이미지
    [SerializeField] private Image cooldownImage;
    // 시퀀스 타임아웃 원형 게이지 이미지
    [SerializeField] private Image sequenceTimeOutImage;
    // 쿨다운 숫자 텍스트
    [SerializeField] private TextMeshProUGUI cooldownText;
    // 슬롯 키 표시 텍스트
    [SerializeField] private TextMeshProUGUI slotKeyText;

    // 하이라이트 페이드 코루틴 핸들
    private Coroutine fadeCoroutine;
    // 현재 적용 중 하이라이트 타입 캐시
    private int currentHighlightType = -1;
    // 현재 슬롯에 바인딩된 스킬 참조
    private SkillTypeSO boundSkill;

    // 초기 표시 상태 정리 구간
    protected override void Awake()
    {
        base.Awake();
        InitializeUIState();
    }

    // 슬롯 스킬 바인딩 및 초기 표시값 반영
    public void SetSkill(SkillTypeSO skill)
    {
        boundSkill = skill;

        if (boundSkill == null)
        {
            Clear();
            return;
        }

        ApplySkillIcon(ResolveSkillIcon(boundSkill));
        SetCooldown(0f, boundSkill.Cooldown);
        SetSequenceTimeout(0f, 0f);
    }

    private static Sprite ResolveSkillIcon(SkillTypeSO skill)
    {
        if (skill == null)
            return null;

        if (skill.SkillSlotImage != null)
            return skill.SkillSlotImage;

        var comboSteps = skill.ComboSteps;
        if (comboSteps == null)
            return null;

        for (int i = 0; i < comboSteps.Count; i++)
        {
            SkillTypeSO comboStep = comboSteps[i];
            if (comboStep != null && comboStep.SkillSlotImage != null)
                return comboStep.SkillSlotImage;
        }

        return null;
    }


    // 쿨다운 잔여시간 표시 계산 및 텍스트 동기화
    public void SetCooldown(float remainingCooldown, float totalCooldown)
    {
        // 스킬 미바인딩 상태 시 표시 비활성
        if (boundSkill == null)
        {
            DisableSkillVisuals();
            return;
        }

        // 음수 입력 방지 보정값 계산
        float resolvedRemaining = Mathf.Max(0f, remainingCooldown);
        float resolvedTotal = Mathf.Max(0f, totalCooldown);

        // 쿨다운 이미지 비율 게이지 계산
        if (cooldownImage != null)
        {
            float fillAmount = resolvedTotal > 0f
                ? Mathf.Clamp01(resolvedRemaining / resolvedTotal)
                : 0f;

            cooldownImage.fillAmount = fillAmount;
            cooldownImage.enabled = fillAmount > 0f;
        }

        // 텍스트 컴포넌트 미지정 가드
        if (cooldownText == null)
            return;

        // 잔여시간 존재 시 소수점 한 자리 표시
        if (resolvedRemaining > 0f)
        {
            cooldownText.enabled = true;
            cooldownText.SetText($"{resolvedRemaining:0.0}");
            return;
        }

        // 잔여시간 0 상태 텍스트 비활성
        cooldownText.enabled = false;
        cooldownText.SetText(string.Empty);
    }

    // 시퀀스 타임아웃 게이지 표시 계산 및 반영
    public void SetSequenceTimeout(float remainingTimeout, float totalTimeout)
    {
        // 타임아웃 이미지 미지정 가드
        if (sequenceTimeOutImage == null)
            return;

        // 음수 입력 방지 보정값 계산
        float resolvedRemaining = Mathf.Max(0f, remainingTimeout);
        float resolvedTotal = Mathf.Max(0f, totalTimeout);
        // 스킬 바인딩 및 타임아웃 유효 구간 표시 조건
        bool shouldShow = boundSkill != null && resolvedTotal > 0f && resolvedRemaining > 0f;

        // 표시 조건 불충족 시 게이지 초기화
        if (!shouldShow)
        {
            sequenceTimeOutImage.fillAmount = 0f;
            sequenceTimeOutImage.enabled = false;
            return;
        }

        // 잔여시간 비율 기반 게이지 반영
        sequenceTimeOutImage.fillAmount = Mathf.Clamp01(resolvedRemaining / resolvedTotal);
        sequenceTimeOutImage.enabled = true;
    }


    // 슬롯 키 텍스트 표시/숨김 동기화
    public void SetSlotKeyText(string text)
    {
        if (slotKeyText == null)
            return;

        // 공백 문자열 입력 시 텍스트 비표시 처리
        Transform keyTextParent = slotKeyText.transform.parent;
        bool hasText = !string.IsNullOrWhiteSpace(text);
        if (!hasText)
        {
            slotKeyText.enabled = false;
            slotKeyText.SetText(string.Empty);

            if (keyTextParent != null && keyTextParent.gameObject.activeSelf)
                keyTextParent.gameObject.SetActive(false);

            return;
        }

        if (keyTextParent != null && !keyTextParent.gameObject.activeInHierarchy)
            keyTextParent.gameObject.SetActive(true);

        slotKeyText.enabled = true;
        slotKeyText.SetText(text);
    }

    // 슬롯 전체 상태 초기화 오버라이드
    public override void Clear()
    {
        base.Clear();
        boundSkill = null;

        // 시각 요소와 하이라이트 상태 초기화
        DisableSkillVisuals();
        SetSlotKeyText(string.Empty);
        UnHighlight();
    }

    // 기본 선택 하이라이트 진입점
    public override void Highlight() => Highlight((int)SlotHighlightType.Select);

    // 하이라이트 즉시 해제 처리
    public override void UnHighlight()
    {
        StopHighlightFade();
        currentHighlightType = -1;
        base.UnHighlight();
    }

    // 타입별 하이라이트 색상 반영 및 활성화
    public void Highlight(int type)
    {
        if (GetImage((int)Images.HighLightImage) is { } highlightImage)
        {
            // 타입별 시각 구분 색상 선택
            Color highlightColor = type switch
            {
                (int)SlotHighlightType.Modified => Color.white,
                (int)SlotHighlightType.Warn => Color.red,
                (int)SlotHighlightType.Casting => default,
                _ => Color.green,
            };
            if (highlightColor == default) return; // 임시 방어 코드, 캐스팅 스킬 하이라이트 효과 추가 후 제거
            // 공통 반투명 알파 강도 적용
            highlightColor.a = 0.25f;
            highlightImage.color = highlightColor;
        }

        // 현재 타입 캐시 후 베이스 하이라이트 호출
        currentHighlightType = type;
        base.Highlight();
    }

    // 지정 타입과 일치할 때만 하이라이트 해제
    public void UnHighlight(int type)
    {
        if (currentHighlightType != type)
            return;

        currentHighlightType = -1;
        base.UnHighlight();
    }

    // 지정 타입 하이라이트 페이드 아웃 실행
    public void UnHighlightWithFade(int type, float duration = 0.5f)
    {
        if (currentHighlightType != type)
            return;

        // 기존 페이드 중단 후 신규 페이드 시작
        StopHighlightFade();
        fadeCoroutine = StartCoroutine(CoUnHighlightFade(type, duration));
    }

    // 초기 UI 기본 상태 정리 루틴
    private void InitializeUIState()
    {
        DisableSkillVisuals();
        base.UnHighlight();

        // 키 텍스트 기본 비활성
        SetSlotKeyText(string.Empty);
    }

    // 슬롯 아이템 아이콘 반영 유틸리티
    private void ApplySkillIcon(Sprite iconSprite)
    {
        if (GetImage((int)Images.ItemImage) is not { } itemImage)
            return;

        // 스프라이트 존재 여부에 따른 표시 토글
        itemImage.sprite = iconSprite;
        itemImage.enabled = iconSprite != null;
    }

    // 스킬 관련 시각 요소 일괄 비활성화
    private void DisableSkillVisuals()
    {
        ApplySkillIcon(null);

        // 쿨다운 이미지 초기화
        if (cooldownImage != null)
        {
            cooldownImage.fillAmount = 0f;
            cooldownImage.enabled = false;
        }

        // 시퀀스 타임아웃 이미지 초기화
        if (sequenceTimeOutImage != null)
        {
            sequenceTimeOutImage.fillAmount = 0f;
            sequenceTimeOutImage.enabled = false;
        }

        // 쿨다운 텍스트 초기화
        if (cooldownText != null)
        {
            cooldownText.enabled = false;
            cooldownText.SetText(string.Empty);
        }
    }

    // 하이라이트 알파값 선형 감소 코루틴
    private IEnumerator CoUnHighlightFade(int type, float duration)
    {
        // 하이라이트 이미지 미획득 가드
        if (GetImage((int)Images.HighLightImage) is not { } highlightImage)
            yield break;

        // 페이드 시작 상태 캡처
        float elapsed = 0f;
        Color color = highlightImage.color;
        float startAlpha = color.a;

        // 지속 시간 동안 현재 타입 유지 시 알파 감쇠
        while (elapsed < duration && currentHighlightType == type)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            color.a = Mathf.Lerp(startAlpha, 0f, t);
            highlightImage.color = color;
            yield return null;
        }

        // 동일 타입 유지 시 최종 해제 보장
        if (currentHighlightType == type)
            UnHighlight(type);

        // 코루틴 핸들 정리
        fadeCoroutine = null;
    }

    // 진행 중 페이드 코루틴 중단 유틸리티
    private void StopHighlightFade()
    {
        if (fadeCoroutine == null)
            return;

        StopCoroutine(fadeCoroutine);
        fadeCoroutine = null;
    }
}

