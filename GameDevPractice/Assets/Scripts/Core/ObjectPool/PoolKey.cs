using UnityEngine;
using System;

namespace TH.Core.Pool
{
    public readonly struct PoolKey : IEquatable<PoolKey>
    {
        public readonly GameObject Prefab;
        public readonly Type KeyType;

        public PoolKey(GameObject prefab, Type keyType)
        {
            this.Prefab = prefab;
            this.KeyType = keyType;
        }
    
        // 구조체에 .Equals() 매서드 사용 시 발생하는 박싱 문제 방지 오버라이드 매서드
        public bool Equals(PoolKey other)
        {
            return ReferenceEquals(Prefab, other.Prefab) && KeyType == other.KeyType;
        }
    
        public override bool Equals(object obj)
        {
            return obj is PoolKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int h1 = Prefab ? Prefab.GetInstanceID() : 0;
                int h2 = KeyType?.GetHashCode() ?? 0;
                return (h1 * 397) ^ h2;
            }
        }

        public override string ToString() => $"{KeyType?.Name} @ {Prefab?.name}";
    }
}

