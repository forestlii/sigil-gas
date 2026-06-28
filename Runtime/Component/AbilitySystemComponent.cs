// Copyright 2026 Likeon All Rights Reserved.
// GAS 中枢。
// 挂在角色 GameObject 上，统管：拥有标签 / 属性集 / 效果 / 技能授予与激活 / 激活组互斥 / 标签关系。
// 诚实声明：本阶段为单机权威实现。
// 但所有"会改状态"的入口都集中在此，便于阶段 6 接 Netcode for GameObjects 时统一加 authority 判断。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Likeon.GAS
{
    [AddComponentMenu("Likeon/GAS/Ability System Component")]
    public class AbilitySystemComponent : MonoBehaviour
    {
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

        private readonly GameplayTagContainer _ownedTagsCache = new GameplayTagContainer();
        private GameplayTagContainer OwnedTagsSnapshot()
        {
            GetOwnedGameplayTags(_ownedTagsCache);
            return _ownedTagsCache;
        }

        // ===================== 属性集 Attribute Sets =====================
        private readonly List<AttributeSet> _attributeSets = new List<AttributeSet>();

        /// <summary>(被改的属性, 旧值, 新值) —— 当前值变化时触发，UI/表现订阅。</summary>
        public event Action<GameplayAttribute, float, float> OnAttributeChanged;

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

        public float GetAttributeValue(GameplayAttribute attribute) => attribute.GetCurrentValue(this);
        public float GetAttributeBaseValue(GameplayAttribute attribute) => attribute.GetBaseValue(this);

        // ===================== 技能授予与激活 Abilities =====================
        private readonly Dictionary<int, GameplayAbilitySpec> _abilities = new Dictionary<int, GameplayAbilitySpec>();
        private int _nextAbilityId = 1;

        public event Action<GameplayAbility> OnAbilityActivated;
        public event Action<GameplayAbility, bool> OnAbilityEnded;

        /// <summary>授予一个技能（克隆模板成本角色实例）。</summary>
        public GameplayAbilitySpecHandle GiveAbility(GameplayAbility abilityTemplate, int level = 1)
        {
            if (abilityTemplate == null) return GameplayAbilitySpecHandle.Invalid;

            var instance = UnityEngine.Object.Instantiate(abilityTemplate); // InstancedPerActor：每角色独立实例
            instance.hideFlags = HideFlags.HideAndDontSave;
            var handle = new GameplayAbilitySpecHandle(_nextAbilityId++);
            var spec = new GameplayAbilitySpec(handle, instance, level);
            instance.ASC = this;
            instance.Spec = spec;
            _abilities[handle.Id] = spec;
            return handle;
        }

        /// <summary>移除一个技能。</summary>
        public void ClearAbility(GameplayAbilitySpecHandle handle)
        {
            if (_abilities.TryGetValue(handle.Id, out var spec))
            {
                if (spec.IsActive) spec.Ability.CancelAbility();
                _abilities.Remove(handle.Id);
                if (spec.Ability != null) UnityEngine.Object.Destroy(spec.Ability);
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
            if (ability.IsActive) return false;
            if (!ability.CanActivate()) return false;

            // 独占技能激活：先打断可替换的独占技能
            if (ability.ActivationPolicy != EAbilityActivationPolicy.Parallel)
                CancelAbilitiesWithPolicy(EAbilityActivationPolicy.Replaceable, ability);

            // 按 TagRelationship 取消互斥技能
            CancelConflictingAbilities(ability);

            ability.Activate(triggerData);
            OnAbilityActivated?.Invoke(ability);
            return true;
        }

        internal void NotifyAbilityEnded(GameplayAbility ability, bool wasCancelled)
            => OnAbilityEnded?.Invoke(ability, wasCancelled);

        // ---- 激活组互斥 ----
        private readonly List<GameplayAbility>[] _policyBuckets =
        {
            new List<GameplayAbility>(), // Independent
            new List<GameplayAbility>(), // Replaceable
            new List<GameplayAbility>()  // Blocking
        };

        /// <summary>该激活组当前是否被阻挡。</summary>
        public bool IsActivationPolicyBlocked(EAbilityActivationPolicy group)
        {
            switch (group)
            {
                case EAbilityActivationPolicy.Parallel:
                    return false; // 独立技能永不被阻挡
                case EAbilityActivationPolicy.Replaceable:
                    // 仅被 Blocking 阻挡
                    return _policyBuckets[(int)EAbilityActivationPolicy.Blocking].Count > 0;
                case EAbilityActivationPolicy.Blocking:
                    // 被任意独占技能阻挡
                    return _policyBuckets[(int)EAbilityActivationPolicy.Replaceable].Count > 0
                        || _policyBuckets[(int)EAbilityActivationPolicy.Blocking].Count > 0;
                default:
                    return false;
            }
        }

        public void RegisterAbilityPolicy(EAbilityActivationPolicy group, GameplayAbility ability)
        {
            if (group == EAbilityActivationPolicy.MAX) return;
            var list = _policyBuckets[(int)group];
            if (!list.Contains(ability)) list.Add(ability);
        }

        public void UnregisterAbilityPolicy(EAbilityActivationPolicy group, GameplayAbility ability)
        {
            if (group == EAbilityActivationPolicy.MAX) return;
            _policyBuckets[(int)group].Remove(ability);
        }

        /// <summary>取消某激活组里的技能（可忽略一个）。</summary>
        public void CancelAbilitiesWithPolicy(EAbilityActivationPolicy group, GameplayAbility ignore)
        {
            if (group == EAbilityActivationPolicy.MAX) return;
            var snapshot = new List<GameplayAbility>(_policyBuckets[(int)group]);
            foreach (var ab in snapshot)
                if (ab != ignore && ab.IsActive) ab.CancelAbility();
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

        // 按关系映射取消互斥技能
        private readonly GameplayTagContainer _blockTagsCache = new GameplayTagContainer();
        private readonly GameplayTagContainer _cancelTagsCache = new GameplayTagContainer();
        private void CancelConflictingAbilities(GameplayAbility ability)
        {
            if (InteractionRules == null) return;
            _blockTagsCache.Clear();
            _cancelTagsCache.Clear();
            InteractionRules.CollectBlockedAndCanceledTags(OwnedTagsSnapshot(), ability.GetAbilityTags(), _blockTagsCache, _cancelTagsCache);
            if (_cancelTagsCache.IsEmpty) return;

            var snapshot = new List<GameplayAbilitySpec>(_abilities.Values);
            foreach (var spec in snapshot)
            {
                if (spec.Ability == ability || !spec.Ability.IsActive) continue;
                if (_cancelTagsCache.HasAny(spec.Ability.GetAbilityTags()))
                    spec.Ability.CancelAbility();
            }
        }

        // ===================== 效果 Effects =====================
        private readonly List<ActiveGameplayEffect> _activeEffects = new List<ActiveGameplayEffect>();
        private int _nextEffectId = 1;

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

            // Duration / Infinite：登记为激活效果
            var handle = new ActiveGameplayEffectHandle(_nextEffectId++);
            var active = new ActiveGameplayEffect(handle, spec);
            _activeEffects.Add(active);

            // 授予标签
            foreach (var t in def.GrantedTags) AddLooseGameplayTag(t);
            // 持续型 cue 开始
            foreach (var cue in def.GameplayCues) AddGameplayCue(cue, MakeCueParams(spec));

            UpdateInhibition(active);
            RecalculateAffectedAttributes(def);
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
                    return true;
                }
            }
            return false;
        }

        private void RemoveActiveEffectsWithTags(List<GameplayTag> tags)
        {
            for (int i = _activeEffects.Count - 1; i >= 0; i--)
            {
                var def = _activeEffects[i].Def;
                bool match = false;
                foreach (var assetTag in def.AssetTags)
                    foreach (var t in tags)
                        if (assetTag.MatchesTag(t)) { match = true; break; }
                if (match) RemoveActiveGameplayEffect(_activeEffects[i].Handle);
            }
        }

        // 立即结算一个效果（Instant 或 周期一次）：改基础值 + 执行计算 + PostGameplayEffectExecute 钩子
        private void ExecuteEffectSpec(GameplayEffectSpec spec)
        {
            var def = spec.Def;

            // 收集所有要施加的增量：普通修改 + 自定义执行
            var outputs = new List<GameplayExecutionOutput>();
            foreach (var mod in def.Modifiers)
                outputs.Add(new GameplayExecutionOutput(mod.Attribute, mod.Magnitude.Evaluate(spec), mod.Operation));

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
                RecalculateCurrentValue(set, output.Attribute);

                // PostGameplayEffectExecute 钩子 —— Meta Attribute 伤害管线落点
                callback.EvaluatedMagnitude = newBase - oldBase;
                set.PostGameplayEffectExecute(callback);
            }
        }

        /// <summary>
        /// 直接对某属性的基础值施加一个修改并重算当前值（不走 GE 结算钩子，避免递归）。
        ///。Meta 属性映射（IncomingDamage→-Health）用它。
        /// </summary>
        public void ApplyModToAttributeBase(GameplayAttribute attribute, EAttributeModifierOp op, float magnitude)
        {
            var set = GetAttributeSet(attribute.AttributeSetTypeName);
            var data = attribute.ResolveData(this);
            if (set == null || data == null) return;
            float newBase = ApplyOp(data.BaseValue, op, magnitude);
            set.PreAttributeBaseChange(attribute, ref newBase);
            data.BaseValue = newBase;
            RecalculateCurrentValue(set, attribute);
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
        private void RecalculateAffectedAttributes(GameplayEffect def)
        {
            if (def == null) return;
            foreach (var mod in def.Modifiers)
            {
                var set = GetAttributeSet(mod.Attribute.AttributeSetTypeName);
                if (set != null) RecalculateCurrentValue(set, mod.Attribute);
            }
        }

        private void RecalculateCurrentValue(AttributeSet set, GameplayAttribute attribute)
        {
            var data = set.GetAttributeData(attribute.AttributeName);
            if (data == null) return;

            float baseV = data.BaseValue;
            float sumAdd = 0f, prodMul = 1f, prodDiv = 1f;
            bool hasOverride = false; float overrideV = 0f;

            foreach (var active in _activeEffects)
            {
                if (active.Inhibited) continue;
                foreach (var mod in active.Def.Modifiers)
                {
                    if (!mod.Attribute.Equals(attribute)) continue;
                    float mag = mod.Magnitude.Evaluate(active.Spec);
                    switch (mod.Operation)
                    {
                        case EAttributeModifierOp.Add: sumAdd += mag; break;
                        case EAttributeModifierOp.Multiply: prodMul *= mag; break;
                        case EAttributeModifierOp.Divide: if (!Mathf.Approximately(mag, 0f)) prodDiv *= mag; break;
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
                OnAttributeChanged?.Invoke(attribute, old, result);
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

            // 属性集
            foreach (var typeName in set.GrantedAttributeSetTypes)
            {
                if (string.IsNullOrEmpty(typeName)) continue;
                var type = Type.GetType(typeName) ?? FindTypeByName(typeName);
                if (type != null && Activator.CreateInstance(type) is AttributeSet attrSet)
                {
                    AddAttributeSet(attrSet);
                    handles.AddedAttributeSets.Add(attrSet);
                }
                else Debug.LogWarning($"[GAS] 找不到属性集类型: {typeName}");
            }

            // 常驻效果
            foreach (var ge in set.GrantedEffects)
            {
                if (ge == null) continue;
                var h = ApplyGameplayEffectToSelf(ge);
                if (h.IsValid) handles.EffectHandles.Add(h);
            }

            // 技能
            foreach (var ga in set.GrantedAbilities)
            {
                if (ga.Ability == null) continue;
                handles.AbilityHandles.Add(GiveAbility(ga.Ability, Mathf.Max(1, ga.Level)));
            }

            return handles;
        }

        private static Type FindTypeByName(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName);
                if (t != null) return t;
            }
            return null;
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
            TickActiveEffects(Time.deltaTime);
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

                // 周期结算
                if (active.Def.IsPeriodic && !active.Inhibited)
                {
                    active.PeriodRemaining -= dt;
                    while (active.PeriodRemaining <= 0f)
                    {
                        ExecuteEffectSpec(active.Spec); // 按 Instant 语义改基础值
                        active.PeriodRemaining += active.Def.Period;
                    }
                }

                // 时长推进
                if (active.Def.DurationType == EGameplayEffectDurationType.HasDuration)
                {
                    active.TimeRemaining -= dt;
                    if (active.TimeRemaining <= 0f) _expiredScratch.Add(active);
                }
            }

            foreach (var expired in _expiredScratch)
                RemoveActiveGameplayEffect(expired.Handle);
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
