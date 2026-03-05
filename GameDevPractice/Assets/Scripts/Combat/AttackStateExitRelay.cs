using System;
using System.Collections.Generic;
using TH.Utils;

public interface IAttackStateExitSignalSource
{
    bool IsAttackActive { get; }
    int CurrentAttackEpoch { get; }
    int LastExitedAttackEpoch { get; }
    IDisposable SubscribeAttackExited(Action<int> handler);
}

public sealed class AttackStateExitRelay
{
    private readonly List<Action<int>> handlers = new();
    private bool isAttackActive;
    private int currentAttackEpoch;
    private int lastExitedAttackEpoch;

    public bool IsAttackActive => isAttackActive;
    public int CurrentAttackEpoch => currentAttackEpoch;
    public int LastExitedAttackEpoch => lastExitedAttackEpoch;

    public void Update(bool isInAttackState)
    {
        if (!isAttackActive && isInAttackState)
        {
            isAttackActive = true;
            currentAttackEpoch++;
            return;
        }

        if (!isAttackActive || isInAttackState)
        {
            return;
        }

        CompleteCurrentAttack();
    }

    public void ForceCompleteCurrentAttack()
    {
        if (!isAttackActive)
        {
            return;
        }

        CompleteCurrentAttack();
    }

    public IDisposable SubscribeAttackExited(Action<int> handler)
    {
        if (handler == null)
        {
            return DisposableDelegate.Empty;
        }

        handlers.Add(handler);
        return new DisposableDelegate(() => handlers.Remove(handler));
    }

    private void CompleteCurrentAttack()
    {
        isAttackActive = false;
        lastExitedAttackEpoch = currentAttackEpoch;

        if (handlers.Count == 0)
        {
            return;
        }

        Action<int>[] snapshot = handlers.ToArray();
        for (int i = 0; i < snapshot.Length; i++)
        {
            snapshot[i]?.Invoke(lastExitedAttackEpoch);
        }
    }
}
