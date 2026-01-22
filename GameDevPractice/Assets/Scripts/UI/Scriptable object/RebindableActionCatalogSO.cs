using System.Collections.Generic;
using TH.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TH.UI.Data
{
    [CreateAssetMenu(fileName = "RebindableActionCatalogSO", menuName = "Scriptable Objects/UI/RebindableActionCatalogSO")]
    public class RebindableActionCatalogSO : ScriptableObject
    {
        [SerializeField] private List<KeyRebindTarget> list;

        private IReadOnlyCollection<KeyRebindTarget> readonlyList;
        public IReadOnlyCollection<KeyRebindTarget> RebindableActions => readonlyList ??= list.AsReadOnly();

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (list == null)
                return;

            foreach (var target in list)
            {
                if (target == null || target.actionReference == null || target.actionReference.action == null)
                    continue;

                // 에디터 편집 시 액션 레퍼런스에서 메타데이터를 자동으로 채운다.
                var action = target.actionReference.action;
                target.actionName = action.name;
                target.actionMap = action.actionMap != null ? action.actionMap.name : string.Empty;

                // 첫 번째 리바인딩 가능한 바인딩으로 바인딩 ID를 채운다.
                var bindingIndex = FindFirstRebindableBindingIndex(action);
                if (bindingIndex >= 0)
                    target.bindingId = action.bindings[bindingIndex].id.ToString();
            }
        }

        private static int FindFirstRebindableBindingIndex(InputAction action)
        {
            if (action == null)
                return -1;

            for (int i = 0; i < action.bindings.Count; i++)
            {
                var binding = action.bindings[i];
                if (binding.isComposite || binding.isPartOfComposite)
                    continue;

                // 런타임 리바인딩 규칙과 맞추기 위해 버튼 계열 단일 바인딩만 허용한다.
                var expectedControlType = action.expectedControlType;
                if (!string.IsNullOrEmpty(expectedControlType) &&
                    !string.Equals(expectedControlType, "Button", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                return i;
            }

            return -1;
        }
#endif
    }
}
