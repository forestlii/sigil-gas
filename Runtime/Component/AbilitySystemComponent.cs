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

        /// <summary>
        /// 销毁时回收随实例克隆的技能 SO（及其 AdditionalCosts 克隆）。这些克隆标了 HideAndDontSave，
        /// 不随场景卸载/资源回收，ASC 被 Destroy（刷怪、换场景）时不清就会在真机包体里逐个泄漏。
        /// 只在 Play 模式做运行时销毁——编辑器/EditMode 测试下 DestroyImmediate(go) 会同步进本回调，
        /// 此时调 Object.Destroy 会报 "Destroy may not be called from edit mode"，故跳过（编辑器泄漏无害）。
        /// </summary>
        protected virtual void OnDestroy()
        {
            if (Application.isPlaying && _abilities.Count > 0)
            {
                var specs = new List<GameplayAbilitySpec>(_abilities.Values);
                _abilities.Clear();
                foreach (var spec in specs)
                {
                    var ability = spec.Ability;
                    if (ability == null) continue;
                    if (spec.IsActive) ability.CancelAbility();
                    foreach (var c in ability.AdditionalCosts)
                        if (c != null) UnityEngine.Object.Destroy(c);
                    UnityEngine.Object.Destroy(ability);
                }
            }
            if (_abilityTriggerHooked)
            {
                _ownedTags.OnTagCountChanged -= HandleOwnedTagTriggerChanged;
                _abilityTriggerHooked = false;
            }
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
            // 拒绝同类型重复：GetAttributeSet 按类型全名解析、只命中第一个，重复实例会让后加的那套读写
            // 静默无效（"属性有时生效有时不生效"级难查 bug）。两个 Loadout 勾同一属性集类型即会触发。
            if (GetAttributeSet(set.GetType().FullName) != null)
            {
                Debug.LogWarning($"[Sigil] AddAttributeSet 忽略了重复的属性集类型 {set.GetType().Name}：同类型已存在，" +
                                 "属性解析只会命中第一个实例。请勿在多个 Loadout 勾选同一属性集类型。");
                return;
            }
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

        /// <summary>
        /// 按属性名在所持有的所有属性集里查该属性数据（找不到返回 null）。
        /// 让框架系统（削韧/死亡过滤/伤害执行等）无需引用具体属性集类型即可读属性——
        /// 属性只由属性名标识，配哪个集都能用（含编辑器生成的属性集）。
        /// </summary>
        public GameplayAttributeData GetAttributeDataByName(string attributeName)
        {
            if (string.IsNullOrEmpty(attributeName)) return null;
            foreach (var set in _attributeSets)
            {
                var d = set.GetAttributeData(attributeName);
                if (d != null) return d;
            }
            return null;
        }

        /// <summary>按属性名解析出一个 <see cref="GameplayAttribute"/> 句柄（找不到返回默认无效句柄）。</summary>
        public GameplayAttribute FindAttributeByName(string attributeName)
        {
            if (string.IsNullOrEmpty(attributeName)) return default;
            foreach (var set in _attributeSets)
                if (set.GetAttributeData(attributeName) != null)
                    return set.GetAttribute(attributeName);
            return default;
        }

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

            EnsureAbilityTriggerHook(); // 惰性订阅 owned-tag 变化，驱动 tag 触发源（不依赖 Awake，EditMode 也生效）

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

        // 借还式已授予技能快照池：避免每次事件/激活遍历都 new 一个 List。
        // 重入安全——每层借一个不同 list（激活链可能重入这些方法）；稳态预热后零分配。
        private readonly Stack<List<GameplayAbilitySpec>> _specSnapshotPool = new Stack<List<GameplayAbilitySpec>>();
        private List<GameplayAbilitySpec> RentSpecSnapshot()
        {
            var list = _specSnapshotPool.Count > 0 ? _specSnapshotPool.Pop() : new List<GameplayAbilitySpec>();
            list.Clear();
            list.AddRange(_abilities.Values);
            return list;
        }
        private void ReturnSpecSnapshot(List<GameplayAbilitySpec> list)
        {
            list.Clear();
            _specSnapshotPool.Push(list);
        }

        /// <summary>尝试激活所有 AbilityTags 命中该 tag 的已授予技能。</summary>
        public bool TryActivateAbilitiesByTag(GameplayTag tag, GameplayEventData triggerData = null)
        {
            bool any = false;
            var specs = RentSpecSnapshot(); // 借用快照，避免激活过程中集合被改（重入安全）
            try
            {
                foreach (var spec in specs)
                    if (spec.Ability.GetAbilityTags().HasTag(tag))
                        any |= TryActivateSpec(spec, triggerData);
            }
            finally { ReturnSpecSnapshot(specs); }
            return any;
        }

        /// <summary>尝试激活第一个属于指定类型（或其子类）的已授予技能（对齐 UE TryActivateAbilityByClass）。</summary>
        public bool TryActivateAbilityByClass(Type abilityType, GameplayEventData triggerData = null)
        {
            if (abilityType == null) return false;
            var specs = RentSpecSnapshot(); // 快照：激活可能改动集合（重入安全）
            try
            {
                foreach (var spec in specs)
                    if (spec.Ability != null && abilityType.IsInstanceOfType(spec.Ability))
                        return TryActivateSpec(spec, triggerData);
            }
            finally { ReturnSpecSnapshot(specs); }
            return false;
        }

        /// <summary>泛型便捷重载。</summary>
        public bool TryActivateAbilityByClass<T>(GameplayEventData triggerData = null) where T : GameplayAbility
            => TryActivateAbilityByClass(typeof(T), triggerData);

        /// <summary>
        /// 从一个 GameObject 解析出 ASC：优先它实现的 <see cref="IAbilitySystemInterface"/>，
        /// 否则退回 GetComponent（对齐 UE GetAbilitySystemComponentFromActor）。
        /// ASC 不在本对象上（挂在子物体/伙伴对象）时用接口指路。
        /// </summary>
        public static AbilitySystemComponent GetAbilitySystem(GameObject actor)
        {
            if (actor == null) return null;
            if (actor.TryGetComponent<IAbilitySystemInterface>(out var provider))
            {
                var asc = provider.GetAbilitySystemComponent();
                if (asc != null) return asc;
            }
            return actor.GetComponent<AbilitySystemComponent>();
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
                var snapshot = RentSpecSnapshot();
                try
                {
                    foreach (var spec in snapshot)
                    {
                        if (spec.Ability == ability || !spec.Ability.IsActive) continue;
                        if (_cancelTagsCache.HasAny(spec.Ability.GetAbilityTags()))
                            spec.Ability.CancelAbility();
                    }
                }
                finally { ReturnSpecSnapshot(snapshot); }
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

        // ===================== 重入安全：作用域锁 + 延迟队列 =====================
        // 效果结算 / 属性变化钩子里，用户 Apply/Remove 效果是合理写法（反伤、斩杀链、受伤驱散护盾）。
        // 若当场执行会破坏正在进行的遍历/结算：跳 tick、对已删对象继续跑、无限递归爆栈。
        // 方案：正处于结算作用域（_scopeDepth>0）时，Apply/Remove 入队，退到最外层再统一 flush。
        // 对齐 UE 的 FScopedAbilityListLock + 延迟移除（见 MD/design/重入安全-延迟队列方案.md）。
        private int _scopeDepth;
        private bool _flushingDeferred;
        private const int MaxDeferredOpsPerFlush = 256; // 兜底：回调自喂自导致队列失控时中止并报错
        private readonly Queue<DeferredEffectOp> _deferredEffectOps = new Queue<DeferredEffectOp>();

        private readonly struct DeferredEffectOp
        {
            public readonly bool IsApply; // true=Apply(Spec)，false=Remove(Handle)
            public readonly GameplayEffectSpec Spec;
            public readonly ActiveGameplayEffectHandle Handle;
            private DeferredEffectOp(bool isApply, GameplayEffectSpec spec, ActiveGameplayEffectHandle handle)
            { IsApply = isApply; Spec = spec; Handle = handle; }
            public static DeferredEffectOp Apply(GameplayEffectSpec s) => new DeferredEffectOp(true, s, default);
            public static DeferredEffectOp Remove(ActiveGameplayEffectHandle h) => new DeferredEffectOp(false, null, h);
        }

        // 退出一层作用域；退到最外层时把攒下的延迟操作一次性执行掉。
        private void LeaveScope()
        {
            _scopeDepth--;
            if (_scopeDepth == 0 && _deferredEffectOps.Count > 0) FlushDeferredEffects();
        }

        private void FlushDeferredEffects()
        {
            if (_flushingDeferred) return;
            _flushingDeferred = true;
            _scopeDepth++; // flush 期间执行 op 触发的回调仍入队，由下面的 while 收敛处理
            try
            {
                int guard = 0;
                while (_deferredEffectOps.Count > 0)
                {
                    if (++guard > MaxDeferredOpsPerFlush)
                    {
                        Debug.LogError("[Sigil] 效果延迟队列超限，疑似结算/属性钩子里无限自施加，已丢弃剩余操作。");
                        _deferredEffectOps.Clear();
                        break;
                    }
                    var op = _deferredEffectOps.Dequeue();
                    if (op.IsApply) ApplyGameplayEffectSpecToSelf_Core(op.Spec);
                    else RemoveActiveGameplayEffect_Core(op.Handle);
                }
            }
            finally { _scopeDepth--; _flushingDeferred = false; }
        }

        /// <summary>
        /// 给自己施加一个效果 spec。
        /// ⚠️ 在效果结算 / 属性变化钩子内调用时（重入），本次会被延迟到当前作用域结束再执行，此时返回 Invalid 句柄
        /// （句柄需在钩子外或经 OnActiveEffectAdded 事件获取）。稳态（非重入）路径行为不变。
        /// </summary>
        public ActiveGameplayEffectHandle ApplyGameplayEffectSpecToSelf(GameplayEffectSpec spec)
        {
            if (spec == null || spec.Def == null) return ActiveGameplayEffectHandle.Invalid;
            if (_scopeDepth > 0) { _deferredEffectOps.Enqueue(DeferredEffectOp.Apply(spec)); return ActiveGameplayEffectHandle.Invalid; }
            _scopeDepth++;
            try { return ApplyGameplayEffectSpecToSelf_Core(spec); }
            finally { LeaveScope(); }
        }

        // 实际施加逻辑（不含作用域/延迟管理）；直接调用与 flush 都走它。spec 已由入口保证非空。
        private ActiveGameplayEffectHandle ApplyGameplayEffectSpecToSelf_Core(GameplayEffectSpec spec)
        {
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

#if UNITY_EDITOR
            WarnIfDurationModifiersTargetMeta(def); // meta 属性防误用（对齐 UE HideFromModifiers 的意图）
#endif

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

        /// <summary>
        /// 移除一个激活效果。
        /// ⚠️ 在结算 / 属性变化钩子内调用时（重入），本次移除会被延迟到当前作用域结束再执行，返回 true 表示"已受理"。
        /// </summary>
        public bool RemoveActiveGameplayEffect(ActiveGameplayEffectHandle handle)
        {
            if (_scopeDepth > 0) { _deferredEffectOps.Enqueue(DeferredEffectOp.Remove(handle)); return true; }
            _scopeDepth++;
            try { return RemoveActiveGameplayEffect_Core(handle); }
            finally { LeaveScope(); }
        }

        private bool RemoveActiveGameplayEffect_Core(ActiveGameplayEffectHandle handle)
        {
            for (int i = 0; i < _activeEffects.Count; i++)
            {
                if (_activeEffects[i].Handle.Equals(handle))
                {
                    var active = _activeEffects[i];
                    _activeEffects.RemoveAt(i);
                    // 若效果当前处于抑制态，其授予标签/持续 cue 已被 UpdateInhibition 撤下 → 不能再撤一次（否则计数双减）。
                    if (!active.Inhibited)
                    {
                        foreach (var t in active.Def.GrantedTags) RemoveLooseGameplayTag(t);
                        foreach (var cue in active.Def.GameplayCues) RemoveGameplayCue(cue, MakeCueParams(active.Spec));
                    }
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
                // 框架内部、自包含的反向遍历移除 → 直接走 _Core 立即执行，保持"先移旧再加新"的顺序
                // （不入延迟队列，否则会与随后新增的效果乱序）。
                if (match) RemoveActiveGameplayEffect_Core(_activeEffects[i].Handle);
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

#if UNITY_EDITOR
        // meta 属性只该被 Instant/Execution 写入并清零；Duration/Infinite 的 modifier 挂上它会破坏 meta 管线。
        // 对齐 UE HideFromModifiers 的防误用意图——本项目无属性选择下拉 UI，改为施加持续效果时警告。
        private void WarnIfDurationModifiersTargetMeta(GameplayEffect def)
        {
            foreach (var mod in def.Modifiers)
            {
                var set = GetAttributeSet(mod.Attribute.AttributeSetTypeName);
                if (set != null && set.IsMeta(mod.Attribute.AttributeName))
                    Debug.LogWarning($"[Sigil] 持续型效果 '{def.name}' 的 modifier 指向 meta 属性 '{mod.Attribute.AttributeName}'——meta 属性应只被 Instant/Execution 写入，挂在持续 modifier 上会破坏 meta 管线（对齐 UE HideFromModifiers）。");
            }
        }
#endif

        // 立即结算一个效果（Instant 或 周期一次）：改基础值 + 执行计算 + PostGameplayEffectExecute 钩子
        // stackCount：周期性叠层效果按层放大结算量（加法语义；其它运算符周期叠层罕见，按单次处理）
        private void ExecuteEffectSpec(GameplayEffectSpec spec, int stackCount = 1)
        {
            var def = spec.Def;

            // 收集所有要施加的增量：普通修改 + 自定义执行
            var outputs = new List<GameplayExecutionOutput>();
            foreach (var mod in def.Modifiers)
            {
                float mag = mod.Magnitude.Evaluate(spec, spec.Context?.SourceASC, this);
                if (mod.Operation == EAttributeModifierOp.Add) mag *= stackCount; // 按层放大加法结算
                outputs.Add(new GameplayExecutionOutput(mod.Attribute, mag, mod.Operation));
            }

            foreach (var exec in def.Executions)
                if (exec != null) exec.Execute(spec, spec.Context?.SourceASC, this, outputs);

            foreach (var output in outputs)
            {
                var set = GetAttributeSet(output.Attribute.AttributeSetTypeName);
                var data = output.Attribute.ResolveData(this);
                if (set == null || data == null)
                {
#if UNITY_EDITOR
                    // 静默丢弃修饰是难查配置错误的常见来源（如 Loadout A 的初始化 GE 引用了 Loadout B 才添加的属性集）。
                    Debug.LogWarning($"[Sigil] 效果 {(def != null ? def.name : "?")} 的 modifier 指向未注册的属性 " +
                        $"{output.Attribute.AttributeSetTypeName}.{output.Attribute.AttributeName}，已忽略——检查属性集是否已添加、Loadout 授予顺序。");
#endif
                    continue;
                }

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
                set.PostAttributeBaseChange(output.Attribute, oldBase, newBase); // BaseValue 改后钩子（对齐 UE）

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
            float oldBase = data.BaseValue;
            float newBase = ApplyOp(oldBase, op, magnitude);
            set.PreAttributeBaseChange(attribute, ref newBase);
            data.BaseValue = newBase;
            set.PostAttributeBaseChange(attribute, oldBase, newBase); // BaseValue 改后钩子（对齐 UE）
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
#if UNITY_EDITOR
                else
                    Debug.LogWarning($"[Sigil] 效果 {def.name} 的 modifier 指向未注册的属性集 " +
                        $"{mod.Attribute.AttributeSetTypeName}，本次重算已跳过——检查属性集是否已添加。");
#endif
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
                // 周期效果按 Instant 语义每周期落 BaseValue（见 TickActiveEffects），其 modifier 不参与
                // CurrentValue 聚合——否则同一 magnitude 会被算两次（持续修饰 + 周期落基础值）。
                if (active.Def.IsPeriodic) continue;
                int stacks = active.StackCount; // 按层数放大：等价于该修改施加 stacks 次
                foreach (var mod in active.Def.Modifiers)
                {
                    if (!mod.Attribute.Equals(attribute)) continue;
                    float mag = mod.Magnitude.Evaluate(active.Spec, active.Spec.Context?.SourceASC, this);
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
                float result = ApplyOp(data.BaseValue, mod.Operation, mod.Magnitude.Evaluate(spec, spec.Context?.SourceASC, this));
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
            TriggerAbilitiesFromEvent(eventTag, data); // AbilityTriggers：GameplayEvent 源自动激活
        }

        // ===================== 技能事件触发 Ability Triggers =====================
        // 对齐 UE：授予的技能声明 AbilityTriggers（GameplayEvent / OwnedTagAdded / OwnedTagPresent），
        // ASC 集中匹配并自动激活。中心遍历 _abilities（ASC 本就持有全部技能），
        // 故无需 per-ability 事件订阅，从根上避免委托注销泄漏。

        private bool _abilityTriggerHooked;

        // 惰性订阅一次 owned-tag 存在性翻转事件，驱动 OwnedTagAdded/OwnedTagPresent 触发源。
        private void EnsureAbilityTriggerHook()
        {
            if (_abilityTriggerHooked) return;
            _ownedTags.OnTagCountChanged += HandleOwnedTagTriggerChanged;
            _abilityTriggerHooked = true;
        }

        // GameplayEvent 源：激活所有 TriggerTag 层级命中该事件的已授予技能，并把事件数据作为 triggerData 传入。
        private void TriggerAbilitiesFromEvent(GameplayTag eventTag, GameplayEventData data)
        {
            if (!eventTag.IsValid || _abilities.Count == 0) return;
            var specs = RentSpecSnapshot(); // 快照：激活可能改动集合（重入安全）
            try
            {
                foreach (var spec in specs)
                {
                    var triggers = spec.Ability != null ? spec.Ability.AbilityTriggers : null;
                    if (triggers == null) continue;
                    for (int i = 0; i < triggers.Count; i++)
                    {
                        var trig = triggers[i];
                        if (trig == null || !trig.IsValid || trig.TriggerSource != EGameplayAbilityTriggerSource.GameplayEvent) continue;
                        if (eventTag.MatchesTag(trig.TriggerTag)) // 层级匹配：事件 tag 命中（等于/子于）监听 tag
                        {
                            TryActivateSpec(spec, data);
                            break; // 本技能已触发，避免多条匹配 trigger 重复激活
                        }
                    }
                }
            }
            finally { ReturnSpecSnapshot(specs); }
        }

        // OwnedTag 源：标签存在性翻转时激活 / 取消。
        // 标签容器按父链逐级 fire，故用精确 Equals 匹配即可命中且不重复。
        private void HandleOwnedTagTriggerChanged(GameplayTag tag, bool isPresent)
        {
            if (_abilities.Count == 0) return;
            var specs = RentSpecSnapshot(); // 快照：激活/取消可能改动集合与标签（重入安全）
            try
            {
                foreach (var spec in specs)
                {
                    var triggers = spec.Ability != null ? spec.Ability.AbilityTriggers : null;
                    if (triggers == null) continue;
                    for (int i = 0; i < triggers.Count; i++)
                    {
                        var trig = triggers[i];
                        if (trig == null || !trig.IsValid || trig.TriggerSource == EGameplayAbilityTriggerSource.GameplayEvent) continue;
                        if (!tag.Equals(trig.TriggerTag)) continue;

                        if (isPresent)
                            TryActivateSpec(spec, null); // 标签出现 → 激活（AlreadyActive 会被 TryActivateSpec 挡掉，不重复）
                        else if (trig.TriggerSource == EGameplayAbilityTriggerSource.OwnedTagPresent && spec.Ability.IsActive)
                            spec.Ability.CancelAbility(); // OwnedTagPresent：标签消失 → 取消
                    }
                }
            }
            finally { ReturnSpecSnapshot(specs); }
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

            // 整个 tick 进一个结算作用域：周期结算/抑制翻转触发的用户钩子里若 Apply/Remove 效果，
            // 会入延迟队列而非当场改 _activeEffects → 本循环始终在稳定列表上跑（不跳 tick、不递归爆栈）。
            _scopeDepth++;
            try
            {
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
                            if (active.Def.Period <= 0f) break; // 兜底：运行时资产被改成 0 时防死循环
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

                // 到期移除（此时仍在作用域内 → 入延迟队列，随下面 LeaveScope 的 flush 一并执行）
                foreach (var expired in _expiredScratch)
                    RemoveActiveGameplayEffect(expired.Handle);
            }
            finally { LeaveScope(); }
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
                // 抑制语义是"效果暂时不生效"：不仅属性修饰要回退（聚合时已按 Inhibited 跳过），
                // 授予标签与持续 cue 也要一并撤下/恢复——否则被抑制的定身效果属性放开了、State.Rooted 标签还挂着，
                // 所有按标签判断的系统仍认为目标定身中（标签与属性表现矛盾）。
                if (shouldInhibit)
                {
                    foreach (var t in active.Def.GrantedTags) RemoveLooseGameplayTag(t);
                    foreach (var cue in active.Def.GameplayCues) RemoveGameplayCue(cue, MakeCueParams(active.Spec));
                }
                else
                {
                    foreach (var t in active.Def.GrantedTags) AddLooseGameplayTag(t);
                    foreach (var cue in active.Def.GameplayCues) AddGameplayCue(cue, MakeCueParams(active.Spec));
                }
                RecalculateAffectedAttributes(active.Def);
            }
        }
    }
}
