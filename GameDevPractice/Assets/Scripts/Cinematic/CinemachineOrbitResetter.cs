using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;

[RequireComponent(typeof(CinemachineOrbitalFollow))]
public sealed class CinemachineOrbitResetter : MonoBehaviour
{
    [SerializeField] private bool resetOnSceneLoad = true;

    private CinemachineOrbitalFollow orbitalFollow;
    private float defaultHorizontal;
    private float defaultVertical;
    private float defaultRadial;

    private void Awake()
    {
        TryGetComponent(out orbitalFollow);
        CacheDefaults();
    }

    private void OnEnable()
    {
        if (resetOnSceneLoad)
            SceneManager.sceneLoaded += HandleSceneLoaded;

        ResetAxes();
    }

    private void OnDisable()
    {
        if (resetOnSceneLoad)
            SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResetAxes();
    }

    private void CacheDefaults()
    {
        if (orbitalFollow == null) return;

        defaultHorizontal = orbitalFollow.HorizontalAxis.Center;
        defaultVertical = orbitalFollow.VerticalAxis.Center;
        defaultRadial = orbitalFollow.RadialAxis.Center;
    }

    private void ResetAxes()
    {
        if (orbitalFollow == null) return;

        orbitalFollow.HorizontalAxis.Value = defaultHorizontal;
        orbitalFollow.VerticalAxis.Value = defaultVertical;
        orbitalFollow.RadialAxis.Value = defaultRadial;
    }
}
