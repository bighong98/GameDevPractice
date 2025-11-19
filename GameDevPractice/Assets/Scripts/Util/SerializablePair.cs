using System;
using UnityEngine;

namespace TH.Utils
{
    // 제네릭으로 유니티 직렬화 타입을 보장하는 것이 불가능하기에
    // 반드시 해당 구조체를 사용하는 클래스에서 직렬화 가능 여부 검사 필요 (ISerializationCallback + ValidateUnitySerializable() 사용)
    // ISerializationCallback은 SerializablePair<> 사용하는 개별 클래스에서 구현해서 ValidateUnitySerializable() 호출
    [Serializable]
    public struct SerializablePair<TFirst, TSecond>
    {
        public TFirst first;
        public TSecond second;

        public void Deconstruct(out TFirst f, out TSecond s)
        {
            f = first;
            s = second;
        }

#if UNITY_EDITOR
        public static void ValidateUnitySerializable()
        {
            if (!IsUnitySerializable(typeof(TFirst)))
                throw new InvalidOperationException($"Invalid type for Unity serialization - {typeof(TFirst)}");
            if (!IsUnitySerializable(typeof(TSecond)))
                throw new InvalidOperationException($"Invalid type for Unity serialization - {typeof(TSecond)}");
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

