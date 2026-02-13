#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.AddressableAssets.Settings;

[UnityEditor.InitializeOnLoad]
public static class AddressablesSimplifyKeepExtensionContextMenu
{
    private const string EntryCommandId = "Simplify Addressable Names (Keep Extension)";
    private const string GroupCommandId = "Simplify Addressable Names (Keep Extension)";

    static AddressablesSimplifyKeepExtensionContextMenu()
    {
        if (AddressableAssetSettings.CustomAssetEntryCommands.Contains(EntryCommandId))
            AddressableAssetSettings.UnregisterCustomAssetEntryCommand(EntryCommandId);
        if (AddressableAssetSettings.CustomAssetGroupCommands.Contains(GroupCommandId))
            AddressableAssetSettings.UnregisterCustomAssetGroupCommand(GroupCommandId);

        AddressableAssetSettings.RegisterCustomAssetEntryCommand(EntryCommandId, SimplifySelectedEntriesKeepExtension);
        AddressableAssetSettings.RegisterCustomAssetGroupCommand(GroupCommandId, SimplifySelectedGroupsKeepExtension);
    }

    private static void SimplifySelectedEntriesKeepExtension(IEnumerable<AddressableAssetEntry> entries)
    {
        if (entries == null)
            return;

        var entryList = entries.Where(entry => entry != null).ToList();
        if (entryList.Count == 0)
            return;

        var modifiedGroups = new HashSet<AddressableAssetGroup>();
        foreach (var entry in entryList)
        {
            if (!string.IsNullOrEmpty(entry.address))
                entry.SetAddress(Path.GetFileName(entry.address), false);

            if (entry.parentGroup != null)
                modifiedGroups.Add(entry.parentGroup);
        }

        MarkDirty(entryList, modifiedGroups);
    }

    private static void SimplifySelectedGroupsKeepExtension(IEnumerable<AddressableAssetGroup> groups)
    {
        if (groups == null)
            return;

        var entryList = new List<AddressableAssetEntry>();
        var modifiedGroups = new HashSet<AddressableAssetGroup>();

        foreach (var group in groups.Where(group => group != null))
        {
            foreach (var entry in group.entries)
            {
                if (!string.IsNullOrEmpty(entry.address))
                    entry.SetAddress(Path.GetFileName(entry.address), false);
                entryList.Add(entry);
            }

            modifiedGroups.Add(group);
        }

        if (entryList.Count == 0)
            return;

        MarkDirty(entryList, modifiedGroups);
    }

    private static void MarkDirty(List<AddressableAssetEntry> entries, HashSet<AddressableAssetGroup> modifiedGroups)
    {
        foreach (var group in modifiedGroups)
            group.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, entries, false, true);

        var settings = modifiedGroups.Select(group => group.Settings).FirstOrDefault(current => current != null)
            ?? entries.Select(entry => entry.parentGroup?.Settings).FirstOrDefault(current => current != null);

        if (settings != null)
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, entries, true, false);
    }
}
#endif
