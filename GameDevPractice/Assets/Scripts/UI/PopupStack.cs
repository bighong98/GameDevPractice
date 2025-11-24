using System.Collections;
using System.Collections.Generic;
using Unity.AppUI.UI;
using UnityEngine;

namespace TH.UI
{
    public sealed class PopupStack : IEnumerable<PopupUI>
    {
        private readonly List<PopupUI> list = new();

        public int Count => list.Count;

        public void Push(PopupUI popup)
        {
            if (popup == null) return;
            list.Add(popup);
        }

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

        public PopupUI Peek()
        {
            if (!TryPeek(out var popup)) return null;
            return popup;
        }

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

        public void Clear() => list.Clear();

        public bool Contains(PopupUI popup)
        {
            if (popup == null) return false;
            return list.Contains(popup);
        }

        public bool Remove(PopupUI popup, bool cutTail = false)
        {
            if (popup == null || list.Count == 0)
                return false;

            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (!ReferenceEquals(list[i], popup))
                    continue;

                if (i == list.Count - 1) list.RemoveAt(i);
                else list[i] = null;

                if (cutTail) CutTails(i);

                return true;
            }

            return false;
        }

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

