using System;
using System.Collections.Generic;
using System.Diagnostics;
using TH.Attribute;
using TH.Resource;
using TH.Utils;

namespace TH.Combat
{
    public sealed partial class SkillController
    {
        private readonly Dictionary<string, IGameSkill> runtimeSkillsById = new(StringComparer.Ordinal);
        private readonly Dictionary<SkillTypeSO, List<IGameSkill>> runtimeSkillsByDefinition = new();
        private readonly List<IGameSkill> runtimeRegisteredSkills = new();
        private readonly List<IGameSkill> runtimeAvailableSkills = new();
        private readonly List<IGameSkill> runtimeOrderedAvailableSkills = new();

        private bool gameSkillBridgeInitialized;
        private IGameSkill activeRuntimeSkill;
        private int suppressLegacySkillTypeWarningDepth;
        private readonly HashSet<string> warnedLegacySkillTypeApis = new(StringComparer.Ordinal);

        public event Action<IGameSkill> OnActiveGameSkillChanged;
        public event Action<IGameSkill> OnResolvedGameSkillChanged;
        public event Action<IGameSkill> OnGameSkillReady;
        public event Action<int, IGameSkill> OnGameSkillSlotChanged;
        public event Action<IGameSkill, int, int> OnGameComboStepChanged;
        public event Action<IGameSkill> OnGameSkillSlotHighlightRequested;

        public IGameSkill ActiveGameSkill
        {
            get
            {
                EnsureGameSkillBridge();
                if (activeRuntimeSkill.IsNotNull() &&
                    ActiveSkill.IsNotNull() &&
                    activeRuntimeSkill.Definition == ActiveSkill)
                {
                    return activeRuntimeSkill;
                }

                activeRuntimeSkill = ResolvePreferredRuntimeSkillForDefinition(ActiveSkill);
                return activeRuntimeSkill;
            }
        }

        public IGameSkill ResolvedGameSkill
        {
            get
            {
                EnsureGameSkillBridge();
                return ResolvePreferredRuntimeSkillForDefinition(ResolvedSkill);
            }
        }

        public IGameSkill ExecutingGameSkill
        {
            get
            {
                EnsureGameSkillBridge();
                return ResolvePreferredRuntimeSkillForDefinition(ExecutingSkill);
            }
        }

        public IReadOnlyList<IGameSkill> RegisteredGameSkills
        {
            get
            {
                EnsureGameSkillBridge();
                RefreshRuntimeSkillViews();
                return runtimeRegisteredSkills;
            }
        }

        public IReadOnlyList<IGameSkill> AvailableGameSkills
        {
            get
            {
                EnsureGameSkillBridge();
                RefreshRuntimeSkillViews();
                return runtimeAvailableSkills;
            }
        }

        public IReadOnlyList<IGameSkill> OrderedAvailableGameSkills
        {
            get
            {
                EnsureGameSkillBridge();
                RefreshRuntimeSkillViews();
                return runtimeOrderedAvailableSkills;
            }
        }

        public bool RegisterSkill(IGameSkill skill, bool setActive = false)
        {
            EnsureGameSkillBridge();
            if (skill.IsNull())
            {
                return false;
            }

            bool runtimeChanged = RegisterRuntimeSkillInstance(skill);
            bool changed;
            using (SuppressLegacySkillTypeWarning())
            {
                changed = RegisterSkill(skill.Definition, setActive: false);
            }

            if (setActive)
            {
                changed |= SetActiveSkill(skill);
            }

            RefreshRuntimeSkillViews();
            return runtimeChanged || changed;
        }

        public bool RegisterSkillFromProvider(
            IGameSkill skill,
            string providerId,
            bool setActive = false,
            bool setAvailable = true)
        {
            EnsureGameSkillBridge();
            if (skill.IsNull())
            {
                return false;
            }

            bool runtimeChanged = RegisterRuntimeSkillInstance(skill);
            bool changed;
            using (SuppressLegacySkillTypeWarning())
            {
                changed = RegisterSkillFromProvider(skill.Definition, providerId, setActive: false, setAvailable: setAvailable);
            }

            if (setActive)
            {
                changed |= SetActiveSkill(skill);
            }

            RefreshRuntimeSkillViews();
            return runtimeChanged || changed;
        }

