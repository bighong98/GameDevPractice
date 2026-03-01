using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Serialization.Json;
using TH.Utils;

namespace TH.SaveLoad
{
    /// <summary>
    /// 세이브/로드 시스템에서 사용하는 타입 해석 및 리플렉션 캐싱 담당
    /// - 타입명 → Type 변환 (캐싱)
    /// - Type → JsonSerialization.FromJson MethodInfo 변환 (캐싱)
    /// </summary>
    public sealed class SaveTypeResolver
    {
        private readonly Dictionary<Type, MethodInfo> cachedMethodInfos = new();
        private readonly Dictionary<string, Type> cachedTypes = new();

        private static readonly Dictionary<string, Type> BuiltinAliasTypes = new(StringComparer.Ordinal)
        {
            ["bool"] = typeof(bool),
            ["byte"] = typeof(byte),
            ["sbyte"] = typeof(sbyte),
            ["char"] = typeof(char),
            ["decimal"] = typeof(decimal),
            ["double"] = typeof(double),
            ["float"] = typeof(float),
            ["int"] = typeof(int),
            ["uint"] = typeof(uint),
            ["long"] = typeof(long),
            ["ulong"] = typeof(ulong),
            ["short"] = typeof(short),
            ["ushort"] = typeof(ushort),
            ["string"] = typeof(string),
        };

        private static readonly MethodInfo FromJsonOpenGeneric =
            typeof(JsonSerialization).GetMethod(
                "FromJson",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(string), typeof(JsonSerializationParameters) },
                null
            );

        /// <summary>
        /// 타입명으로 Type 조회 (캐싱 적용)
        /// </summary>
        public Type GetTypeByName(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return null;

            if (cachedTypes.TryGetValue(typeName, out var t)) return t;

            if (BuiltinAliasTypes.TryGetValue(typeName, out var aliasType))
            {
                cachedTypes[typeName] = aliasType;
                return aliasType;
            }

            var type = GetTypeFromAssembly(typeName);
            cachedTypes[typeName] = type;
            return type;
        }

        /// <summary>
        /// Type에 대응하는 JsonSerialization.FromJson 제네릭 메서드 조회 (캐싱 적용)
        /// </summary>
        public MethodInfo GetFromJsonMethod(Type type)
        {
            if (type == null) return null;

            if (cachedMethodInfos.TryGetValue(type, out var result))
            {
                return result;
            }

            try
            {
                var method = FromJsonOpenGeneric.MakeGenericMethod(type);
                cachedMethodInfos[type] = method;
                return method;
            }
            catch (Exception e)
            {
                Logg.LogError($"[{nameof(SaveTypeResolver)}] MakeGenericMethod failed: {type.FullName}, {e.Message}");
                return null;
            }
        }

        private static Type GetTypeFromAssembly(string typeName)
        {
            var type = Type.GetType(typeName);
            if (type != null) return type;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(typeName);
                if (type != null) break;
            }

            if (type == null)
            {
                Logg.LogWarning($"[{nameof(SaveTypeResolver)}] GetTypeFromAssembly({typeName}) result is null");
            }

            return type;
        }
    }
}
