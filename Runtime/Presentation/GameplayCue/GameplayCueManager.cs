// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 把 cue 标签路由到对应的 GameplayCueNotify 处理器。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>
    /// 全局 Cue 管理器：登记 GameplayCueNotify，按标签层级把 cue 事件路由到匹配的处理器。
    ///的最小可用子集。
    /// </summary>
    public sealed class GameplayCueManager
    {
        private static GameplayCueManager _instance;
        public static GameplayCueManager Instance => _instance ??= new GameplayCueManager();

        private readonly List<GameplayCueNotify> _notifies = new List<GameplayCueNotify>();

        /// <summary>所有 cue 触发时都会广播（供代码/测试观察，不依赖真实 VFX）。</summary>
        public event Action<GameObject, GameplayTag, EGameplayCueEvent, GameplayCueParameters> OnGameplayCue;

        /// <summary>登记一个 cue 处理器。</summary>
        public void RegisterCueNotify(GameplayCueNotify notify)
        {
            if (notify != null && !_notifies.Contains(notify)) _notifies.Add(notify);
        }

        public void UnregisterCueNotify(GameplayCueNotify notify) => _notifies.Remove(notify);

        /// <summary>清空登记（测试用）。</summary>
        public void Clear()
        {
            _notifies.Clear();
            _activeActorCues.Clear();
        }

        /// <summary>
        /// 路由一次 cue 事件：层级匹配（notify.CueTag 是 cueTag 的等于或父级）的处理器都会被调用。
        /// </summary>
        public void HandleGameplayCue(GameObject target, GameplayTag cueTag, EGameplayCueEvent cueEvent, GameplayCueParameters parameters)
        {
            if (!cueTag.IsValid) return;
            parameters ??= new GameplayCueParameters();

            for (int i = 0; i < _notifies.Count; i++)
            {
                var notify = _notifies[i];
                if (notify == null) continue;
                // cueTag "GameplayCue.Hit.Flesh" 命中 notify.CueTag "GameplayCue.Hit"
                if (cueTag.MatchesTag(notify.CueTag))
                    notify.HandleCue(target, cueEvent, parameters);
            }

            OnGameplayCue?.Invoke(target, cueTag, cueEvent, parameters);
        }

        // ===================== 有状态 Cue 实例托管 =====================
        // 按 (notify, target) 维护活跃的 GameplayCueNotify_Actor 实例：
        // OnActive 建实例、Removed 销毁、WhileActive 转发。GameplayCueNotify_Actor.HandleCue 委托到这里。
        private readonly Dictionary<(GameplayCueNotify_Actor, GameObject), GameplayCueActorInstance> _activeActorCues
            = new Dictionary<(GameplayCueNotify_Actor, GameObject), GameplayCueActorInstance>();

        /// <summary>分发一次有状态 Cue 事件（由 <see cref="GameplayCueNotify_Actor.HandleCue"/> 调用）。</summary>
        public void DispatchActorCue(GameplayCueNotify_Actor notify, GameObject target, EGameplayCueEvent cueEvent, GameplayCueParameters parameters)
        {
            if (notify == null) return;
            var key = (notify, target);
            switch (cueEvent)
            {
                case EGameplayCueEvent.OnActive:
                    if (_activeActorCues.ContainsKey(key)) return; // 同 target 同 Cue 已激活，不重复 spawn
                    _activeActorCues[key] = GameplayCueActorInstance.Create(notify, target, parameters);
                    break;

                case EGameplayCueEvent.WhileActive:
                    // 生产中逐帧由实例 Update 自驱；此分支供外部主动驱动（如未来 ASC 发 WhileActive / 测试）
                    if (_activeActorCues.TryGetValue(key, out var ticking) && ticking != null)
                        ticking.Tick(parameters, Time.deltaTime);
                    break;

                case EGameplayCueEvent.Removed:
                    if (_activeActorCues.TryGetValue(key, out var inst))
                    {
                        _activeActorCues.Remove(key);
                        if (inst != null) inst.Remove(parameters);
                    }
                    break;

                case EGameplayCueEvent.Executed:
                    // 有状态 Cue 不响应瞬时 Executed（那是 _Static 的语义）
                    break;
            }
        }

        /// <summary>查询某 target 上某有状态 Cue 是否有活跃实例（调试/测试）。</summary>
        public bool TryGetActorCueInstance(GameplayCueNotify_Actor notify, GameObject target, out GameplayCueActorInstance instance)
            => _activeActorCues.TryGetValue((notify, target), out instance);

        /// <summary>活跃有状态 Cue 实例数（调试/测试）。</summary>
        public int ActiveActorCueCount => _activeActorCues.Count;
    }
}