        public bool RemoveSkillFromProvider(IGameSkill skill, string providerId)
        {
            EnsureGameSkillBridge();
            if (skill.IsNull())
            {
                return false;
            }

            bool runtimeRemoved = UnregisterRuntimeSkillInstance(skill);
            bool shouldRemoveLegacy = !runtimeSkillsByDefinition.TryGetValue(skill.Definition, out var remaining) ||
                                      remaining == null ||
                                      remaining.Count == 0;

            bool legacyRemoved = false;
            if (shouldRemoveLegacy)
            {
                using (SuppressLegacySkillTypeWarning())
                {
                    legacyRemoved = RemoveSkillFromProvider(skill.Definition, providerId);
                }
            }

            RefreshRuntimeSkillViews();
            return runtimeRemoved || legacyRemoved;
        }

        public bool SetActiveSkill(IGameSkill skill)
        {
            EnsureGameSkillBridge();
            using (SuppressLegacySkillTypeWarning())
            {
                if (skill.IsNull())
                {
                    activeRuntimeSkill = null;
                    return SetActiveSkill((SkillTypeSO)null);
                }

                RegisterRuntimeSkillInstance(skill);
                bool changed = SetActiveSkill(skill.Definition);
                if (HasActiveSkill && ActiveSkill == skill.Definition)
                {
                    activeRuntimeSkill = skill;
                    OnActiveGameSkillChanged?.Invoke(activeRuntimeSkill);
                }

                return changed;
            }
        }

        public bool ApplySkillAvailabilityChange(IGameSkill targetSkill, IGameSkill replacementSkill = null)
        {
            EnsureGameSkillBridge();
            if (targetSkill.IsNull())
            {
                return false;
            }

            if (replacementSkill.IsNotNull())
            {
                RegisterRuntimeSkillInstance(replacementSkill);
            }

            bool changed;
            using (SuppressLegacySkillTypeWarning())
            {
                changed = ApplySkillAvailabilityChange(targetSkill.Definition, replacementSkill?.Definition);
            }

            RefreshRuntimeSkillViews();
            return changed;
        }

        public bool TryGetOrderedGameSkillAt(int slotIndex, out IGameSkill skill)
        {
            EnsureGameSkillBridge();
            skill = null;

            SkillTypeSO definition;
            using (SuppressLegacySkillTypeWarning())
            {
                if (!TryGetOrderedSkillAt(slotIndex, out definition))
                {
                    return false;
                }
            }

            if (definition.IsNull())
            {
                return false;
            }

            skill = ResolvePreferredRuntimeSkillForDefinition(definition);
            return skill.IsNotNull();
        }

        public int FindOrderedGameSkillSlotIndex(IGameSkill skill)
        {
            if (skill.IsNull())
            {
                return -1;
            }

            using (SuppressLegacySkillTypeWarning())
            {
                return FindOrderedSkillSlotIndex(skill.Definition);
            }
        }

        public float GetRemainingCooldown(IGameSkill skill)
        {
            if (skill.IsNull())
            {
                return 0f;
            }

            using (SuppressLegacySkillTypeWarning())
            {
                return GetRemainingCooldown(skill.Definition);
            }
        }

        public bool TryGetActiveSequenceTimeout(IGameSkill skill, out float remainingTimeout, out float totalTimeout)
        {
            if (skill.IsNull())
            {
                remainingTimeout = 0f;
                totalTimeout = 0f;
                return false;
            }

            using (SuppressLegacySkillTypeWarning())
            {
                return TryGetActiveSequenceTimeout(skill.Definition, out remainingTimeout, out totalTimeout);
            }
        }

        private IDisposable SuppressLegacySkillTypeWarning()
        {
            suppressLegacySkillTypeWarningDepth++;
            return new LegacySkillTypeWarningScope(this);
        }

