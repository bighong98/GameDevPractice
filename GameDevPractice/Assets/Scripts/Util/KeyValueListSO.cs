using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TH.Utils 
{
    // SerializablePair<TKey, TValue> 리스트를 바탕으로
    // 내부 딕셔너리 캐싱 + 외부 읽기 전용 컬렉션, 키(<TKey>) 조회를 제공하는 
    // 제네릭 추상 ScriptableObject 클래스 
    
    public abstract class KeyValueListSO<TKey, TValue> : ScriptableObject
#if UNITY_EDITOR
        , ISerializationCallbackReceiver
#endif
    {
        // 직렬화된 SerializablePair<> 기반 원본 데이터 목록
        [SerializeField] private List<SerializablePair<TKey, TValue>> list = new();
        // 파생 클래스의 원본 데이터 리스트 접근용 프로퍼티
        protected List<SerializablePair<TKey, TValue>> RawList => list;
        // 외부에 노출 가능한 읽기 전용 컬렉션
        private IReadOnlyCollection<SerializablePair<TKey, TValue>> _readOnly;
        public IReadOnlyCollection<SerializablePair<TKey, TValue>> Items
            => _readOnly ??= list.AsReadOnly();

        // 런타임 조회용 캐시
        private readonly Dictionary<TKey, TValue> _dict = new();
        private bool _initialized;

        // 내부 딕셔너리 초기화 (외부 키 기반 조회 대응 목적)
        protected void ForceInitDict()
        {
            _dict.Clear();
            foreach (var (key, value) in list)
            {
                // 중복 키가 존재할 경우 리스트 순서상 마지막 값으로 덮어씀
                _dict[key] = value;
            }

            _initialized = true;
        }

        private void EnsureInitialized()
        {
            if (_initialized)
                return;

            ForceInitDict();
        }

        // TKey 타입 키 기반 TValue 조회
        public bool TryGetValue(TKey key, out TValue value)
        {
            EnsureInitialized();
            return _dict.TryGetValue(key, out value);
        }

        #region IEnumerable

        public IEnumerable<TKey> Keys { get { EnsureInitialized(); return _dict.Keys; } }
        public IEnumerable<TValue> Values { get { EnsureInitialized(); return _dict.Values; } }

        #endregion
        

#if UNITY_EDITOR
        // 유니티 기준 직렬화 가능한 타입인지 에디터에서 강제 검사
        // 직렬화 불가능할 경우 예외 발생
        public void OnBeforeSerialize()
        {
            SerializablePair<TKey, TValue>.ValidateUnitySerializable();
        }

        public void OnAfterDeserialize()
        {
            _initialized = false;
        }
#endif
        
    }
}