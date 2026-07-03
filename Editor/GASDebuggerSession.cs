// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// GAS 调试器的数据/订阅层：跟踪一个 ASC 的事件流并维护事件日志与属性变更高亮。
// 与 GUI 分离，便于 EditMode 测试直接驱动。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Likeon.GAS.Editor
{
    /// <summary>
    /// 调试器会话：订阅目标 ASC 的可观测事件，滚动记录事件日志，
    /// 并记录"最近变更过的属性"用于 GUI 高亮。切换目标时自动退订旧目标。
    /// </summary>
    public sealed class GASDebuggerSession : IDisposable
    {
        public const int MaxLogEntries = 200;

        public struct LogEntry
        {
            /// <summary>记录时的游戏时间（Time.time）。</summary>
            public float Time;
            public string Text;
        }

        public AbilitySystemComponent Target { get; private set; }

        /// <summary>事件日志（新条目在尾部，超过 <see cref="MaxLogEntries"/> 丢最旧）。</summary>
        public readonly List<LogEntry> Log = new List<LogEntry>();

        /// <summary>最近变更过的属性："SetTypeFullName.AttrName" -> 变更时的 realtimeSinceStartup。</summary>
        public readonly Dictionary<string, double> RecentAttributeChanges = new Dictionary<string, double>();

        /// <summary>外部时钟（默认 Time.realtimeSinceStartup；测试可注入固定值）。</summary>
        public Func<double> Clock = () => UnityEngine.Time.realtimeSinceStartupAsDouble;

        /// <summary>切换跟踪目标。传 null 仅退订。切换会清空日志与高亮（按目标各自成流）。</summary>
        public void SetTarget(AbilitySystemComponent asc)
        {
            if (ReferenceEquals(Target, asc)) return;
            Unsubscribe();
            Target = asc;
            Log.Clear();
            RecentAttributeChanges.Clear();
            Subscribe();
        }

        public void Dispose() => SetTarget(null);

        public static string AttributeKey(GameplayAttribute attribute)
            => attribute.AttributeSetTypeName + "." + attribute.AttributeName;

        private void AddLog(string text)
        {
            Log.Add(new LogEntry { Time = UnityEngine.Time.time, Text = text });
            if (Log.Count > MaxLogEntries) Log.RemoveAt(0);
        }

        private static string SourceSuffix(GameplayEffectContext ctx)
        {
            if (ctx == null) return string.Empty;
            if (ctx.SourceAbility != null) return $"  (by {ctx.SourceAbility.name})";
            if (ctx.Instigator != null) return $"  (by {ctx.Instigator.name})";
            return string.Empty;
        }

        // ---- 订阅管理 ----

        private void Subscribe()
        {
            if (Target == null) return;
            Target.OnAttributeChanged += HandleAttributeChanged;
            Target.OnAbilityActivated += HandleAbilityActivated;
            Target.OnAbilityEnded += HandleAbilityEnded;
            Target.OnAbilityActivationFailed += HandleAbilityFailed;
            Target.OnAbilityGiven += HandleAbilityGiven;
            Target.OnAbilityRemoved += HandleAbilityRemoved;
            Target.OnActiveEffectAdded += HandleEffectAdded;
            Target.OnActiveEffectRemoved += HandleEffectRemoved;
            Target.OnActiveEffectStackChanged += HandleEffectStackChanged;
            Target.OnGameplayEvent += HandleGameplayEvent;
            Target.OnTagChanged += HandleTagChanged;
        }

        private void Unsubscribe()
        {
            if (Target == null) return;
            Target.OnAttributeChanged -= HandleAttributeChanged;
            Target.OnAbilityActivated -= HandleAbilityActivated;
            Target.OnAbilityEnded -= HandleAbilityEnded;
            Target.OnAbilityActivationFailed -= HandleAbilityFailed;
            Target.OnAbilityGiven -= HandleAbilityGiven;
            Target.OnAbilityRemoved -= HandleAbilityRemoved;
            Target.OnActiveEffectAdded -= HandleEffectAdded;
            Target.OnActiveEffectRemoved -= HandleEffectRemoved;
            Target.OnActiveEffectStackChanged -= HandleEffectStackChanged;
            Target.OnGameplayEvent -= HandleGameplayEvent;
            Target.OnTagChanged -= HandleTagChanged;
        }

        // ---- 事件处理 ----

        private void HandleAttributeChanged(AttributeChangeData d)
        {
            RecentAttributeChanges[AttributeKey(d.Attribute)] = Clock();
            AddLog($"[Attr] {d.Attribute.AttributeName}: {d.OldValue:F2} → {d.NewValue:F2}{SourceSuffix(d.Source)}");
        }

        private void HandleAbilityActivated(GameplayAbility a) => AddLog($"[Ability] Activated: {a.name}");

        private void HandleAbilityEnded(GameplayAbility a, bool cancelled)
            => AddLog($"[Ability] {(cancelled ? "Cancelled" : "Ended")}: {a.name}");

        private void HandleAbilityFailed(GameplayAbility a, EAbilityActivationFailReason reason)
            => AddLog($"[Ability] Failed: {a.name} ({reason})");

        private void HandleAbilityGiven(GameplayAbilitySpec spec) => AddLog($"[Ability] Given: {spec.Ability.name}");
        private void HandleAbilityRemoved(GameplayAbilitySpec spec) => AddLog($"[Ability] Removed: {spec.Ability.name}");

        private void HandleEffectAdded(ActiveGameplayEffect ae) => AddLog($"[Effect] Added: {ae.Def.name}");
        private void HandleEffectRemoved(ActiveGameplayEffect ae) => AddLog($"[Effect] Removed: {ae.Def.name}");

        private void HandleEffectStackChanged(ActiveGameplayEffect ae, int oldCount, int newCount)
            => AddLog($"[Effect] Stack {ae.Def.name}: {oldCount} → {newCount}");

        private void HandleGameplayEvent(GameplayTag tag, GameplayEventData data) => AddLog($"[Event] {tag}");

        private void HandleTagChanged(GameplayTag tag, bool present) => AddLog($"[Tag] {(present ? "+" : "-")} {tag}");
    }
}
