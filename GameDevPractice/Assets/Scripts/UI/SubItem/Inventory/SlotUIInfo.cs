using System;
using System.Collections.Generic;
using UnityEngine;

namespace TH.UI
{
    public struct SlotUIInfo<T> : IEquatable<SlotUIInfo<T>> where T : class
    {
        public T Source;
        public int Index;
        private const int UnInitializedIndexValue = -1;

        public void Clear()
        {
            Source = null;
            Index = UnInitializedIndexValue;
        }
        public bool IsValid()
        {
            return Source != null && Index != UnInitializedIndexValue;
        }

        public bool Equals(SlotUIInfo<T> other)
        {
            return EqualityComparer<T>.Default.Equals(Source, other.Source) && Index == other.Index;
        }

        public override bool Equals(object obj)
        {
            return obj is SlotUIInfo<T> other && Equals(other);
        }

        public bool Equals(T source, int index)
        {
            return EqualityComparer<T>.Default.Equals(Source, source) && Index == index;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Source, Index);
        }

        public static bool operator ==(SlotUIInfo<T> left, SlotUIInfo<T> right)
        {
            return left.Equals(right);
        }
            
        public static bool operator !=(SlotUIInfo<T> left, SlotUIInfo<T> right)
        {
            return !(left == right);
        }
    }
}

