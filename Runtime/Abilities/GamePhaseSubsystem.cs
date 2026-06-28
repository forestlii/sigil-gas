// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 游戏阶段子系统：用层级 GameplayTag 管理嵌套游戏阶段——父子阶段共存、兄弟阶段互斥。
// 例：Game.Playing 与 Game.Playing.WarmUp 可共存；启动 Game.Playing.PostGame 会结束 Game.Playing.WarmUp 但保留 Game.Playing。
// 用纯 C# 单例（同 GlobalAbilitySystem 风格）。
//
// ⚠️ 嵌套互斥用修正逻辑（朴素的“结束父子树阶段”会误结束父阶段）；此处
// 加了"A 是 T 祖先则保留"守卫，实现正确的父子共存语义。

using System;
using System.Collections.Generic;

namespace Likeon.GAS
{
    /// <summary>阶段标签匹配方式：精确 or 含子级。</summary>
    public enum EPhaseTagMatchType { ExactMatch, PartialMatch }

    /// <summary>嵌套游戏阶段管理单例。阶段由 <see cref="GamePhaseAbility"/> 表示，经 <see cref="StartPhase"/> 启动。</summary>
    public sealed class GamePhaseSubsystem
    {
        private static GamePhaseSubsystem _instance;
        public static GamePhaseSubsystem Instance => _instance ??= new GamePhaseSubsystem();

        private sealed class PhaseEntry { public GamePhaseAbility Ability; public Action<GamePhaseAbility> OnEnded; }
        private sealed class Observer
        {
            public GameplayTag Tag; public EPhaseTagMatchType Match; public Action<GameplayTag> Callback;
            public bool IsMatch(GameplayTag t) =>
                Match == EPhaseTagMatchType.ExactMatch ? t.MatchesTagExact(Tag) : t.MatchesTag(Tag);
        }

        private readonly List<PhaseEntry> _active = new List<PhaseEntry>();
        private readonly List<Observer> _startObservers = new List<Observer>();
        private readonly List<Observer> _endObservers = new List<Observer>();
        private Action<GamePhaseAbility> _pendingOnEnded;

        /// <summary>启动一个阶段：在驱动 ASC 上授予并激活阶段技能（兄弟阶段会被自动结束）。</summary>
        public void StartPhase(AbilitySystemComponent asc, GamePhaseAbility template, Action<GamePhaseAbility> onEnded = null)
        {
            if (asc == null || template == null) return;
            _pendingOnEnded = onEnded;
            var handle = asc.GiveAbility(template);
            asc.TryActivateAbility(handle); // 触发克隆实例 OnActivateAbility → OnBeginPhase
            _pendingOnEnded = null;
        }

        /// <summary>是否有活跃阶段命中该标签（活跃阶段 tag 等于或为其子级）。</summary>
        public bool IsPhaseActive(GameplayTag phaseTag)
        {
            for (int i = 0; i < _active.Count; i++)
                if (_active[i].Ability.GamePhaseTag.MatchesTag(phaseTag)) return true;
            return false;
        }

        /// <summary>注册观察者：阶段开始或当前已活跃时回调（已活跃则立即回调一次）。</summary>
        public void WhenPhaseStartsOrIsActive(GameplayTag phaseTag, EPhaseTagMatchType match, Action<GameplayTag> callback)
        {
            _startObservers.Add(new Observer { Tag = phaseTag, Match = match, Callback = callback });
            if (IsPhaseActive(phaseTag)) callback?.Invoke(phaseTag);
        }

        /// <summary>注册观察者：阶段结束时回调。</summary>
        public void WhenPhaseEnds(GameplayTag phaseTag, EPhaseTagMatchType match, Action<GameplayTag> callback)
            => _endObservers.Add(new Observer { Tag = phaseTag, Match = match, Callback = callback });

        /// <summary>由 <see cref="GamePhaseAbility"/> 激活时回调：结束兄弟阶段、记录、通知开始观察者。</summary>
        public void OnBeginPhase(GamePhaseAbility phase)
        {
            var t = phase.GamePhaseTag;
            var p = DirectParent(t);

            // 结束兄弟子树阶段：A 在父 P 子树内、不在 T 子树内、且不是 T 的祖先（见文件头说明）。
            var toEnd = new List<GamePhaseAbility>();
            for (int i = 0; i < _active.Count; i++)
            {
                var a = _active[i].Ability.GamePhaseTag;
                bool sibling = p.IsValid
                    ? (a.MatchesTag(p) && !a.MatchesTag(t) && !t.MatchesTag(a))
                    : (!a.MatchesTag(t) && !t.MatchesTag(a)); // 顶层阶段：结束所有无关阶段
                if (sibling) toEnd.Add(_active[i].Ability);
            }
            foreach (var a in toEnd) a.EndAbility(true); // → OnEndPhase 移除该项

            _active.Add(new PhaseEntry { Ability = phase, OnEnded = _pendingOnEnded });

            for (int i = 0; i < _startObservers.Count; i++)
                if (_startObservers[i].IsMatch(t)) _startObservers[i].Callback?.Invoke(t);
        }

        /// <summary>由 <see cref="GamePhaseAbility"/> 结束时回调：移除、触发 onEnded、通知结束观察者。</summary>
        public void OnEndPhase(GamePhaseAbility phase)
        {
            int idx = _active.FindIndex(e => e.Ability == phase);
            if (idx < 0) return;
            var entry = _active[idx];
            _active.RemoveAt(idx);

            entry.OnEnded?.Invoke(phase);

            var t = phase.GamePhaseTag;
            for (int i = 0; i < _endObservers.Count; i++)
                if (_endObservers[i].IsMatch(t)) _endObservers[i].Callback?.Invoke(t);
        }

        /// <summary>清空所有阶段与观察者（场景卸载 / 测试用）。</summary>
        public void Clear()
        {
            _active.Clear();
            _startObservers.Clear();
            _endObservers.Clear();
        }

        // 取标签的直接父级（去掉最后一段点分），无父返回无效标签。
        private static GameplayTag DirectParent(GameplayTag tag)
        {
            if (!tag.IsValid) return default;
            string n = tag.TagName;
            int i = n.LastIndexOf('.');
            return i < 0 ? default : GameplayTag.RequestTag(n.Substring(0, i));
        }
    }
}
