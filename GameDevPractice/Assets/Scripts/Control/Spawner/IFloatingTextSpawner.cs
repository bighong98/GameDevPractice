using System.Collections.Generic;
using UnityEngine;

namespace TH.Utils
{
    public interface IFloatingTextSpawner
    {
        void Register(object source, FloatingTextEventType eventType);
        void UnRegister(object source, FloatingTextEventType eventType);

        void SpawnBatch(
            FloatingTextEventType eventType,
            Transform anchor,
            IReadOnlyList<string> values,
            FloatingTextBatchLayout layout = FloatingTextBatchLayout.Line);
        void SpawnBatch(
            FloatingTextEventType eventType,
            Transform anchor,
            IReadOnlyCollection<float> values,
            FloatingTextBatchLayout layout = FloatingTextBatchLayout.Line);
    }

    public enum FloatingTextBatchLayout
    {
        Spread,
        Line,
    }

    public enum FloatingTextEventType
    {
        Damage,
        Heal,
        GetXp,
    }
}

