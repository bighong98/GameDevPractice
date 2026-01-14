using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "OptionCategoryPanelMapSO", menuName = "Scriptable Objects/UI/Option Category Panel Map")]
public class OptionCategoryPanelMapSO : ScriptableObject
{
    [Serializable]
    public class OptionCategoryPanelEntry
    {
        public Enums.OptionCategory category;
        public string label;
        public GameObject panelPrefab;
    }

    [SerializeField] private OptionCategoryButton categoryButtonTemplate;
    [SerializeField] private List<OptionCategoryPanelEntry> entries = new();

    public OptionCategoryButton CategoryButtonTemplate => categoryButtonTemplate;
    public IReadOnlyList<OptionCategoryPanelEntry> Entries => entries;
}
