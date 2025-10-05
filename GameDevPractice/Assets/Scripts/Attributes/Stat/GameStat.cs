using UnityEngine;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using StatModCalcType = Enums.StatModCalcType;

namespace TH.Attribute.Stat
{
    [Serializable]
    public class GameStat : IGameStat
    {
        public float BaseValue;
        
        protected bool isDirty = true;
        protected float value;
        protected float lastBaseValue = float.MinValue;

        protected readonly List<StatModifier> statModifiers;
        public readonly ReadOnlyCollection<StatModifier> StatModifiers;

        public event Action OnStatChanged;
        
        public virtual float Value
        {
            get
            {
                if (isDirty || !Mathf.Approximately(BaseValue, lastBaseValue)) // 값에 변동사항이 있다면
                {
                    lastBaseValue = BaseValue;
                    value = CalculateFinalValue();
                    isDirty = false;
                }

                return value;
            }
        }

        public GameStat()
        {
            statModifiers = new List<StatModifier>();
            StatModifiers = statModifiers.AsReadOnly();
        }

        public GameStat(float baseValue) : this()
        {
            BaseValue = baseValue;
        }

        // 모드 추가
        public virtual void AddModifier(StatModifier mod)
        {
            isDirty = true;
            statModifiers.Add(mod);
            statModifiers.Sort(CompareModOrder); // CompareOrder() 규칙에 따라 모드 재정렬
            OnStatChanged?.Invoke(); // 해당 스탯 변경 시 필요한 작업이 있다면 실행
        }

        // 특정 모드 제거
        public virtual bool RemoveModifier(StatModifier mod)
        {
            if (statModifiers.Remove(mod))
            {
                isDirty = true;
                OnStatChanged?.Invoke(); // 해당 스탯 변경 시 필요한 작업이 있다면 실행
                return true; // 제거 성공: true 반환
            }
            return false; // 제거 시도한 모드가 없는 경우: false 반환
        }
        
        // 특정 출처(장비, 스킬 등)으로부터 추가된 모드 전부 제거
        public virtual bool RemoveModifiersFromSource(object source)
        {
            bool removeDone = false; 

            for (int i = statModifiers.Count - 1; i >= 0; i--) // 역순(= 가장 최근에 추가된 모드부터) 조회
            {
                if (statModifiers[i].Source == source)
                {
                    statModifiers.RemoveAt(i);
                    isDirty = true;
                    removeDone = true; // 제거된 모드가 한 개라도 있다면: true 반환
                }
            }

            return removeDone;
        }

        protected virtual int CompareModOrder(StatModifier a, StatModifier b)
        {
            if (a.Order < b.Order)
                return -1;
            
            if (a.Order > b.Order)
                return 1;

            return 0; // case if (a.Order == b.Order)
        }
        
        // protected virtual int CompareModOrder(StatModifier a, StatModifier b)
        // {
        //     // 타입 우선순위: Add(0) < PerAdd(1) < PerMul(2)
        //     int typeOrder = a.Type.CompareTo(b.Type);
        //     if (typeOrder != 0) return typeOrder;
        //     return a.Order.CompareTo(b.Order);
        // }

        protected virtual float CalculateFinalValue()
        {
            float finalValue = BaseValue;
            float sumPerAdd = 0;
            int length = statModifiers.Count;
            for (int i = 0; i < length; i++)
            {
                var mod = statModifiers[i];
                switch (mod.Type)
                {
                    case StatModCalcType.Add:
                        finalValue += mod.Value;
                        break;
                    case StatModCalcType.PerAdd:
                        sumPerAdd += mod.Value;
                        if ((i + 1 >= length) || (statModifiers[i + 1].Type != StatModCalcType.PerAdd)) // 더이상 퍼센트 합연산 모드가 없는 경우
                        {
                            finalValue *= 1 + sumPerAdd;
                            sumPerAdd = 0;
                        }
                        break;
                    case StatModCalcType.PerMul:
                        finalValue *= 1 + mod.Value;
                        break;
                    default:
                        break;
                }
            }

            return (float)Math.Round(finalValue, 4); // 소수점 다섯번째 자리에서 반올림
        }

        #region Usage example

        // // 불가능
        // statModifiers = null;
        // statModifiers = new List<StatModifier>();
        //
        // // 가능
        // statModifiers[0] = null;
        // statModifiers.Add(new StatModifier());
        
        #endregion
    }

}
