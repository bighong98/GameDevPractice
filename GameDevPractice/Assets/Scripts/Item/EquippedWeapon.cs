using System;
using System.Collections.Generic;
using UnityEngine;
using TH.Resource;

namespace TH.Item
{
    public interface IAnchorProvider
    {
        bool TryGetAnchor(string anchorName, out Transform anchor);
    }

    public class EquippedWeapon : WeaponTypeHolder, IAnchorProvider
    {
        [SerializeField] private Transform handle;
        [SerializeField] private Transform model;

        private readonly Dictionary<string, Transform> anchorCache = new(StringComparer.Ordinal);
        private readonly HashSet<string> missingAnchorCache = new(StringComparer.Ordinal);

        public Transform Handle => handle;
        public Transform Model => model;

        public bool TryGetAnchor(string anchorName, out Transform anchor)
        {
            anchor = null;
            if (string.IsNullOrWhiteSpace(anchorName))
            {
                return false;
            }

            if (anchorCache.TryGetValue(anchorName, out anchor))
            {
                return anchor != null;
            }

            if (missingAnchorCache.Contains(anchorName))
            {
                return false;
            }

            if (TryFindAnchorRecursive(transform, anchorName, out anchor))
            {
                anchorCache[anchorName] = anchor;
                return true;
            }

            missingAnchorCache.Add(anchorName);
            return false;
        }

        private static bool TryFindAnchorRecursive(Transform root, string anchorName, out Transform found)
        {
            found = null;
            if (root == null)
            {
                return false;
            }

            if (string.Equals(root.name, anchorName, StringComparison.Ordinal))
            {
                found = root;
                return true;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                if (TryFindAnchorRecursive(root.GetChild(i), anchorName, out found))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

