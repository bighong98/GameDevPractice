using System.Collections;
using System.Collections.Generic;

namespace TH.UI
{
    /// <summary>
    /// 팝업 UI를 스택 구조로 관리하는 컬렉션 클래스
    /// LIFO(Last In First Out) 방식으로 팝업을 관리
    /// null 참조 처리를 통한 안전한 삭제 메커니즘 제공
    /// IEnumerable을 구현하여 foreach 순회 지원
    /// </summary>
    
    public sealed class PopupStack : IEnumerable<PopupUI>
    {
        
        private readonly List<PopupUI> list = new();
        public int Count => list.Count;


        // 스택의 최상단에 팝업 추가
        public void Push(PopupUI popup)
        {
            if (popup == null) return;
            list.Add(popup);
        }
        
        // 스택의 최상단에서 팝업을 제거하고 반환
        // null 슬롯을 건너뛰면서 유효한 팝업을 찾음
        // 제거된 팝업 UI 반환 (스택이 비어있으면 null 반환)
        public PopupUI Pop()
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var popup = list[i];
                list.RemoveAt(i); // 슬롯 자체는 항상 제거

                if (popup != null) return popup;
            }

            return null;
        }

        
        // 스택의 최상단 팝업을 제거하지 않고 반환
        // 최상단 팝업 UI 반환 (빈 스택이면 null)
        
        public PopupUI Peek()
        {
            if (!TryPeek(out var popup)) return null;
            return popup;
        }

        // 스택이 비어있는지 확인 + 최상단 팝업 확인
        // null 슬롯을 건너뛰면서 유효한 팝업을 찾음
        // 유효한 팝업을 찾았으면 true, 없으면 false 반환
        // popup: 최상단에 위치한 유효 팝업 인스턴스
        public bool TryPeek(out PopupUI popup)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var p = list[i];
                if (p == null) 
                    continue;

                popup = p;
                return true;
            }

            popup = null;
            return false;
        }


        /// 스택의 모든 팝업을 제거
        public void Clear() => list.Clear();

                
        /// 특정 팝업이 스택에 포함되어 있는지 확인
        public bool Contains(PopupUI popup)
        {
            if (popup == null) return false;
            return list.Contains(popup);
        }

        // 특정 팝업을 스택에서 제거
        // 최상단이면 슬롯 제거, 중간이면 null로 마킹 (cutTail: false -> 슬롯 유지)
        // cutTail: true -> 제거 위치 상단의 null 슬롯들 정리
        public bool Remove(PopupUI popup, bool cutTail = false)
        {
            if (popup == null || list.Count == 0)
                return false;

            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (!ReferenceEquals(list[i], popup))
                    continue;

                // 최상단이면 슬롯 제거, 중간이면 null로 마킹하여 순서 유지
                if (i == list.Count - 1) list.RemoveAt(i);
                else list[i] = null;

                if (cutTail) CutTails(i);

                return true;
            }

            return false;
        }

        
        // 지정된 인덱스 이후 연속된 null 슬롯들을 제거
        // 스택 끝에서부터 startIndex까지 역방향으로 순회하며 null 슬롯 정리
        private void CutTails(int startIndex)
        {
            int current = list.Count - 1;
            while (current > startIndex && list[current] == null)
            {
                list.RemoveAt(current);
                current--;
            }
        }
        
        #region IEnumerable
       
        public IEnumerator<PopupUI> GetEnumerator()
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var popup = list[i];
                if (popup == null) 
                    continue;

                yield return popup;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion
    }
}

