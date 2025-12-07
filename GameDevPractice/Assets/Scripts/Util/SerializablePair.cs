using System;
using UnityEngine;

namespace TH.Utils
{
    // 제네릭으로 유니티 직렬화 타입을 보장하는 것이 불가능하기에
    // 반드시 해당 구조체를 사용하는 클래스에서 직렬화 가능 여부 검사 필요 (ISerializationCallback + ValidateUnitySerializable() 사용)
    // ISerializationCallback은 SerializablePair<> 사용하는 개별 클래스에서 구현해서 ValidateUnitySerializable() 호출
    [Serializable]
    public struct SerializablePair<TKey, TValue>
    {
        public TKey key;
        public TValue value;

        public void Deconstruct(out TKey f, out TValue s)
        {
            f = key;
            s = value;
        }

#if UNITY_EDITOR
        public static void ValidateUnitySerializable()
        {
            if (!IsUnitySerializable(typeof(TKey)))
                throw new InvalidOperationException($"Invalid type for Unity serialization - {typeof(TKey)}");
            if (!IsUnitySerializable(typeof(TValue)))
                throw new InvalidOperationException($"Invalid type for Unity serialization - {typeof(TValue)}");
        }

        private static bool IsUnitySerializable(Type t)
        {
            if (typeof(UnityEngine.Object).IsAssignableFrom(t)) return true;
            if (t.IsEnum) return true;
            return (t.IsClass || t.IsValueType) && t.IsDefined(typeof(SerializableAttribute), false);
        }
#endif
        
    }
}

