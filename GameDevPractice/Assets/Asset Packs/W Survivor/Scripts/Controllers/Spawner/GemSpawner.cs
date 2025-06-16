// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
//
// public class GemSpawner
// {
//     public int maxGemNum = 500;
//
//     private int _poolIndex;
//     public Transform gemContainer;
//     
//     private string baseGemKey;
//     // GemSprites is not used yet
//     //public List<SpriteRenderer> GemSprites;
//     
//     // Init() is called by InGameManager
//     public bool Init()
//     {
//         Transform dropContainer = InGameManager.Instance.DropContainer;
//         if (dropContainer == null)
//         {
//             Debug.Log("failed to find drop container");
//         }
//             
//         gemContainer = dropContainer.Find("Gem");
//         if (gemContainer == null)
//         {
//             GameObject go = new GameObject() { name = "Gem" };
//             gemContainer = go.transform;
//             gemContainer.SetParent(dropContainer);
//         }
//             
//         // todo: manually add prefab key => get from table
//         
//         baseGemKey = "ExpGem0.prefab";
//         GameObject prefab = (Managers.Resource.Load<Object>(baseGemKey) as GameObject);
//         
//         if (prefab == null)
//             return false;
//         
//         GemController gem = prefab.GetComponent<GemController>();
//         Managers.Pool.AddPool<GemController>(gem, maxGemNum, gemContainer, out _poolIndex, false);
//
//         return true;
//     }
//
//     // Get gem from pool and move locate it on scene
//     // default: add gem to grid dictionary
//     // todo: spawn gems by type
//     public GemController GemSpawn(Vector3 pos, bool grid = true)
//     {
//         GemController gem = Managers.Pool.GetFromPool<GemController>(_poolIndex, pos);
//         if (gem == null)
//             return null;
//         
//         if (grid)
//         {
//             Managers.Grid.AddToCell<GemController>(gem);
//         }
//         
//         return gem;
//     }
//
//     // release gem and remove it from scene
//     // default: not remove gem from grid dictionary (gem collector do it)
//     public void GemDespawn(GemController gemClone, bool grid = false)
//     {
//         if (grid)
//         {
//             Managers.Grid.RemoveFromCell<GemController>(gemClone);
//         }
//         Managers.Pool.ReturnPool<GemController>(_poolIndex, gemClone);
//     }
//     
//     // todo: make methods that change sprite and exp amount by type
// }