        private void WarnLegacySkillTypePath(string apiName)
        {
            if (suppressLegacySkillTypeWarningDepth > 0 || string.IsNullOrWhiteSpace(apiName))
            {
                return;
            }

            if (IsInternalLegacySkillTypeInvocation())
            {
                return;
            }

            if (!warnedLegacySkillTypeApis.Add(apiName))
            {
                return;
            }

            Logg.LogWarning($"[{nameof(SkillController)}] Legacy SkillTypeSO API used: {apiName}. Prefer IGameSkill API.");
        }

        private static bool IsInternalLegacySkillTypeInvocation()
        {
            var trace = new StackTrace(skipFrames: 2, fNeedFileInfo: false);
            for (int i = 1; i < trace.FrameCount; i++)
            {
                var caller = trace.GetFrame(i)?.GetMethod();
                if (caller == null)
                {
                    continue;
                }

                var declaringType = caller.DeclaringType;
                if (declaringType == null)
                {
                    continue;
                }

                return declaringType == typeof(SkillController);
            }

            return false;
        }


        private sealed class LegacySkillTypeWarningScope : IDisposable
        {
            private SkillController owner;

            public LegacySkillTypeWarningScope(SkillController owner)
            {
                this.owner = owner;
            }

            public void Dispose()
            {
                if (owner == null)
                {
                    return;
                }

                if (owner.suppressLegacySkillTypeWarningDepth > 0)
                {
                    owner.suppressLegacySkillTypeWarningDepth--;
                }

                owner = null;
            }
        }

        private void EnsureGameSkillBridge()
        {
            if (gameSkillBridgeInitialized)
            {
                return;
            }

            OnActiveSkillChanged += HandleLegacyActiveSkillChanged;
            OnResolvedSkillChanged += HandleLegacyResolvedSkillChanged;
            OnSkillReady += HandleLegacySkillReady;
            OnSkillSlotChanged += HandleLegacySkillSlotChanged;
            OnComboStepChanged += HandleLegacyComboStepChanged;
            OnSkillSlotHighlightRequested += HandleLegacySlotHighlightRequested;
            gameSkillBridgeInitialized = true;
            RefreshRuntimeSkillViews();
        }

        private void HandleLegacyActiveSkillChanged(SkillTypeSO skill)
        {
            if (skill.IsNull())
            {
                activeRuntimeSkill = null;
                OnActiveGameSkillChanged?.Invoke(null);
                return;
            }

            if (activeRuntimeSkill.IsNull() || activeRuntimeSkill.Definition != skill)
            {
                activeRuntimeSkill = ResolvePreferredRuntimeSkillForDefinition(skill);
            }

            OnActiveGameSkillChanged?.Invoke(activeRuntimeSkill);
        }

        private void HandleLegacyResolvedSkillChanged(SkillTypeSO skill)
        {
            OnResolvedGameSkillChanged?.Invoke(ResolvePreferredRuntimeSkillForDefinition(skill));
        }

        private void HandleLegacySkillReady(SkillTypeSO skill)
        {
            OnGameSkillReady?.Invoke(ResolvePreferredRuntimeSkillForDefinition(skill));
        }

        private void HandleLegacySkillSlotChanged(int slotIndex, SkillTypeSO skill)
        {
            OnGameSkillSlotChanged?.Invoke(slotIndex, ResolvePreferredRuntimeSkillForDefinition(skill));
        }

        private void HandleLegacyComboStepChanged(SkillTypeSO baseSkill, int stepIndex, int stepCount)
        {
            OnGameComboStepChanged?.Invoke(
                ResolvePreferredRuntimeSkillForDefinition(baseSkill),
                stepIndex,
                stepCount);
        }

        private void HandleLegacySlotHighlightRequested(SkillTypeSO skill)
        {
            OnGameSkillSlotHighlightRequested?.Invoke(ResolvePreferredRuntimeSkillForDefinition(skill));
        }

