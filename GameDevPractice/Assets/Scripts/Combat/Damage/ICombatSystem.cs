namespace TH.Combat.Service
{
    public delegate void HitAppliedEvent(in HitResult result, IDamageable target);

    public interface ICombatSystem
    {
        event HitAppliedEvent OnHitApplied;
        void ApplyHit(in HitRequest hitRequest);
    }
}

