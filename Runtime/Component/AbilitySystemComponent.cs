// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// GAS 中枢。
// 挂在角色 GameObject 上，统管：拥有标签 / 属性集 / 效果 / 技能授予与激活 / 激活组互斥 / 标签关系。
// 诚实声明：本阶段为单机权威实现。
// 但所有"会改状态"的入口都集中在此，便于阶段 6 接 Netcode for GameObjects 时统一加 authority 判断。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Likeon.GAS
{
    [AddComponentMenu("Sigil/GAS/Ability System Component")]
    public class AbilitySystemComponent : MonoBehaviour
    {
        // ===================== 默认技能装载 Default Loadouts =====================
        // 对应 UE 技能系统的 DefaultAbilitySet（一组 AbilitySet）：每个 AbilityLoadout = 一组 属性集 + 技能 + 初始化效果。
        [Header("默认技能装载 Initial Loadouts")]
        [Tooltip("Inspector 配一组 AbilityLoadout（对齐 UE DefaultAbilitySet 复数）：每个装载一组 属性集 + 技能 + 初始化效果。Awake 时按序全部 GrantLoadout —— 角色的初始属性/技能在 prefab 即可配，无需代码 AddAttributeSet/GiveAbility。")]
        [SerializeField] private List<AbilityLoadout> initialLoadouts = new List<AbilityLoadout>();

        [Tooltip("初始装载里‘初始化效果’施加时用的等级（对齐 UE AttributeSetInitializeLevel）：属性按等级初始化时，曲线表 magnitude 据此 level 查值。")]
        [SerializeField, Min(1)] private int attributeInitializeLevel = 1;

        /// <summary>Inspector 配的初始装载列表（对应 UE DefaultAbilitySet）。生成器/代码可填充后由 Awake 授予。</summary>
        public List<AbilityLoadout> InitialLoadouts => initialLoadouts;

        /// <summary>属性初始化等级（GrantLoadout 的初始化效果按此 level 施加）。</summary>
        public int AttributeInitializeLevel { get => attributeInitializeLevel; set => attributeInitializeLevel = value; }

        // 已授予的初始装载句柄（对应 UE DefaultAbilitySet_GrantedHandles）：供反初始化整批回收。
        private readonly List<GrantedAbilityHandles> _grantedLoadoutHandles = new List<GrantedAbilityHandles>();
        /// <summary>已授予的初始装载句柄（只读，供整批回收）。</summary>
        public IReadOnlyList<GrantedAbilityHandles> GrantedLoadoutHandles => _grantedLoadoutHandles;

        /// <summary>按序授予所有初始装载，记录句柄供回收。Awake 自动调用；列表为空则不做事（零回归）。可重写扩展初始化时机。</summary>
        protected virtual void Awake()
        {
            for (int i = 0; i < initialLoadouts.Count; i++)
                if (initialLoadouts[i] != null) _grantedLoadoutHandles.Add(GrantLoadout(initialLoadouts[i]));
        }

        // ===================== 拥有标签 Owned Tags =====================
        private readonly GameplayTagCountContainer _ownedTags = new GameplayTagCountContainer();

        /// <summary>某标签从有到无 / 无到有时触发。</summary>
        public event Action<GameplayTag, bool> OnTagChanged
        {
            add => _ownedTags.OnTagCountChanged += value;
            remove => _ownedTags.OnTagCountChanged -= value;
        }

        /// <summary>层级查询：角色当前是否拥有该状态标签。</summary>
        public bool HasMatchingGameplayTag(GameplayTag tag) => _ownedTags.HasMatchingTag(tag);

        public void AddLooseGameplayTag(GameplayTag tag, int count = 1) => _ownedTags.UpdateTagCount(tag, count);
        public void RemoveLooseGameplayTag(GameplayTag tag, int count = 1) => _ownedTags.UpdateTagCount(tag, -count);

        /// <summary>取出当前拥有的标签容器（供 TagRelationship / 输入门控查询）。</summary>
        public void GetOwnedGameplayTags(GameplayTagContainer outContainer)
        {
            outContainer.Clear();
            _ownedTags.FillTagContainer(outContainer);
        }

        /// <summary>取出当前拥有的（显式）标签及计数（供调试器/UI 枚举多来源叠加情况）。</summary>
        public void GetOwnedGameplayTagCounts(List<KeyValuePair<GameplayTag, int>> outList)
            => _ownedTags.FillTagCounts(outList);

        private readonly GameplayTagContainer _ownedTagsCache = new GameplayTagContainer();
        private GameplayTagContainer OwnedTagsSnapshot()
        {
            GetOwnedGameplayTags(_ownedTagsCache);
            return _ownedTagsCache;
        }

        // ===================== 属性集 Attribute Sets =====================
        private readonly List<AttributeSet> _attributeSets = new List<AttributeSet>();

        /// <summary>属性当前值变化时触发（载 <see cref="AttributeChangeData"/>：属性/旧值/新值/来源）。UI/表现订阅。
        /// 对齐 UE 属性变更事件——来源效果可用时随事件带出"谁打的/哪个效果"，无单一来源时 Source=null。</summary>
        public event Action<AttributeChangeData> OnAttributeChanged;

        public T AddAttributeSet<T>() where T : AttributeSet, new()
        {
            var set = new T();
            AddAttributeSet(set);
            return set;
        }

        public void AddAttributeSet(AttributeSet set)
        {
            if (set == null || _attributeSets.Contains(set)) return;
            set.Owner = this;
            set.EnsureRegistered();
            _attributeSets.Add(set);
        }

        public void RemoveAttributeSet(AttributeSet set)
        {
            if (set != null && _attributeSets.Remove(set)) set.Owner = null;
        }

        /// <summary>按类型全名取属性集。供 <see cref="GameplayAttribute"/> 解析数据。</summary>
        public AttributeSet GetAttributeSet(string typeFullName)
        {
            for (int i = 0; i < _attributeSets.Count; i++)
                if (_attributeSets[i].GetType().FullName == typeFullName) return _attributeSets[i];
            return null;
        }

        public T GetAttributeSet<T>() where T : AttributeSet
        {
            for (int i = 0; i < _attributeSets.Count; i++)
                if (_attributeSets[i] is T t) return t;
            return null;
        }

        /// <summary>当前持有的全部属性集（只读，供调试器/UI 枚举）。</summary>
        public IReadOnlyList<AttributeSet> GetAttributeSets() => _attributeSets;

        public float GetAttributeValue(GameplayAttribute attribute) => attribute.GetCurrentValue(this);
        public float GetAttributeBaseValue(GameplayAttribute attribute) => attribute.GetBaseValue(this);

        // ===================== 技能授予与激活 Abilities =====================
        private readonly Dictionary<int, GameplayAbilitySpec> _abilities = new Dictionary<int, GameplayAbilitySpec>();
        private int _nextAbilityId = 1;

        public event Action<GameplayAbility> OnAbilityActivated;
        public event Action<GameplayAbility, bool> OnAbilityEnded;

        /// <summary>技能激活失败时触发（带失败原因）。供 UI 反馈"为何放不出技能"（对齐 UE OnAbilityActivationFailed）。</summary>
        public event Action<GameplayAbility, EAbilityActivationFailReason> OnAbilityActivationFailed;

        /// <summary>一个技能被授予时触发（含 loadout / 全局系统的批量授予）。供 loadout 驱动的技能栏订阅。</summary>
        public event Action<GameplayAbilitySpec> OnAbilityGiven;

        /// <summary>一个技能被移除时触发（在销毁实例前回调，订阅方仍可读 spec.Ability）。</summary>
        public event Action<GameplayAbilitySpec> OnAbilityRemoved;

        /// <summary>当前所有已授予技能的只读集合（供 UI 列举技能栏 / 调试）。</summary>
        public IReadOnlyCollection<GameplayAbilitySpec> GetGrantedAbilities() => _abilities.Values;

        /// <summary>授予一个技能（克隆模板成本角色实例）。<paramref name="dynamicTags"/> 在授予时附加到实例的 AbilityTags（对齐 UE DynamicTags）。</summary>
        public GameplayAbilitySpecHandle GiveAbility(GameplayAbility abilityTemplate, int level = 1, IReadOnlyList<GameplayTag> dynamicTags = null)
        {
            if (abilityTemplate == null) return GameplayAbilitySpecHandle.Invalid;

            var instance = UnityEngine.Object.Instantiate(abilityTemplate); // InstancedPerActor：每角色独立实例
            instance.hideFlags = HideFlags.HideAndDontSave;
            // 授予时附加的动态标签（对齐 UE 技能集授予项的动态标签）：加到克隆实例的身份标签，参与 TagRelationship 匹配
            if (dynamicTags != null)
                for (int i = 0; i < dynamicTags.Count; i++)
                    if (dynamicTags[i].IsValid && !instance.AbilityTags.Contains(dynamicTags[i]))
                        instance.AbilityTags.Add(dynamicTags[i]);
            // 额外消耗也按实例克隆（对齐 UE Instanced）：否则多个技能实例会共享同一 cost SO 的状态（如充能计数互相干扰）
            for (int i = 0; i < instance.AdditionalCosts.Count; i++)
            {
                var c = instance.AdditionalCosts[i];
                if (c == null) continue;
                var clone = UnityEngine.Object.Instantiate(c);
                clone.hideFlags = HideFlags.HideAndDontSave;
                instance.AdditionalCosts[i] = clone;
            }
            var handle = new GameplayAbilitySpecHandle(_nextAbilityId++);
            var spec = new GameplayAbilitySpec(handle, instance, level);
            instance.ASC = this;
            instance.Spec = spec;
            _abilities[handle.Id] = spec;
            OnAbilityGiven?.Invoke(spec);
            // 被动 / 光环技能：授予即尝试激活（对齐 UE TryActivateAbilityOnSpawn），仍走 CanActivate 检查
            if (instance.ActivateOnGranted) TryActivateSpec(spec, null);
            return handle;
        }

        /// <summary>移除一个技能。</summary>
        public void ClearAbility(GameplayAbilitySpecHandle handle)
        {
            if (_abilities.TryGetValue(handle.Id, out var spec))
            {
                if (spec.IsActive) spec.Ability.CancelAbility();
                _abilities.Remove(handle.Id);
                OnAbilityRemoved?.Invoke(spec); // 在销毁前回调，订阅方仍可读 spec
                if (spec.Ability != null)
                {
                    // 销毁随实例克隆的额外消耗对象，避免泄漏
                    foreach (var c in spec.Ability.AdditionalCosts)
                        if (c != null) UnityEngine.Object.Destroy(c);
                    UnityEngine.Object.Destroy(spec.Ability);
                }
            }
        }

        public GameplayAbilitySpec FindAbilitySpec(GameplayAbilitySpecHandle handle)
            => _abilities.TryGetValue(handle.Id, out var spec) ? spec : null;

        /// <summary>尝试激活指定句柄的技能。</summary>
        public bool TryActivateAbility(GameplayAbilitySpecHandle handle, GameplayEventData triggerData = null)
        {
            var spec = FindAbilitySpec(handle);
            if (spec == null) return false;
            return TryActivateSpec(spec, triggerData);
        }

        /// <summary>尝试激活所有 AbilityTags 命中该 tag 的已授予技能。</summary>
        public bool TryActivateAbilitiesByTag(GameplayTag tag, GameplayEventData triggerData = null)
        {
            bool any = false;
            // 复制一份，避免激活过程中集合被改
            var specs = new List<GameplayAbilitySpec>(_abilities.Values);
            foreach (var spec in specs)
            {
                if (spec.Ability.GetAbilityTags().HasTag(tag))
                    any |= TryActivateSpec(spec, triggerData);
            }
            return any;
        }

        private bool TryActivateSpec(GameplayAbilitySpec spec, GameplayEventData triggerData)
        {
            var ability = spec.Ability;
            if (ability.IsActive) { OnAbilityActivationFailed?.Invoke(ability, EAbilityActivationFailReason.AlreadyActive); return false; }
            if (!ability.CanActivate(out var failReason)) { OnAbilityActivationFailed?.Invoke(ability, failReason); return false; }

            // 独占技能激活：先打断可替换的独占技能
            if (ability.ActivationGroup != EAbilityActivationGroup.Independent)
                CancelAbilitiesWithActivationGroup(EAbilityActivationGroup.ExclusiveReplaceable, ability);

            // 按 TagRelationship 取消互斥技能 + 登记本技能激活期间阻挡的技能标签
            ApplyAbilityBlockAndCancelTags(ability);

            ability.Activate(triggerData);
            OnAbilityActivated?.Invoke(ability);
            return true;
        }

        internal void NotifyAbilityEnded(GameplayAbility ability, bool wasCancelled)
        {
            RemoveBlockedAbilityTags(ability); // 撤销本技能激活期间贡献的"阻挡其它技能激活"标签
            OnAbilityEnded?.Invoke(ability, wasCancelled);
        }

        // ---- 激活组互斥 ----
        private readonly List<GameplayAbility>[] _activationGroupBuckets =
        {
            new List<GameplayAbility>(), // Independent
            new List<GameplayAbility>(), // ExclusiveReplaceable
            new List<GameplayAbility>()  // ExclusiveBlocking
        };

        /// <summary>该激活组当前是否被阻挡。</summary>
        public bool IsActivationGroupBlocked(EAbilityActivationGroup group)
        {
            switch (group)
            {
                case EAbilityActivationGroup.Independent:
                    return false; // 独立技能永不被阻挡
                case EAbilityActivationGroup.ExclusiveReplaceable:
                    // 仅被 ExclusiveBlocking 阻挡
                    return _activationGroupBuckets[(int)EAbilityActivationGroup.ExclusiveBlocking].Count > 0;
                case EAbilityActivationGroup.ExclusiveBlocking:
                    // 被任意独占技能阻挡
                    return _activationGroupBuckets[(int)EAbilityActivationGroup.ExclusiveReplaceable].Count > 0
                        || _activationGroupBuckets[(int)EAbilityActivationGroup.ExclusiveBlocking].Count > 0;
                default:
                    return false;
            }
        }

        public void RegisterAbilityActivationGroup(EAbilityActivationGroup group, GameplayAbility ability)
        {
            if (group == EAbilityActivationGroup.MAX) return;
            var list = _activationGroupBuckets[(int)group];
            if (!list.Contains(ability)) list.Add(ability);
        }

        public void UnregisterAbilityActivationGroup(EAbilityActivationGroup group, GameplayAbility ability)
        {
            if (group == EAbilityActivationGroup.MAX) return;
            _activationGroupBuckets[(int)group].Remove(ability);
        }

        /// <summary>取消某激活组里的技能（可忽略一个）。</summary>
        public void CancelAbilitiesWithActivationGroup(EAbilityActivationGroup group, GameplayAbility ignore)
        {
            if (group == EAbilityActivationGroup.MAX) return;
            var snapshot = new List<GameplayAbility>(_activationGroupBuckets[(int)group]);
            foreach (var ab in snapshot)
                if (ab != ignore && ab.IsActive) ab.CancelAbility();
        }

        /// <summary>能否把技能换到新激活组（排除自身占位后，新组当前不被其它技能阻挡）。对齐 UE CanChangeActivationGroup。</summary>
        public bool CanChangeActivationGroup(EAbilityActivationGroup newGroup, GameplayAbility ability)
        {
            if (ability == null || newGroup == EAbilityActivationGroup.MAX) return false;
            if (ability.ActivationGroup == newGroup) return true;
            bool registered = ability.IsActive; // 激活中才在 bucket
            if (registered) UnregisterAbilityActivationGroup(ability.ActivationGroup, ability);
            bool blocked = IsActivationGroupBlocked(newGroup);
            if (registered) RegisterAbilityActivationGroup(ability.ActivationGroup, ability);
            return !blocked;
        }

        /// <summary>把技能换到新激活组（如蓄力→释放切组）。成功返回 true。对齐 UE ChangeActivationGroup。</summary>
        public bool ChangeActivationGroup(EAbilityActivationGroup newGroup, GameplayAbility ability)
        {
            if (!CanChangeActivationGroup(newGroup, ability)) return false;
            if (ability.IsActive)
            {
                UnregisterAbilityActivationGroup(ability.ActivationGroup, ability);
                ability.ActivationGroup = newGroup;
                RegisterAbilityActivationGroup(newGroup, ability);
            }
            else ability.ActivationGroup = newGroup;
            return true;
        }

        // ===================== 标签关系映射 TagRelationship =====================
        public AbilityInteractionRules InteractionRules;

        public void SetInteractionRules(AbilityInteractionRules mapping) => InteractionRules = mapping;

        /// <summary>按当前角色状态，取得激活某技能额外的所需/禁止标签。</summary>
        public void GatherActivationRequirements(GameplayTagContainer abilityTags,
            GameplayTagContainer outRequired, GameplayTagContainer outBlocked)
        {
            if (InteractionRules == null) return;
            InteractionRules.CollectActivationRequirements(OwnedTagsSnapshot(), abilityTags, outRequired, outBlocked);
        }

        // 按关系映射取消互斥技能 + 登记激活期间阻挡的技能标签
        private readonly GameplayTagContainer _blockTagsCache = new GameplayTagContainer();
        private readonly GameplayTagContainer _cancelTagsCache = new GameplayTagContainer();

        // "激活期间阻挡其它技能激活"标签的引用计数累加器：
        // 多个激活中技能可能各自贡献同一标签，靠计数避免一方结束误清。
        // _blockedAbilityTagsView 与计数同步，是个 GameplayTagContainer，复用 HasAny 做层级匹配（与 cancel 路径一致）。
        private readonly Dictionary<GameplayTag, int> _blockedAbilityTagCounts = new Dictionary<GameplayTag, int>();
        private readonly GameplayTagContainer _blockedAbilityTagsView = new GameplayTagContainer();
        // 每个激活技能此刻贡献了哪些 block 标签 —— 结束时按原样撤销（条件规则在激活/结束之间可能变化，故记录而非重算）。
        private readonly Dictionary<GameplayAbility, List<GameplayTag>> _appliedBlockTags = new Dictionary<GameplayAbility, List<GameplayTag>>();

        /// <summary>待激活技能的身份标签是否被某个激活中技能的 AbilityTagsToBlock 命中（层级匹配，与 cancel 同语义）。</summary>
        public bool AreAbilityTagsBlocked(GameplayTagContainer abilityTags)
            => !_blockedAbilityTagsView.IsEmpty && _blockedAbilityTagsView.HasAny(abilityTags);

        private void ApplyAbilityBlockAndCancelTags(GameplayAbility ability)
        {
            if (InteractionRules == null) return;
            _blockTagsCache.Clear();
            _cancelTagsCache.Clear();
            InteractionRules.CollectBlockedAndCanceledTags(OwnedTagsSnapshot(), ability.GetAbilityTags(), _blockTagsCache, _cancelTagsCache);

            // 取消：打断已激活、命中 cancel 标签的技能
            if (!_cancelTagsCache.IsEmpty)
            {
                var snapshot = new List<GameplayAbilitySpec>(_abilities.Values);
                foreach (var spec in snapshot)
                {
                    if (spec.Ability == ability || !spec.Ability.IsActive) continue;
                    if (_cancelTagsCache.HasAny(spec.Ability.GetAbilityTags()))
                        spec.Ability.CancelAbility();
                }
            }

            // 阻挡：登记本技能激活期间要阻挡的技能标签（CanActivate 据此拒绝后续激活，技能结束时撤销）
            if (!_blockTagsCache.IsEmpty)
                AddBlockedAbilityTags(ability, _blockTagsCache);
        }

        private void AddBlockedAbilityTags(GameplayAbility ability, GameplayTagContainer tags)
        {
            List<GameplayTag> applied = null;
            foreach (var t in tags)
            {
                if (!t.IsValid) continue;
                _blockedAbilityTagCounts.TryGetValue(t, out int c);
                if (c == 0) _blockedAbilityTagsView.AddTag(t);
                _blockedAbilityTagCounts[t] = c + 1;
                (applied ??= new List<GameplayTag>()).Add(t);
            }
            if (applied != null) _appliedBlockTags[ability] = applied;
        }

        private void RemoveBlockedAbilityTags(GameplayAbility ability)
        {
            if (!_appliedBlockTags.TryGetValue(ability, out var applied)) return;
            _appliedBlockTags.Remove(ability);
            foreach (var t in applied)
            {
                if (!_blockedAbilityTagCounts.TryGetValue(t, out int c)) continue;
                c -= 1;
                if (c <= 0) { _blockedAbilityTagCounts.Remove(t); _blockedAbilityTagsView.RemoveTag(t); }
                else _blockedAbilityTagCounts[t] = c;
            }
        }

        // ===================== 效果 Effects =====================
        private readonly List<ActiveGameplayEffect> _activeEffects = new List<ActiveGameplayEffect>();
        private int _nextEffectId = 1;

        /// <summary>一个持续/永久效果被登记为激活时触发（瞬时效果不产生激活实例，故不触发）。供 buff/debuff 图标条订阅。</summary>
        public event Action<ActiveGameplayEffect> OnActiveEffectAdded;

        /// <summary>一个激活效果被移除时触发（到期、显式移除、或被 RemoveEffectsWithTags 顶掉皆触发）。</summary>
        public event Action<ActiveGameplayEffect> OnActiveEffectRemoved;

        /// <summary>叠层效果层数变化时触发（参数：效果, 旧层数, 新层数）。供 UI 刷新 ×N 角标。</summary>
        public event Action<ActiveGameplayEffect, int, int> OnActiveEffectStackChanged;

        /// <summary>任意 GameplayEffect 结算（改基础值）后触发——组件级统一钩子（对齐 UE 属性系统组件的“GE 结算后”事件）。
        /// 供"想统一监听所有 GE 结算"的订阅方（伤害统计/AI 感知）用，无需逐 AttributeSet 重写。</summary>
        public event Action<GameplayEffectModCallbackData> OnPostGameplayEffectExecute;

        /// <summary>当前所有存活的持续/永久效果（只读视图，含剩余时长/抑制态）。供 UI 列举 buff/debuff。</summary>
        public IReadOnlyList<ActiveGameplayEffect> GetActiveGameplayEffects() => _activeEffects;

        /// <summary>按句柄取激活效果实例（用于刷新单个 buff 图标的剩余时间），未找到返回 null。</summary>
        public ActiveGameplayEffect GetActiveGameplayEffect(ActiveGameplayEffectHandle handle)
        {
            for (int i = 0; i < _activeEffects.Count; i++)
                if (_activeEffects[i].Handle.Equals(handle)) return _activeEffects[i];
            return null;
        }

        /// <summary>制作一个外发效果 spec（带本 ASC 作为来源）。</summary>
        public GameplayEffectSpec MakeOutgoingSpec(GameplayEffect effect, int level = 1)
        {
            var ctx = new GameplayEffectContext(this);
            return new GameplayEffectSpec(effect, ctx, level);
        }

        /// <summary>给自己施加一个效果（按资产 + 等级）。</summary>
        public ActiveGameplayEffectHandle ApplyGameplayEffectToSelf(GameplayEffect effect, int level = 1)
        {
            if (effect == null) return ActiveGameplayEffectHandle.Invalid;
            return ApplyGameplayEffectSpecToSelf(MakeOutgoingSpec(effect, level));
        }

        /// <summary>给自己施加一个效果 spec。</summary>
        public ActiveGameplayEffectHandle ApplyGameplayEffectSpecToSelf(GameplayEffectSpec spec)
        {
            if (spec == null || spec.Def == null) return ActiveGameplayEffectHandle.Invalid;
            var def = spec.Def;

            // 施加条件检查
            var owned = OwnedTagsSnapshot();
            foreach (var t in def.ApplicationRequiredTags) if (!owned.HasTag(t)) return ActiveGameplayEffectHandle.Invalid;
            foreach (var t in def.ApplicationBlockedTags) if (owned.HasTag(t)) return ActiveGameplayEffectHandle.Invalid;

            // 移除带指定标签的现有效果
            if (def.RemoveEffectsWithTags.Count > 0) RemoveActiveEffectsWithTags(def.RemoveEffectsWithTags);

            if (def.IsInstant)
            {
                ExecuteEffectSpec(spec); // 立即改基础值
                foreach (var cue in def.GameplayCues) ExecuteGameplayCue(cue, MakeCueParams(spec));
                return ActiveGameplayEffectHandle.Invalid; // 瞬时效果不产生句柄
            }

            // 可叠层效果：命中已有同组实例则加层并按策略刷新，不新建实例
            if (def.IsStackable)
            {
                var existing = FindStackableActiveEffect(spec);
                if (existing != null)
                {
                    ApplyStackOnExisting(existing, spec);
                    return existing.Handle;
                }
            }

            // Duration / Infinite：登记为激活效果
            var handle = new ActiveGameplayEffectHandle(_nextEffectId++);
            var active = new ActiveGameplayEffect(handle, spec);
            _activeEffects.Add(active);

            // 授予标签
            foreach (var t in def.GrantedTags) AddLooseGameplayTag(t);
            // 持续型 cue 开始
            foreach (var cue in def.GameplayCues) AddGameplayCue(cue, MakeCueParams(spec));

            UpdateInhibition(active);
            RecalculateAffectedAttributes(def, spec.Context);

            OnActiveEffectAdded?.Invoke(active);
            return handle;
        }

        /// <summary>移除一个激活效果。</summary>
        public bool RemoveActiveGameplayEffect(ActiveGameplayEffectHandle handle)
        {
            for (int i = 0; i < _activeEffects.Count; i++)
            {
                if (_activeEffects[i].Handle.Equals(handle))
                {
                    var active = _activeEffects[i];
                    _activeEffects.RemoveAt(i);
                    foreach (var t in active.Def.GrantedTags) RemoveLooseGameplayTag(t);
                    foreach (var cue in active.Def.GameplayCues) RemoveGameplayCue(cue, MakeCueParams(active.Spec));
                    RecalculateAffectedAttributes(active.Def);
                    OnActiveEffectRemoved?.Invoke(active);
                    return true;
                }
            }
            return false;
        }

        private void RemoveActiveEffectsWithTags(List<GameplayTag> tags)
        {
            for (int i = _activeEffects.Count - 1; i >= 0; i--)
            {
                bool match = false;
                // 静态 AssetTags + 运行时动态注入（如攻击类型）一并匹配
                foreach (var assetTag in _activeEffects[i].Spec.GetAllAssetTags())
                {
                    foreach (var t in tags)
                        if (assetTag.MatchesTag(t)) { match = true; break; }
                    if (match) break;
                }
                if (match) RemoveActiveGameplayEffect(_activeEffects[i].Handle);
            }
        }

        // 找一个可与新 spec 合并的现有激活实例（同 Def + 同叠层组）
        private ActiveGameplayEffect FindStackableActiveEffect(GameplayEffectSpec spec)
        {
            var def = spec.Def;
            var newSource = spec.Context?.SourceASC; // AggregateBySource 用来源区分
            for (int i = 0; i < _activeEffects.Count; i++)
            {
                var a = _activeEffects[i];
                if (a.Def != def) continue;
                if (def.StackingType == EGameplayEffectStackingType.AggregateBySource
                    && a.Spec.Context?.SourceASC != newSource) continue;
                return a;
            }
            return null;
        }

        // 命中已有实例：按上限加层 + 按策略刷新时长/周期，重算受影响属性
        private void ApplyStackOnExisting(ActiveGameplayEffect existing, GameplayEffectSpec spec)
        {
            var def = spec.Def;
            int oldCount = existing.StackCount;
            int newCount = oldCount;
            bool atCap = def.StackLimitCount > 0 && oldCount >= def.StackLimitCount;
            if (!atCap) newCount = oldCount + 1;
            existing.StackCount = newCount;

            // 时长刷新
            if (def.DurationType == EGameplayEffectDurationType.HasDuration
                && def.StackDurationRefreshPolicy == EGameplayEffectStackingDurationRefreshPolicy.RefreshOnSuccessfulApplication)
                existing.TimeRemaining = def.Duration;

            // 周期重置
            if (def.IsPeriodic
                && def.StackPeriodResetPolicy == EGameplayEffectStackingPeriodResetPolicy.ResetOnSuccessfulApplication)
                existing.PeriodRemaining = def.Period;

            if (newCount != oldCount)
            {
                RecalculateAffectedAttributes(def, spec.Context); // 层数变了 → 修改量变了
                OnActiveEffectStackChanged?.Invoke(existing, oldCount, newCount);
            }
        }

        // 立即结算一个效果（Instant 或 周期一次）：改基础值 + 执行计算 + PostGameplayEffectExecute 钩子
        // stackCount：周期性叠层效果按层放大结算量（加法语义；其它运算符周期叠层罕见，按单次处理）
        private void ExecuteEffectSpec(GameplayEffectSpec spec, int stackCount = 1)
        {
            var def = spec.Def;

            // 收集所有要施加的增量：普通修改 + 自定义执行
            var outputs = new List<GameplayExecutionOutput>();
            foreach (var mod in def.Modifiers)
            {
                float mag = mod.Magnitude.Evaluate(spec);
                if (mod.Operation == EAttributeModifierOp.Add) mag *= stackCount; // 按层放大加法结算
                outputs.Add(new GameplayExecutionOutput(mod.Attribute, mag, mod.Operation));
            }

            foreach (var exec in def.Executions)
                if (exec != null) exec.Execute(spec, spec.Context?.SourceASC, this, outputs);

            foreach (var output in outputs)
            {
                var set = GetAttributeSet(output.Attribute.AttributeSetTypeName);
                var data = output.Attribute.ResolveData(this);
                if (set == null || data == null) continue;

                // PreGameplayEffectExecute 钩子（可否决）
                var callback = new GameplayEffectModCallbackData
                {
                    Spec = spec, Attribute = output.Attribute,
                    EvaluatedMagnitude = output.Magnitude, TargetASC = this
                };
                if (!set.PreGameplayEffectExecute(callback)) continue;

                // 算新基础值
                float oldBase = data.BaseValue;
                float newBase = ApplyOp(oldBase, output.Operation, output.Magnitude);
                set.PreAttributeBaseChange(output.Attribute, ref newBase);
                data.BaseValue = newBase;

                // 重算当前值（基础值变了）
                RecalculateCurrentValue(set, output.Attribute, spec.Context);

                // PostGameplayEffectExecute 钩子 —— Meta Attribute 伤害管线落点
                callback.EvaluatedMagnitude = newBase - oldBase;
                set.PostGameplayEffectExecute(callback);
                OnPostGameplayEffectExecute?.Invoke(callback); // 组件级统一钩子（#35）
            }
        }

        /// <summary>
        /// 直接对某属性的基础值施加一个修改并重算当前值（不走 GE 结算钩子，避免递归）。
        ///。Meta 属性映射（IncomingDamage→-Health）用它。
        /// </summary>
        public void ApplyModToAttributeBase(GameplayAttribute attribute, EAttributeModifierOp op, float magnitude, GameplayEffectContext source = null)
        {
            var set = GetAttributeSet(attribute.AttributeSetTypeName);
            var data = attribute.ResolveData(this);
            if (set == null || data == null) return;
            float newBase = ApplyOp(data.BaseValue, op, magnitude);
            set.PreAttributeBaseChange(attribute, ref newBase);
            data.BaseValue = newBase;
            RecalculateCurrentValue(set, attribute, source);
        }

        private static float ApplyOp(float current, EAttributeModifierOp op, float magnitude)
        {
            switch (op)
            {
                case EAttributeModifierOp.Add: return current + magnitude;
                case EAttributeModifierOp.Multiply: return current * magnitude;
                case EAttributeModifierOp.Divide: return Mathf.Approximately(magnitude, 0f) ? current : current / magnitude;
                case EAttributeModifierOp.Override: return magnitude;
                default: return current;
            }
        }

        // ---- 当前值聚合（base + 所有激活的 Duration/Infinite 修改）----
        private void RecalculateAffectedAttributes(GameplayEffect def, GameplayEffectContext source = null)
        {
            if (def == null) return;
            foreach (var mod in def.Modifiers)
            {
                var set = GetAttributeSet(mod.Attribute.AttributeSetTypeName);
                if (set != null) RecalculateCurrentValue(set, mod.Attribute, source);
            }
        }

        private void RecalculateCurrentValue(AttributeSet set, GameplayAttribute attribute, GameplayEffectContext source = null)
        {
            var data = set.GetAttributeData(attribute.AttributeName);
            if (data == null) return;

            float baseV = data.BaseValue;
            float sumAdd = 0f, prodMul = 1f, prodDiv = 1f;
            bool hasOverride = false; float overrideV = 0f;

            foreach (var active in _activeEffects)
            {
                if (active.Inhibited) continue;
                int stacks = active.StackCount; // 按层数放大：等价于该修改施加 stacks 次
                foreach (var mod in active.Def.Modifiers)
                {
                    if (!mod.Attribute.Equals(attribute)) continue;
                    float mag = mod.Magnitude.Evaluate(active.Spec);
                    switch (mod.Operation)
                    {
                        case EAttributeModifierOp.Add: sumAdd += mag * stacks; break;
                        case EAttributeModifierOp.Multiply: prodMul *= Mathf.Pow(mag, stacks); break;
                        case EAttributeModifierOp.Divide: if (!Mathf.Approximately(mag, 0f)) prodDiv *= Mathf.Pow(mag, stacks); break;
                        case EAttributeModifierOp.Override: hasOverride = true; overrideV = mag; break;
                    }
                }
            }

            float result = (baseV + sumAdd) * prodMul / (Mathf.Approximately(prodDiv, 0f) ? 1f : prodDiv);
            if (hasOverride) result = overrideV;

            // PreAttributeChange clamp 钩子
            set.PreAttributeChange(attribute, ref result);

            float old = data.CurrentValue;
            if (!Mathf.Approximately(old, result))
            {
                data.CurrentValue = result;
                set.PostAttributeChange(attribute, old, result);
                OnAttributeChanged?.Invoke(new AttributeChangeData(attribute, old, result, source));
            }
        }

        /// <summary>检查能否承担某效果的属性修改（消耗检查：扣后不为负）。</summary>
        public bool CanApplyAttributeModifiers(GameplayEffect effect, int level)
        {
            if (effect == null) return true;
            var spec = MakeOutgoingSpec(effect, level);
            foreach (var mod in effect.Modifiers)
            {
                var data = mod.Attribute.ResolveData(this);
                if (data == null) continue;
                float result = ApplyOp(data.BaseValue, mod.Operation, mod.Magnitude.Evaluate(spec));
                if (result < 0f) return false;
            }
            return true;
        }

        /// <summary>查询某组冷却标签的剩余时间。</summary>
        public bool GetCooldownRemainingForTags(IEnumerable<GameplayTag> cooldownTags, out float timeRemaining, out float duration)
        {
            timeRemaining = 0f; duration = 0f;
            bool found = false;
            foreach (var active in _activeEffects)
            {
                foreach (var granted in active.Def.GrantedTags)
                {
                    foreach (var ct in cooldownTags)
                    {
                        if (granted.MatchesTag(ct))
                        {
                            found = true;
                            if (active.TimeRemaining > timeRemaining)
                            {
                                timeRemaining = active.TimeRemaining;
                                duration = active.Def.Duration;
                            }
                        }
                    }
                }
            }
            return found;
        }

        // ===================== AbilityLoadout 批量授予 =====================
        /// <summary>授予一个 AbilityLoadout（技能 + 效果 + 属性集）。</summary>
        public GrantedAbilityHandles GrantLoadout(AbilityLoadout set)
        {
            var handles = new GrantedAbilityHandles();
            if (set == null) return handles;

            // 属性集（强类型引用）：按所选模板的实际类型新建独立实例加入本 ASC
            foreach (var template in set.GrantedAttributeSets)
            {
                if (template == null) continue;
                if (Activator.CreateInstance(template.GetType()) is AttributeSet attrSet)
                {
                    AddAttributeSet(attrSet);
                    handles.AddedAttributeSets.Add(attrSet);
                }
            }

            // 常驻效果（含属性初始化 GE）：按 attributeInitializeLevel 施加（曲线表 magnitude 据此查值）
            foreach (var ge in set.GrantedEffects)
            {
                if (ge == null) continue;
                var h = ApplyGameplayEffectToSelf(ge, attributeInitializeLevel);
                if (h.IsValid) handles.EffectHandles.Add(h);
            }

            // 技能
            foreach (var ga in set.GrantedAbilities)
            {
                if (ga.Ability == null) continue;
                handles.AbilityHandles.Add(GiveAbility(ga.Ability, Mathf.Max(1, ga.Level), ga.DynamicTags));
            }

            return handles;
        }

        // ===================== 游戏事件 GameplayEvent =====================
        /// <summary>监听任意游戏事件（输入系统 / AbilityTask WaitGameplayEvent 用）。</summary>
        public event Action<GameplayTag, GameplayEventData> OnGameplayEvent;

        /// <summary>广播一个游戏事件，并尝试激活以该 tag 为身份标签的技能。</summary>
        public void SendGameplayEvent(GameplayTag eventTag, GameplayEventData data = null)
        {
            data ??= new GameplayEventData(eventTag);
            data.EventTag = eventTag;
            OnGameplayEvent?.Invoke(eventTag, data);
        }

        // ===================== GameplayCue（表现钩子） =====================
        /// <summary>瞬时执行一个 cue（命中特效等）。</summary>
        public void ExecuteGameplayCue(GameplayTag cueTag, GameplayCueParameters parameters = null)
            => GameplayCueManager.Instance.HandleGameplayCue(gameObject, cueTag, EGameplayCueEvent.Executed, parameters);

        /// <summary>开始一个持续 cue（持续型效果施加时）。</summary>
        public void AddGameplayCue(GameplayTag cueTag, GameplayCueParameters parameters = null)
            => GameplayCueManager.Instance.HandleGameplayCue(gameObject, cueTag, EGameplayCueEvent.OnActive, parameters);

        /// <summary>结束一个持续 cue（持续型效果移除时）。</summary>
        public void RemoveGameplayCue(GameplayTag cueTag, GameplayCueParameters parameters = null)
            => GameplayCueManager.Instance.HandleGameplayCue(gameObject, cueTag, EGameplayCueEvent.Removed, parameters);

        // 从效果 spec 构造 cue 参数（命中点/来源）
        private GameplayCueParameters MakeCueParams(GameplayEffectSpec spec)
        {
            var ctx = spec?.Context;
            return new GameplayCueParameters
            {
                Instigator = ctx?.Instigator,
                SourceObject = ctx?.EffectCauser,
                Location = ctx != null && ctx.HasHitLocation ? ctx.HitLocation : transform.position,
                EffectContext = ctx
            };
        }

        // ===================== Tick =====================
        protected virtual void Update()
        {
            float dt = Time.deltaTime;
            TickActiveAbilities(dt);
            TickActiveEffects(dt);
        }

        // 驱动激活中、且 EnableTick=true 的技能逐帧回调（对齐 UE AbilityTick）。
        // 快照迭代：AbilityTick 内可能结束/授予技能而改动集合。
        private readonly List<GameplayAbility> _tickScratch = new List<GameplayAbility>();
        private void TickActiveAbilities(float dt)
        {
            if (_abilities.Count == 0) return;
            _tickScratch.Clear();
            foreach (var spec in _abilities.Values)
                if (spec.IsActive && spec.Ability != null && spec.Ability.EnableTick)
                    _tickScratch.Add(spec.Ability);
            for (int i = 0; i < _tickScratch.Count; i++)
                if (_tickScratch[i].IsActive) _tickScratch[i].AbilityTick(dt);
        }

        private readonly List<ActiveGameplayEffect> _expiredScratch = new List<ActiveGameplayEffect>();
        private void TickActiveEffects(float dt)
        {
            if (_activeEffects.Count == 0) return;
            _expiredScratch.Clear();

            // 用索引遍历快照，避免在迭代时被周期结算改动
            for (int i = 0; i < _activeEffects.Count; i++)
            {
                var active = _activeEffects[i];

                // Ongoing 标签条件 -> 抑制
                UpdateInhibition(active);

                // 周期结算（叠层效果按层放大结算量）
                if (active.Def.IsPeriodic && !active.Inhibited)
                {
                    active.PeriodRemaining -= dt;
                    while (active.PeriodRemaining <= 0f)
                    {
                        ExecuteEffectSpec(active.Spec, active.StackCount); // 按 Instant 语义改基础值
                        active.PeriodRemaining += active.Def.Period;
                    }
                }

                // 时长推进
                if (active.Def.DurationType == EGameplayEffectDurationType.HasDuration)
                {
                    active.TimeRemaining -= dt;
                    if (active.TimeRemaining <= 0f) HandleDurationExpired(active);
                }
            }

            foreach (var expired in _expiredScratch)
                RemoveActiveGameplayEffect(expired.Handle);
        }

        // 限时效果到期：不可叠层 / 整组清空 → 整体移除；否则按叠层到期策略掉层或仅刷新
        private void HandleDurationExpired(ActiveGameplayEffect active)
        {
            var def = active.Def;
            if (!def.IsStackable || def.StackExpirationPolicy == EGameplayEffectStackingExpirationPolicy.ClearEntireStack)
            {
                _expiredScratch.Add(active);
                return;
            }

            if (def.StackExpirationPolicy == EGameplayEffectStackingExpirationPolicy.RemoveSingleStackAndRefreshDuration)
            {
                if (active.StackCount > 1)
                {
                    int old = active.StackCount;
                    active.StackCount = old - 1;
                    active.TimeRemaining = def.Duration; // 刷新，下一层继续计时
                    RecalculateAffectedAttributes(def);
                    OnActiveEffectStackChanged?.Invoke(active, old, active.StackCount);
                }
                else _expiredScratch.Add(active); // 最后一层 → 整体移除
            }
            else // RefreshDuration：不掉层，仅刷新（需显式移除）
            {
                active.TimeRemaining = def.Duration;
            }
        }

        // 根据 Ongoing 标签要求更新抑制态，状态翻转时重算当前值
        private void UpdateInhibition(ActiveGameplayEffect active)
        {
            var required = active.Def.OngoingRequiredTags;
            bool shouldInhibit = false;
            if (required != null && required.Count > 0)
            {
                foreach (var t in required)
                    if (!HasMatchingGameplayTag(t)) { shouldInhibit = true; break; }
            }
            if (active.Inhibited != shouldInhibit)
            {
                active.Inhibited = shouldInhibit;
                RecalculateAffectedAttributes(active.Def);
            }
        }
    }
}