        private bool RegisterRuntimeSkillInstance(IGameSkill runtimeSkill)
        {
            if (runtimeSkill.IsNull())
            {
                return false;
            }

            string runtimeId = string.IsNullOrWhiteSpace(runtimeSkill.RuntimeId)
                ? Guid.NewGuid().ToString("N")
                : runtimeSkill.RuntimeId;
            if (runtimeSkillsById.ContainsKey(runtimeId))
            {
                return false;
            }

            runtimeSkillsById.Add(runtimeId, runtimeSkill);
            runtimeRegisteredSkills.Add(runtimeSkill);

            if (!runtimeSkillsByDefinition.TryGetValue(runtimeSkill.Definition, out var instances))
            {
                instances = new List<IGameSkill>();
                runtimeSkillsByDefinition.Add(runtimeSkill.Definition, instances);
            }

            instances.Add(runtimeSkill);
            return true;
        }

        private bool UnregisterRuntimeSkillInstance(IGameSkill runtimeSkill)
        {
            if (runtimeSkill.IsNull())
            {
                return false;
            }

            bool changed = false;
            if (!string.IsNullOrWhiteSpace(runtimeSkill.RuntimeId))
            {
                changed |= runtimeSkillsById.Remove(runtimeSkill.RuntimeId);
            }

            changed |= runtimeRegisteredSkills.Remove(runtimeSkill);

            if (runtimeSkillsByDefinition.TryGetValue(runtimeSkill.Definition, out var instances))
            {
                changed |= instances.Remove(runtimeSkill);
                if (instances.Count == 0)
                {
                    runtimeSkillsByDefinition.Remove(runtimeSkill.Definition);
                }
            }

            if (activeRuntimeSkill == runtimeSkill)
            {
                activeRuntimeSkill = null;
            }

            return changed;
        }

        private IGameSkill ResolvePreferredRuntimeSkillForDefinition(SkillTypeSO definition)
        {
            if (definition.IsNull())
            {
                return null;
            }

            if (activeRuntimeSkill.IsNotNull() && activeRuntimeSkill.Definition == definition)
            {
                return activeRuntimeSkill;
            }

            if (runtimeSkillsByDefinition.TryGetValue(definition, out var instances) &&
                instances != null &&
                instances.Count > 0)
            {
                return instances[0];
            }

            var created = new GameSkill(definition, ResolvePreferredRuntimeSkillForDefinition);
            RegisterRuntimeSkillInstance(created);
            return created;
        }

        private static bool ContainsDefinition(IReadOnlyList<SkillTypeSO> skills, SkillTypeSO definition)
        {
            if (skills == null || definition.IsNull())
            {
                return false;
            }

            for (int i = 0; i < skills.Count; i++)
            {
                if (skills[i] == definition)
                {
                    return true;
                }
            }

            return false;
        }

        private void RefreshRuntimeSkillViews()
        {
            runtimeAvailableSkills.Clear();
            runtimeOrderedAvailableSkills.Clear();

            if (RegisteredSkills != null)
            {
                for (int i = 0; i < RegisteredSkills.Count; i++)
                {
                    var definition = RegisteredSkills[i];
                    if (definition.IsNull())
                    {
                        continue;
                    }

                    ResolvePreferredRuntimeSkillForDefinition(definition);
                }
            }

            if (AvailableSkills != null)
            {
                for (int i = 0; i < runtimeRegisteredSkills.Count; i++)
                {
                    var runtimeSkill = runtimeRegisteredSkills[i];
                    if (runtimeSkill.IsNull())
                    {
                        continue;
                    }

                    if (ContainsDefinition(AvailableSkills, runtimeSkill.Definition))
                    {
                        runtimeAvailableSkills.Add(runtimeSkill);
                    }
                }
            }

            if (OrderedAvailableSkills != null)
            {
                for (int i = 0; i < OrderedAvailableSkills.Count; i++)
                {
                    var definition = OrderedAvailableSkills[i];
                    if (definition.IsNull())
                    {
                        continue;
                    }

                    var runtimeSkill = ResolvePreferredRuntimeSkillForDefinition(definition);
                    if (runtimeSkill.IsNotNull())
                    {
                        runtimeOrderedAvailableSkills.Add(runtimeSkill);
                    }
                }
            }
        }
    }
}
