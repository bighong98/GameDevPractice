// using System;
// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
// using UnityEngine.UI;
//
// // skill upgrade popup script when player is level up
// public class UI_SkillSelectPopup : UI_Popup
// {
//     private SkillManager _skill;
//     
//     #region enums
//
//     enum GameObjects
//     {
//         UI_SkillChoicePopup0,
//         UI_SkillChoicePopup1,
//         UI_SkillChoicePopup2,
//     }
//     
//     #endregion
//     
//     public override bool Init()
//     {
//         if (base.Init() == false)
//             return false;
//         
//         _skill = InGameManager.Instance.playerSkillManager;
//         _escapable = false; // ESC로 팝업 종료 불가능
//         
//         #region Objects Bind
//
//         BindObject(typeof(GameObjects));
//
//         #endregion
//         
//         return true;
//     }
//
//     public void ShowChoicePopups(SkillChoiceInfo[] infos)
//     {
//         if (infos.Length < 1)
//             return;
//         
//         for (int i = 0; i < infos.Length; i++)
//         {
//             int idx;
//             switch (i)
//             {
//                 case 0:
//                     idx = (int)GameObjects.UI_SkillChoicePopup0;
//                     break;
//                 case 1:
//                     idx = (int)GameObjects.UI_SkillChoicePopup1;
//                     break;
//                 case 2:
//                     idx = (int)GameObjects.UI_SkillChoicePopup2;
//                     break;
//                 default:
//                     idx = -1;
//                     break;
//             }
//             
//             UI_SkillChoicePopup choice = GetObject(idx).GetOrAddComponent<UI_SkillChoicePopup>();
//             choice.SkillInfo = infos[i];
//             choice.ShowChoice(this);
//         }
//         
//     }
//
//     // buggy. fix required
//     public void DoSkillGacha()
//     {
//         Init();
//         
//         int[] resultNums = {-1, -1, -1};
//         SkillChoiceInfo[] resultInfos = new SkillChoiceInfo[3];
//         List<int> excludes = new List<int>();
//         int total = 0;
//
//         int activeNum = _skill.currSkillNum < Constants.activeSkill_MaxSlot ? _skill.playerSkillData.groupIds.Count : _skill.currSkillNum;
//         int passiveNum = _skill.currSkillNum < Constants.passiveSkill_MaxSlot ? _skill.playerPassiveData.groupIds.Count : _skill.currPassiveNum;
//
//         total += activeNum;
//         total += passiveNum;
//         
//         foreach (ISkill skill in _skill.skillSlots)
//         {
//             // exclude player's skill which is already max level
//             if (skill.Level >= Constants.activeSkill_MaxLevel)
//                 excludes.Add(_skill.playerSkillData.groupIds.IndexOf(skill.GroupId));
//             // record player's skill which is not max level yet
//         }
//         
//         // todo: exclude max level passives 
//         
//         resultNums = Util.GetRandomNums(total, 3, excludes.ToArray());
//
//         for (int i = 0; i < resultNums.Length; i++)
//         {
//             if (resultNums[i] == -1)
//                 continue;
//
//             if (resultNums[i] < _skill.playerSkillData.groupIds.Count)
//                 resultInfos[i] = GetActiveInfo(_skill.playerSkillData.groupIds[resultNums[i]]);
//             else
//                 resultInfos[i] = GetPassiveInfo(_skill.playerPassiveData.groupIds[resultNums[i] - _skill.playerSkillData.groupIds.Count]);
//         }
//         
//         ShowChoicePopups(resultInfos);
//     }
//
//     public SkillChoiceInfo GetActiveInfo(int id)
//     {
//         foreach (ISkill skill in _skill.skillSlots)
//         {
//             // 'dataId + 1' means next level dataId of the skill
//             if (skill.GroupId == id)
//                 id = skill.DataId + 1;
//         }
//         SkillStat stat = _skill.playerSkillData.GetSkillStat(id);
//         return new SkillChoiceInfo(Enums.UpgradeType.Active, stat.groupId, stat.name, stat.level, stat.desc);
//     }
//     // buggy
//     public SkillChoiceInfo GetPassiveInfo(int id)
//     {
//         //todo: check if player has same passive (of which groupId is same)
//         foreach (IPassive passive in _skill.passiveSlots)
//         {
//             if (passive.GroupId == id)
//                 id = passive.DataId + 1;
//         }
//
//         PassiveInfo passiveInfo = _skill.playerPassiveData.GetPassiveInfo(id);
//         Debug.Log(passiveInfo.name);
//         return new SkillChoiceInfo(Enums.UpgradeType.Passive, passiveInfo.groupId, passiveInfo.name, passiveInfo.level, passiveInfo.desc);
//     }
// }
//
// [Serializable]
// public class SkillChoiceInfo
// {
//     public Image iconImage;
//     public Enums.UpgradeType type;
//     public int groupId;
//     public string name;
//     public int nextLevel;
//     public string choiceDesc;
//     
//     public SkillChoiceInfo(Enums.UpgradeType type, int groupId, string name, int nextLevel, string choiceDesc)
//     {
//         this.type = type;
//         this.groupId = groupId;
//         this.name = name;
//         this.nextLevel = nextLevel;
//         this.choiceDesc = choiceDesc;
//     }
// }