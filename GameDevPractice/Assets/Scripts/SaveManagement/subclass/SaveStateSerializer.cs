using System;
using System.Collections.Generic;
using Unity.Serialization.Json;
using UnityEngine;
using TH.Utils;

namespace TH.SaveLoad
{
    /// <summary>
    /// 세이브/로드 시스템에서 상태 직렬화/역직렬화 담당
    /// - CaptureState → JSON 직렬화 (SavableEntry 생성)
    /// - JSON → 런타임 객체 역직렬화
    /// </summary>
    public sealed class SaveStateSerializer
    {
        private readonly SaveTypeResolver typeResolver;

        public SaveStateSerializer(SaveTypeResolver typeResolver)
        {
            this.typeResolver = typeResolver;
        }

        #region Serialization (CaptureState → JSON)

        /// <summary>
        /// ISavableEntity의 CaptureState 결과를 SavableEntry 목록으로 직렬화
        /// </summary>
        public void SerializeEntity(ICollection<SavableEntry> entryCollection, ISavableEntity entity)
        {
            try
            {
                if (!entity.IsNotNull())
                {
                    Logg.LogWarning($"[{nameof(SaveStateSerializer)}] SerializeEntity - entity is destroyed");
                    return;
                }

                if (entity.CaptureState() is not { } capturedStates) return;

                if (capturedStates is Dictionary<string, object> stateDict)
                {
                    foreach (var (typeName, state) in stateDict)
                    {
                        SerializeStateEntry(entryCollection, entity.UniqueIdentifier, typeName, state);
                    }
                }
                else
                {
                    var typeName = capturedStates.GetType().AssemblyQualifiedName;
                    SerializeStateEntry(entryCollection, entity.UniqueIdentifier, typeName, capturedStates);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[{nameof(SaveStateSerializer)}] error occurred while SerializeEntity() - {e}");
            }
        }

        private void SerializeStateEntry(
            ICollection<SavableEntry> entryCollection,
            string identifier,
            string typeName,
            object state)
        {
            if (typeResolver.GetTypeByName(typeName) is not { } type) return;

            try
            {
                string json = JsonSerialization.ToJson(state, new JsonSerializationParameters
                {
                    SerializedType = type,
                });

                entryCollection.Add(new SavableEntry
                {
                    id = identifier,
                    typeName = typeName,
                    jsonPayload = json
                });
            }
            catch (Exception e)
            {
                Debug.LogError($"[{nameof(SaveStateSerializer)}] Failed to serialize {typeName}: {e.Message}");
            }
        }

        #endregion

        #region Deserialization (JSON → Runtime Object)

        /// <summary>
        /// SavableEntry 목록을 파싱하여 (identifier → (typeName → state)) 그룹으로 변환
        /// </summary>
        public void DeserializeEntries(
            List<SavableEntry> entries,
            Dictionary<string, Dictionary<string, object>> grouped)
        {
            foreach (var entry in entries)
            {
                if (typeResolver.GetTypeByName(entry.typeName) is not { } type)
                {
                    Logg.LogError($"[{nameof(SaveStateSerializer)}] Type not found: {entry.typeName}");
                    continue;
                }

                object state;
                try
                {
                    state = typeResolver.GetFromJsonMethod(type)?.Invoke(null, new object[]
                    {
                        entry.jsonPayload,
                        new JsonSerializationParameters { SerializedType = type }
                    });

                    if (state == null)
                    {
                        Logg.LogError($"[{nameof(SaveStateSerializer)}] FromJson Method missing for: {type.FullName}");
                        continue;
                    }
                }
                catch (Exception e)
                {
                    Logg.LogError($"[{nameof(SaveStateSerializer)}] Deserialize failed for {entry.typeName}: {e}");
                    continue;
                }

                if (!grouped.TryGetValue(entry.id, out var dict))
                {
                    dict = new Dictionary<string, object>();
                    grouped[entry.id] = dict;
                }

                dict[entry.typeName] = state;
            }
        }

        #endregion
    }
}
