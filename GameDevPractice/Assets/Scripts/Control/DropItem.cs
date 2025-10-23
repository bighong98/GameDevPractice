using System;
using TH.Control;
using TH.Utils;
using UnityEngine;


namespace TH.Control
{
    [RequireComponent(typeof(ItemTypeHolder))]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class DropItem : MonoBehaviour, IDropItem
    {
        public ItemTypeSO ItemData => holder.Type;
        public bool UseImmediately => useImmediately;
        public int Amount => amount;
        public Transform Trs => transform;

        [SerializeField] private bool useImmediately;
        [SerializeField] private int amount = 1;
        private ItemTypeHolder holder;

        private void Awake()
        {
            if (!TryGetComponent(out holder))
            {
                Logg.LogError($"[{gameObject.name}] DropItem doesn't have valid ItemTypeHolder");
                Destroy(gameObject);
                return;
            }
        }

        public void Interact()
        {
            holder.ReleaseSelf();
        }
    }

}
