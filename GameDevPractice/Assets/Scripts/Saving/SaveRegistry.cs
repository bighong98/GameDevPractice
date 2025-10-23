using System.Collections.Generic;
using RPG.Saving;
using UnityEngine;

namespace TH.SaveLoad
{
    public class SaveRegistry : ISaveRegistry
    {
        private static readonly Dictionary<string, object> Registry = new(); // Non-MB 클래스 데이터
        private static readonly Dictionary<string, ISavableWithId> Registers = new(); // Non-MB 클래스
        
        public void Register(ISavableWithId savable)
        {
            throw new System.NotImplementedException();
        }

        public void UnRegister(ISavableWithId savable)
        {
            throw new System.NotImplementedException();
        }
    }
}

