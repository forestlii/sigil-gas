// Copyright 2026 Likeon All Rights Reserved.
// 技能本体。
// Unity 无蓝图，技能逻辑用 C# 子类重写 OnActivateAbility 实现。

using System.Collections.Generic;
using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>
    /// 技能基类（ScriptableObject 模板）。授予时被 ASC 克隆成每角色一个实例（InstancedPerActor）。
    /// 子类重写 <see cref="OnActivateAbility"/> 写技能逻辑（播动画、等待事件、施加效果……）。
    /// </summary>
    public abstract class GameplayAbility : ScriptableObject
    {
        [Header("身份标签 Ability Tags")]
        [Tooltip("标识本技能的标签，用于 TagRelationship 的 block/cancel 匹配。")]
        public List<GameplayTag> AbilityTags = new List<GameplayTag>();

        [Header("激活组 Activation Group")]
        public EAbilityActivationPolicy ActivationPolicy = EAbilityActivationPolicy.Parallel;

        [Header("激活期间挂在角色身上的松散标签")]
        [Tooltip("如滑铲激活时挂 State.Sliding")]
        public List<GameplayTagCount> ActivationOwnedLooseTags = new List<GameplayTagCount>();

        [Header("激活准入标签 Activation Tag Requirements")]
        [Tooltip("角色必须拥有这些标签才能激活")]
        public List<GameplayTag> ActivationRequiredTags = new List<GameplayTag>();
        [Tooltip("角色拥有任一这些标签则不能激活")]
        public List<GameplayTag> ActivationBlockedTags = new List<GameplayTag>();

        [Header("消耗与冷却 Cost / Cooldown")]
        [Tooltip("激活时施加给自己的消耗效果（如扣 Stamina）")]
        public GameplayEffect CostEffect;
        [Tooltip("冷却效果。其 GrantedTags 作为冷却标签")]
        public GameplayEffect CooldownEffect;

        [Header("效果容器 Effect Containers")]
        [Tooltip("按标签组织'命中要施加的效果'")]
        public SerializableEffectContainerMap EffectContainerMap = new SerializableEffectContainerMap();

        // ---- 运行时（每个实例自己的状态）----
        public AbilitySystemComponent ASC { get; internal set; }
        public GameplayAbilitySpec Spec { get; internal set; }
        public bool IsActive { get; private set; }

        // 本技能未结束的 AbilityTask（技能结束时统一取消，避免悬挂协程）
        private readonly List<AbilityTask> _activeTasks = new List<AbilityTask>();
        internal void AddTask(AbilityTask task) { if (task != null && !_activeTasks.Contains(task)) _activeTasks.Add(task); }
        internal void RemoveTask(AbilityTask task) { _activeTasks.Remove(task); }

        private readonly GameplayTagContainer _abilityTagsCache = new GameplayTagContainer();
        /// <summary>把 AbilityTags 列表转成容器（缓存）。</summary>
        public GameplayTagContainer GetAbilityTags()
        {
            _abilityTagsCache.Clear();
            foreach (var t in AbilityTags) _abilityTagsCache.AddTag(t);
            return _abilityTagsCache;
        }

        /// <summary>
        /// 能否激活：检查准入标签 + TagRelationship 状态准入 + Cost + Cooldown。
        /// </summary>
        public virtual bool CanActivate()
        {
            if (ASC == null) return false;

            // 1) 本技能自身的准入/阻挡标签
            foreach (var t in ActivationRequiredTags)
                if (!ASC.HasMatchingGameplayTag(t)) return false;
            foreach (var t in ActivationBlockedTags)
                if (ASC.HasMatchingGameplayTag(t)) return false;

            // 2) 状态感知的 TagRelationship 准入（V2，按角色当前 tag 动态注入所需/禁止）
            if (ASC.InteractionRules != null)
            {
                var required = new GameplayTagContainer();
                var blocked = new GameplayTagContainer();
                ASC.GatherActivationRequirements(GetAbilityTags(), required, blocked);
                foreach (var t in required) if (!ASC.HasMatchingGameplayTag(t)) return false;
                foreach (var t in blocked) if (ASC.HasMatchingGameplayTag(t)) return false;
            }

            // 3) 激活组互斥
            if (ASC.IsActivationPolicyBlocked(ActivationPolicy)) return false;

            // 4) 消耗与冷却
            if (!CheckCost()) return false;
            if (!CheckCooldown()) return false;

            return true;
        }

        /// <summary>检查能否支付消耗。</summary>
        public virtual bool CheckCost()
        {
            if (CostEffect == null) return true;
            return ASC.CanApplyAttributeModifiers(CostEffect, Spec?.Level ?? 1);
        }

        /// <summary>检查是否在冷却中。</summary>
        public virtual bool CheckCooldown()
        {
            if (CooldownEffect == null) return true;
            foreach (var t in CooldownEffect.GrantedTags)
                if (ASC.HasMatchingGameplayTag(t)) return false;
            return true;
        }

        /// <summary>施加消耗 + 冷却。的核心。</summary>
        public virtual bool CommitAbility()
        {
            if (!CheckCost() || !CheckCooldown()) return false;
            ApplyCost();
            ApplyCooldown();
            return true;
        }

        public virtual void ApplyCost()
        {
            if (CostEffect != null) ASC.ApplyGameplayEffectToSelf(CostEffect, Spec?.Level ?? 1);
        }

        public virtual void ApplyCooldown()
        {
            if (CooldownEffect != null) ASC.ApplyGameplayEffectToSelf(CooldownEffect, Spec?.Level ?? 1);
        }

        // ---- 生命周期，由 ASC 调用 ----

        internal void Activate(GameplayEventData triggerData)
        {
            IsActive = true;
            Spec.IsActive = true;
            // 挂上激活期间的松散标签
            foreach (var tc in ActivationOwnedLooseTags)
                ASC.AddLooseGameplayTag(tc.Tag, tc.Count);
            ASC.RegisterAbilityPolicy(ActivationPolicy, this);
            OnActivateAbility(triggerData);
        }

        /// <summary>技能逻辑入口。子类必须实现。</summary>
        protected abstract void OnActivateAbility(GameplayEventData triggerData);

        /// <summary>结束技能。</summary>
        public void EndAbility(bool wasCancelled = false)
        {
            if (!IsActive) return;
            IsActive = false;
            if (Spec != null) Spec.IsActive = false;

            CancelAllTasks(); // 结束所有未完成的 AbilityTask（清理 AbilityTask）

            OnEndAbility(wasCancelled);

            // 卸下激活期间的松散标签
            foreach (var tc in ActivationOwnedLooseTags)
                ASC.RemoveLooseGameplayTag(tc.Tag, tc.Count);
            ASC.UnregisterAbilityPolicy(ActivationPolicy, this);
            ASC.NotifyAbilityEnded(this, wasCancelled);
        }

        /// <summary>技能结束时的清理。子类可重写（停动画、解绑等）。的子类逻辑。</summary>
        protected virtual void OnEndAbility(bool wasCancelled) { }

        /// <summary>外部取消（被打断）。</summary>
        public void CancelAbility() => EndAbility(true);

        // 取消并清空本技能所有未结束的任务（用快照迭代，因 ExternalCancel 会回调 RemoveTask）
        private void CancelAllTasks()
        {
            if (_activeTasks.Count == 0) return;
            var snapshot = new List<AbilityTask>(_activeTasks);
            _activeTasks.Clear();
            for (int i = 0; i < snapshot.Count; i++)
                snapshot[i].ExternalCancel();
        }

        // ---- 效果容器便捷接口----

        public bool HasEffectContainer(GameplayTag tag) => EffectContainerMap.Contains(tag);

        /// <summary>实例化效果容器：把容器里的效果做成 spec，并绑定目标。</summary>
        public GameplayEffectContainerSpec MakeEffectContainerSpec(GameplayTag containerTag, IEnumerable<AbilitySystemComponent> targets, int overrideLevel = -1)
        {
            var result = new GameplayEffectContainerSpec();
            if (!EffectContainerMap.TryGet(containerTag, out var container)) return result;

            int level = overrideLevel >= 0 ? overrideLevel : (Spec?.Level ?? 1);
            if (targets != null) result.Targets.AddRange(targets);

            if (container.TargetGameplayEffects != null)
                foreach (var ge in container.TargetGameplayEffects)
                    if (ge != null) result.TargetEffectSpecs.Add(ASC.MakeOutgoingSpec(ge, level));

            if (container.SelfGameplayEffects != null)
                foreach (var ge in container.SelfGameplayEffects)
                    if (ge != null) result.SelfEffectSpecs.Add(ASC.MakeOutgoingSpec(ge, level));

            return result;
        }

        /// <summary>施加一个效果容器 spec：给目标和自己施加对应效果。</summary>
        public List<ActiveGameplayEffectHandle> ApplyEffectContainerSpec(GameplayEffectContainerSpec spec)
        {
            var handles = new List<ActiveGameplayEffectHandle>();
            if (spec == null) return handles;

            foreach (var target in spec.Targets)
            {
                if (target == null) continue;
                foreach (var es in spec.TargetEffectSpecs)
                    handles.Add(target.ApplyGameplayEffectSpecToSelf(es));
            }
            foreach (var es in spec.SelfEffectSpecs)
                handles.Add(ASC.ApplyGameplayEffectSpecToSelf(es));

            return handles;
        }
    }
}
