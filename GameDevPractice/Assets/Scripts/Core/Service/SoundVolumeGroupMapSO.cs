using System;
using System.Collections.Generic;
using UnityEngine;

namespace TH.Core.Service
{
    [CreateAssetMenu(fileName = "SoundVolumeGroupMapSO", menuName = "Scriptable Objects/Audio/Sound Volume Group Map")]
    public class SoundVolumeGroupMapSO : ScriptableObject
    {
        [Serializable]
        public class SoundGroupEntry
        {
            public Enums.VolumeGroup group;
            public string label;
            public List<Enums.AudioType> audioTypes = new();
        }

        [SerializeField] private List<SoundGroupEntry> entries = new();

        public IReadOnlyList<SoundGroupEntry> Entries => entries;
    }
}
