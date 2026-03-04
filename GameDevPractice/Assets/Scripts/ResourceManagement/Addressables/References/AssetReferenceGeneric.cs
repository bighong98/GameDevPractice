using UnityEngine;
using UnityEngine.AddressableAssets;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
#endif

namespace TH.Resource
{
    public class AssetReferenceGeneric<TObject> : AssetReference where TObject : UnityEngine.Object
    {
#if UNITY_EDITOR
        public AssetReferenceGeneric() { }

        public AssetReferenceGeneric(TObject obj)
            : base(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(obj)))
        {
        }

        public override bool ValidateAsset(string path)
        {
            return ValidateAsset(AssetDatabase.LoadAssetAtPath<TObject>(path));
        }

        public override bool ValidateAsset(UnityEngine.Object obj)
        {
            return obj is TObject;
        }

        public static bool TryCreateEditorReference<TReference>(TObject source, out TReference createdReference)
            where TReference : AssetReferenceGeneric<TObject>, new()
        {
            createdReference = null;
            if (source == null)
            {
                return false;
            }

            var assetPath = AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return false;
            }

            var assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrWhiteSpace(assetGuid))
            {
                return false;
            }

            EnsureAddressableEntry(assetGuid);

            var assetReference = new TReference();
            if (!assetReference.SetEditorAsset(source))
            {
                return false;
            }

            createdReference = assetReference;
            return true;
        }

        private static void EnsureAddressableEntry(string assetGuid)
        {
            if (string.IsNullOrWhiteSpace(assetGuid))
            {
                return;
            }

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                return;
            }

            if (settings.FindAssetEntry(assetGuid) != null)
            {
                return;
            }

            var defaultGroup = settings.DefaultGroup;
            if (defaultGroup == null)
            {
                return;
            }

            settings.CreateOrMoveEntry(assetGuid, defaultGroup, false, true);
        }

        // 추가 serialized field가 필요할 경우 SetEditorAsset() 아래와 같이 오버라이드해서 사용
        /*
        public override bool SetEditorAsset(UnityEngine.Object obj)
        {
            if (!base.SetEditorAsset(obj))
            {
                return false;
            }

            if (obj is TObject tObj)
            {
                //todo: serialized field 초기화
                return true;
            }
            else
            {
                //todo: default value 적용
                return false;
            }
        }
        */
#endif

    }
}

