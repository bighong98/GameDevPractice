using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace TH.Resource
{
    public class AssetReferenceGeneric<TObject>: AssetReference where TObject : UnityEngine.Object
    {
#if UNITY_EDITOR
        public AssetReferenceGeneric() {}
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

