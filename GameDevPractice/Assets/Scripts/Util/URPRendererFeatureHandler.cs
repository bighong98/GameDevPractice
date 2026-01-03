using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Reflection;
using System.Collections.Generic;

namespace TH.Utils
{
    public static class URPRendererFeatureHandler
    {
        // --- 캐싱 데이터 ---
        private static UniversalRenderPipelineAsset _cachedPipelineAsset;
        private static UniversalRendererData _cachedRendererData;
        
        // [핵심 변경 1] Feature 캐시의 키를 int(Hash)로 변경
        private static readonly Dictionary<int, ScriptableRendererFeature> FeatureCache = new Dictionary<int, ScriptableRendererFeature>();

        // [핵심 변경 2] 문자열 -> 해시 변환 캐시 (중복 해시 계산 방지)
        private static readonly Dictionary<string, int> NameHashCache = new Dictionary<string, int>();

        // 리플렉션 필드 정보
        private static FieldInfo _rendererDataListField;
        private static FieldInfo _defaultRendererIndexField;
        private static bool _reflectionInitialized = false;

        /// <summary>
        /// Feature 이름을 고유한 해시값(int)으로 변환합니다.
        /// 내부적으로 캐싱을 수행하므로, 동일한 문자열에 대해 빠르게 반환됩니다.
        /// </summary>
        public static int StringToHash(string name)
        {
            if (string.IsNullOrEmpty(name)) return 0;

            // 이미 캐싱된 해시가 있다면 즉시 반환 (Dictionary TryGetValue는 매우 빠름)
            if (NameHashCache.TryGetValue(name, out int cachedHash))
            {
                return cachedHash;
            }

            // 캐싱된 게 없다면 새로 계산 (임시로 Animator.StringToHash 빌려서 사용)
            int newHash = Animator.StringToHash(name);
            NameHashCache[name] = newHash;
            
            return newHash;
        }

        private static void RefreshCacheIfNeeded()
        {
            var currentPipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

            // 파이프라인 변경 확인
            if (currentPipeline == null || (_cachedPipelineAsset == currentPipeline && _cachedRendererData != null))
            {
                return;
            }

            _cachedPipelineAsset = currentPipeline;
            FeatureCache.Clear();
            // 참고: _nameHashCache는 파이프라인이 바뀌어도 이름<->해시 매핑이 불변이므로 비울 필요 없음

            // 리플렉션 초기화
            if (!_reflectionInitialized)
            {
                var assetType = typeof(UniversalRenderPipelineAsset);
                _rendererDataListField = assetType.GetField("m_RendererDataList", BindingFlags.Instance | BindingFlags.NonPublic);
                _defaultRendererIndexField = assetType.GetField("m_DefaultRendererIndex", BindingFlags.Instance | BindingFlags.NonPublic);
                _reflectionInitialized = true;
            }

            // Renderer Data 획득
            var rendererDataList = (ScriptableRendererData[])_rendererDataListField?.GetValue(currentPipeline);
            if (rendererDataList == null || rendererDataList.Length == 0)
            {
                _cachedRendererData = null;
                return;
            }

            int activeIndex = 0;
            if (_defaultRendererIndexField != null)
            {
                activeIndex = (int)_defaultRendererIndexField.GetValue(currentPipeline);
            }
            
            if (activeIndex < 0 || activeIndex >= rendererDataList.Length) activeIndex = 0;
            
            _cachedRendererData = rendererDataList[activeIndex] as UniversalRendererData;
            if (_cachedRendererData == null) return;
            
            // Feature 딕셔너리 구축 (이름 대신 해시를 Key로 사용)
            foreach (var feature in _cachedRendererData.rendererFeatures)
            {
                if (feature == null) continue;
                    
                // Feature의 이름을 해시로 변환하여 키로 등록
                int featureHash = StringToHash(feature.name);
                    
                if (!FeatureCache.ContainsKey(featureHash))
                {
                    FeatureCache.Add(featureHash, feature);
                }
            }
        }

        // ==================================================================================
        // 1. INT (Hash) 기반 메서드 (최적화된 버전 - Update 등에서 권장)
        // ==================================================================================

        /// <summary>
        /// (최적화됨) 해시값을 사용하여 Feature를 찾습니다.
        /// </summary>
        public static bool GetRendererFeature<T>(int featureHash, out T feature) where T : ScriptableRendererFeature
        {
            feature = null;
            RefreshCacheIfNeeded();

            if (!FeatureCache.TryGetValue(featureHash, out var baseFeature)) return false;
            if (baseFeature is not T typedFeature) return false;
            
            feature = typedFeature;
            return true;
        }

        /// <summary>
        /// (최적화됨) 해시값을 사용하여 Feature 활성 상태를 변경합니다.
        /// </summary>
        public static bool SetRendererFeatureActive<T>(int featureHash, bool isActive) where T : ScriptableRendererFeature
        {
            // 내부적으로 RefreshCacheIfNeeded 호출됨
            if (!GetRendererFeature<T>(featureHash, out var feature)) return false;
            if (feature.isActive == isActive) return true;
            
            feature.SetActive(isActive);
            if (_cachedRendererData != null) _cachedRendererData.SetDirty();
            return true;
        }

        // ==================================================================================
        // 2. STRING 기반 메서드 (편의성 버전 - 내부적으로 해시 변환 후 호출)
        // ==================================================================================

        public static bool GetRendererFeature<T>(string featureName, out T feature) where T : ScriptableRendererFeature
        {
            // 문자열 -> 해시 변환 후 int 버전 호출
            int hash = StringToHash(featureName);
            return GetRendererFeature(hash, out feature);
        }

        public static bool SetRendererFeatureActive<T>(string featureName, bool isActive) where T : ScriptableRendererFeature
        {
            int hash = StringToHash(featureName);
            return SetRendererFeatureActive<T>(hash, isActive);
        }

        /// <summary>
        /// 강제 캐시 초기화
        /// </summary>
        public static void InvalidateCache()
        {
            _cachedPipelineAsset = null;
            _cachedRendererData = null;
            FeatureCache.Clear();
            // _nameHashCache는 유지해도 무방함 (이름-해시 매핑은 영구적)
        }
    }
}
