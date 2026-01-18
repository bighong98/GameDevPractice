using TH.Core.Service;
using TH.Utils;
using Unity.Cinemachine;
using UnityEngine;

// 현재 게임 오브젝트에 부착된 CinemachineBrain 컴포넌트를 서비스(ICameraHolder)에 전달하는 역할 수행
// CinemachineBrain 컴포넌트가 있는 오브젝트에만 부착되어 있어야함
namespace TH.Cinematic.Service 
{
    public class CinemachineBrainRegister : MonoBehaviour
    {
        [SerializeField] private CinemachineBrain cinemachineBrain;

        private void Awake() 
        {
            if (cinemachineBrain == null && !TryGetComponent(out cinemachineBrain))
            {
                this.LogWarning("There is no valid CinemachineBrian Component attached to this gameObject", context: this);
                return;
            }

            ServiceLocator.Get<ICameraHolder>().SetCinemachineBrain(cinemachineBrain);
        }
    }
}

