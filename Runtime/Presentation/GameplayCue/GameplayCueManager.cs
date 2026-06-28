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
        public void Clear() => _notifies.Clear();

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
    }
}
