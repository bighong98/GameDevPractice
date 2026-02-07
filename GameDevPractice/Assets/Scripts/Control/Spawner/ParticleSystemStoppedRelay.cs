using UnityEngine;

public sealed class ParticleSystemStoppedRelay : MonoBehaviour
{
    private SimplePooledParticlePlayer owner;

    public void Bind(SimplePooledParticlePlayer player)
    {
        owner = player;
    }

    private void OnParticleSystemStopped()
    {
        if (owner == null) return;
        owner.ReleaseSelf();
    }
}
