// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 受击反应处理器。
// 受击方收到 AttackResult 时，CombatFlow 依次跑一串处理器做反应：判死亡、按标签查询触发
// GameplayEvent（→可激活受击/硬直技能）、触发命中 cue。
// Unity 用 [SerializeReference] 多态类（与 InputProcessor 同范式），Inspector 里按子类型加。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>受击反应处理上下文：受击方 + 攻击方。</summary>
    public readonly struct AttackFlowContext
    {
        public readonly GameObject Owner;                  // 受击方 GameObject
        public readonly AbilitySystemComponent OwnerASC;   // 受击方 ASC
        public readonly AbilitySystemComponent AttackerASC; // 攻击方 ASC（可空）

        public AttackFlowContext(GameObject owner, AbilitySystemComponent ownerASC, AbilitySystemComponent attackerASC)
        {
            Owner = owner; OwnerASC = ownerASC; AttackerASC = attackerASC;
        }
    }

    /// <summary>受击结果处理器基类。</summary>
    [Serializable]
    public abstract class AttackResultProcessor
    {
        public abstract void Process(AttackResult result, in AttackFlowContext ctx);
    }

    /// <summary>死亡处理：受击方 Health&lt;=0 时挂死亡标签、触发死亡事件。</summary>
    [Serializable]
    public class AttackResultProcessor_Death : AttackResultProcessor
    {
        [Tooltip("死亡状态标签（留空默认 State.Dead）")]
        public GameplayTag DeadTag;
        [Tooltip("死亡时广播的事件标签（可空，用于触发死亡技能/表现）")]
        public GameplayTag DeathEventTag;
        [Tooltip("判定死亡读的生命属性名（跨属性集按名解析，默认 Health）")]
        public string HealthAttributeName = "Health";

        public override void Process(AttackResult result, in AttackFlowContext ctx)
        {
            var asc = ctx.OwnerASC;
            if (asc == null) return;
            var hp = asc.GetAttributeDataByName(string.IsNullOrEmpty(HealthAttributeName) ? "Health" : HealthAttributeName);
            if (hp == null || hp.CurrentValue > 0f) return;

            var deadTag = DeadTag.IsValid ? DeadTag : GameplayTag.RequestTag("State.Dead");
            if (!asc.HasMatchingGameplayTag(deadTag)) asc.AddLooseGameplayTag(deadTag);

            if (DeathEventTag.IsValid)
                asc.SendGameplayEvent(DeathEventTag, new GameplayEventData(DeathEventTag)
                {
                    Target = ctx.Owner,
                    Context = result.EffectContext
                });
        }
    }

    /// <summary>
    /// 触发 GameplayEvent：按来源/目标标签查询过滤后，对受击方（或攻击方）广播事件。
    /// 这是"受击→激活硬直/受击技能"的桥。（带 Tag 查询）。
    /// </summary>
    [Serializable]
    public class AttackResultProcessor_GameplayEvent : AttackResultProcessor
    {
        [Tooltip("对攻击方聚合标签的查询（空=不限）")]
        public GameplayTagQuery SourceTagQuery;
        [Tooltip("对受击方聚合标签的查询（空=不限）")]
        public GameplayTagQuery TargetTagQuery;
        [Tooltip("勾选则把事件发给攻击方，否则发给受击方")]
        public bool SendToAttacker;
        [Tooltip("从 AttackResult.TaggedValues 取此标签的值作为事件 magnitude（可空）")]
        public GameplayTag MagnitudeTag;
        [Tooltip("要广播的事件标签")]
        public List<GameplayTag> EventTriggers = new List<GameplayTag>();

        public override void Process(AttackResult result, in AttackFlowContext ctx)
        {
            if (SourceTagQuery != null && !SourceTagQuery.Matches(result.AggregatedSourceTags)) return;
            if (TargetTagQuery != null && !TargetTagQuery.Matches(result.AggregatedTargetTags)) return;

            var receiver = SendToAttacker ? ctx.AttackerASC : ctx.OwnerASC;
            if (receiver == null) return;

            float magnitude = MagnitudeTag.IsValid ? result.GetTaggedValue(MagnitudeTag) : 0f;
            foreach (var tag in EventTriggers)
            {
                if (!tag.IsValid) continue;
                receiver.SendGameplayEvent(tag, new GameplayEventData(tag)
                {
                    Target = ctx.Owner,
                    Context = result.EffectContext,
                    EventMagnitude = magnitude
                });
            }
        }
    }

    /// <summary>触发命中 cue：在命中点执行 cue（备用 cue 列表）。</summary>
    [Serializable]
    public class AttackResultProcessor_GameplayCue : AttackResultProcessor
    {
        [Tooltip("要执行的命中表现 cue")]
        public List<GameplayTag> GameplayCues = new List<GameplayTag>();

        public override void Process(AttackResult result, in AttackFlowContext ctx)
        {
            var asc = ctx.OwnerASC;
            if (asc == null) return;
            var p = new GameplayCueParameters
            {
                Location = result.HitLocation,
                EffectContext = result.EffectContext,
                Instigator = result.EffectContext?.Instigator,
                SourceObject = result.EffectContext?.EffectCauser
            };
            foreach (var cue in GameplayCues)
                if (cue.IsValid) asc.ExecuteGameplayCue(cue, p);
        }
    }
}
