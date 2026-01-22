using UnityEngine;

public abstract class OptionPanelUIBase : MonoBehaviour
{
    protected virtual void OnEnable()
    {
        SyncFromSettings();
    }

    

    

    public void NotifyMenuClosed()
    {
        OnMenuClosed();
    }

    protected virtual void OnMenuClosed()
    {
    }
public void ApplyDefaults()
    {
        ResetToDefaults();
        SyncFromSettings();
    }

    protected abstract void ResetToDefaults();
protected abstract void SyncFromSettings();
}
