#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class OutfitMeshSwapVerifier
{
    private const string PrefabPath = "Assets/Game/Characters/NPC/Enemy/Character Prefab/Enemy_fighter_mm.prefab";

    [MenuItem("Tools/Outfit/Verify Mesh Swap Compatibility (Enemy_fighter_mm)")]
    private static void Verify()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var fighterTop = FindSkinned(root.transform, "BasicHero_M_Fighter/M_Fighter/M_Fighter_Top");
            var knightTop = FindSkinned(root.transform, "BasicHero_M_Fighter/M_Knight/M_Knight_Top");

            var fighterBottom = FindSkinned(root.transform, "BasicHero_M_Fighter/M_Fighter/M_Fighter_Bottom");
            var knightBottom = FindSkinned(root.transform, "BasicHero_M_Fighter/M_Knight/M_Knight_Bottom");

            var fighterHeadband = FindSkinned(root.transform, "BasicHero_M_Fighter/M_Fighter/M_Fighter_Headband");
            var knightHelm = FindSkinned(root.transform, "BasicHero_M_Fighter/M_Knight/M_Knight_GreatHelm");

            Debug.Log("[OutfitMeshSwapVerifier] START");
            Compare("Top", fighterTop, knightTop);
            Compare("Bottom", fighterBottom, knightBottom);
            Compare("Head", fighterHeadband, knightHelm);
            Debug.Log("[OutfitMeshSwapVerifier] END");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static SkinnedMeshRenderer FindSkinned(Transform root, string path)
    {
        var t = root.Find(path);
        if (t == null)
        {
            Debug.LogError($"[OutfitMeshSwapVerifier] Missing transform: {path}");
            return null;
        }

        if (!t.TryGetComponent(out SkinnedMeshRenderer smr) || smr == null)
        {
            Debug.LogError($"[OutfitMeshSwapVerifier] Missing SkinnedMeshRenderer: {path}");
            return null;
        }

        return smr;
    }

    private static void Compare(string label, SkinnedMeshRenderer a, SkinnedMeshRenderer b)
    {
        if (a == null || b == null)
        {
            Debug.LogError($"[OutfitMeshSwapVerifier] {label}: missing renderer(s)");
            return;
        }

        Debug.Log($"[OutfitMeshSwapVerifier] {label}: A={a.name}, B={b.name}");

        DumpRenderer("A", a);
        DumpRenderer("B", b);

        bool sameRoot = (a.rootBone != null && b.rootBone != null && a.rootBone.name == b.rootBone.name);
        bool sameBones = HaveSameBoneSignature(a, b);

        Debug.Log($"[OutfitMeshSwapVerifier] {label}: sameRootBoneName={sameRoot}, sameBonesSignature={sameBones}");

        // Critical check for 'sharedMesh-only swap': renderer keeps its bones array.
        // This is only safe if the incoming mesh expects the same bone count/order.
        bool aCanAcceptB = CanAssignMeshWithoutBoneChange(a, b.sharedMesh);
        bool bCanAcceptA = CanAssignMeshWithoutBoneChange(b, a.sharedMesh);
        Debug.Log($"[OutfitMeshSwapVerifier] {label}: A<=B(sharedMesh only)={aCanAcceptB}, B<=A(sharedMesh only)={bCanAcceptA}");
    }

    private static void DumpRenderer(string prefix, SkinnedMeshRenderer r)
    {
        var mesh = r.sharedMesh;
        int bindposes = mesh != null ? mesh.bindposes.Length : -1;
        int vertices = mesh != null ? mesh.vertexCount : -1;
        int subMeshes = mesh != null ? mesh.subMeshCount : -1;

        Debug.Log(
            $"[OutfitMeshSwapVerifier] {prefix}: mesh={(mesh != null ? mesh.name : "<null>")}, " +
            $"meshId={(mesh != null ? mesh.GetInstanceID() : 0)}, bindposes={bindposes}, " +
            $"vertices={vertices}, subMeshes={subMeshes}, bones={r.bones?.Length ?? 0}, " +
            $"rootBone={(r.rootBone != null ? r.rootBone.name : "<null>")}, materials={r.sharedMaterials?.Length ?? 0}");
    }

    private static bool HaveSameBoneSignature(SkinnedMeshRenderer a, SkinnedMeshRenderer b)
    {
        if (a.bones == null || b.bones == null) return false;
        if (a.bones.Length != b.bones.Length) return false;

        // Compare bone name order (instance IDs can differ in prefab contents).
        for (int i = 0; i < a.bones.Length; i++)
        {
            var an = a.bones[i] != null ? a.bones[i].name : string.Empty;
            var bn = b.bones[i] != null ? b.bones[i].name : string.Empty;
            if (!string.Equals(an, bn, StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private static bool CanAssignMeshWithoutBoneChange(SkinnedMeshRenderer target, Mesh incoming)
    {
        if (target == null || incoming == null) return false;
        if (target.bones == null) return false;

        // Unity requires bindposes length to match bones length when skinning.
        if (incoming.bindposes == null || incoming.bindposes.Length != target.bones.Length)
            return false;

        var original = target.sharedMesh;
        try
        {
            target.sharedMesh = incoming;
            return target.sharedMesh == incoming;
        }
        catch (Exception e)
        {
            Debug.LogError($"[OutfitMeshSwapVerifier] Exception while assigning mesh: {e}");
            return false;
        }
        finally
        {
            target.sharedMesh = original;
        }
    }
}
#endif
