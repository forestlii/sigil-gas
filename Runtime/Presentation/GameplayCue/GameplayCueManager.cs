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

        // 禁用 Domain Reload 时静态实例会跨 Play 会话残留（_notifies/_activeActorCues/_actorPool 里全是已销毁对象）。
        // 进入 Play Mode 前重置。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;

        private readonly List<GameplayCueNotify> _notifies = new List<GameplayCueNotify>();

        /// <summary>所有 cue 触发时都会广播（供代码/测试观察，不依赖真实 VFX）。</summary>
        public event Action<GameObject, GameplayTag, EGameplayCueEvent, GameplayCueParameters> OnGameplayCue;

        /// <summary>登记一个 cue 处理器。</summary>
        public void RegisterCueNotify(GameplayCueNotify notify)
        {
            if (notify != null && !_notifies.Contains(notify)) _notifies.Add(notify);
        }

        public void UnregisterCueNotify(GameplayCueNotify notify)
        {
            _notifies.Remove(notify);
            // 级联回收：注销一个有状态 notify 时，销毁以它为键的活跃实例 + 清它的池桶，
            // 否则场景里残留没有处理器的孤儿 CueActor / 尸体条目。
            if (notify is GameplayCueNotify_Actor actorNotify)
            {
                _cascadeScratch.Clear();
                foreach (var kv in _activeActorCues)
                    if (kv.Key.Item1 == actorNotify)
                    {
                        if (kv.Value != null) kv.Value.DestroyNow();
                        _cascadeScratch.Add(kv.Key);
                    }
                foreach (var k in _cascadeScratch) _activeActorCues.Remove(k);
                _cascadeScratch.Clear();

                if (_actorPool.TryGetValue(actorNotify, out var stack))
                {
                    while (stack.Count > 0) { var inst = stack.Pop(); if (inst != null) inst.DestroyNow(); }
                    _actorPool.Remove(actorNotify);
                }
            }
        }

        // UnregisterCueNotify 级联回收时收集待删 key，避免遍历中改字典。
        private readonly List<(GameplayCueNotify_Actor, GameObject)> _cascadeScratch
            = new List<(GameplayCueNotify_Actor, GameObject)>();

        /// <summary>清空登记（测试用）。</summary>
        public void Clear()
        {
            _notifies.Clear();
            // 销毁活跃实例本体，避免场景里残留孤儿 CueActor GameObject（原来只清字典）。
            foreach (var kv in _activeActorCues)
                if (kv.Value != null) kv.Value.DestroyNow();
            _activeActorCues.Clear();
            foreach (var kv in _actorPool)
                while (kv.Value.Count > 0)
                {
                    var inst = kv.Value.Pop();
                    if (inst != null) inst.DestroyNow();
                }
            _actorPool.Clear();
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
        // OnActive 建/复用实例、Removed 回池或销毁、WhileActive 转发。GameplayCueNotify_Actor.HandleCue 委托到这里。
        private readonly Dictionary<(GameplayCueNotify_Actor, GameObject), GameplayCueActorInstance> _activeActorCues
            = new Dictionary<(GameplayCueNotify_Actor, GameObject), GameplayCueActorInstance>();
        // 空闲实例池（按 notify 分桶）：移除时回收、再激活时复用，避免频繁 new GameObject / Instantiate / Destroy。
        private readonly Dictionary<GameplayCueNotify_Actor, Stack<GameplayCueActorInstance>> _actorPool
            = new Dictionary<GameplayCueNotify_Actor, Stack<GameplayCueActorInstance>>();

        /// <summary>分发一次有状态 Cue 事件（由 <see cref="GameplayCueNotify_Actor.HandleCue"/> 调用）。</summary>
        public void DispatchActorCue(GameplayCueNotify_Actor notify, GameObject target, EGameplayCueEvent cueEvent, GameplayCueParameters parameters)
        {
            if (notify == null) return;
            var key = (notify, target);
            switch (cueEvent)
            {
                case EGameplayCueEvent.OnActive:
                    // 已有活跃实例且未被外部销毁 → 不重复 spawn；若命中的是 Unity fake-null（实例被外部 Destroy），
                    // 视为不存在并重建——否则 ContainsKey 命中尸体条目，后续激活被永久静默吞掉。
                    if (_activeActorCues.TryGetValue(key, out var existing) && existing != null) return;
                    _activeActorCues[key] = RentOrCreateActor(notify, target, parameters);
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
                        if (inst != null)
                        {
                            inst.NotifyRemove(parameters); // 先回调 OnRemove
                            // 主路径（自动销毁 + 无延迟）→ 回池复用；延迟淡出 / 不自动销毁 → 走原销毁/保留
                            if (notify.AutoDestroyOnRemove && notify.AutoDestroyDelay <= 0f)
                                ReturnActorToPool(notify, inst);
                            else
                                inst.DestroyOrKeep();
                        }
                    }
                    break;

                case EGameplayCueEvent.Executed:
                    // 有状态 Cue 不响应瞬时 Executed（那是 _Static 的语义）
                    break;
            }
        }

        // 从池取一个复用（跳过被外部销毁的），没有则新建。
        private GameplayCueActorInstance RentOrCreateActor(GameplayCueNotify_Actor notify, GameObject target, GameplayCueParameters parameters)
        {
            if (_actorPool.TryGetValue(notify, out var stack))
                while (stack.Count > 0)
                {
                    var pooled = stack.Pop();
                    if (pooled == null) continue; // 被外部销毁的跳过
                    pooled.Reactivate(target, parameters);
                    return pooled;
                }
            return GameplayCueActorInstance.Create(notify, target, parameters);
        }

        // 停用并回收进池。
        private void ReturnActorToPool(GameplayCueNotify_Actor notify, GameplayCueActorInstance inst)
        {
            inst.ParkForPool();
            if (!_actorPool.TryGetValue(notify, out var stack))
            {
                stack = new Stack<GameplayCueActorInstance>();
                _actorPool[notify] = stack;
            }
            stack.Push(inst);
        }

        /// <summary>查询某 target 上某有状态 Cue 是否有活跃实例（调试/测试）。</summary>
        public bool TryGetActorCueInstance(GameplayCueNotify_Actor notify, GameObject target, out GameplayCueActorInstance instance)
            => _activeActorCues.TryGetValue((notify, target), out instance);

        /// <summary>活跃有状态 Cue 实例数（调试/测试）。</summary>
        public int ActiveActorCueCount => _activeActorCues.Count;

        /// <summary>空闲池中的实例数（调试/测试）。</summary>
        public int PooledActorCount
        {
            get { int n = 0; foreach (var kv in _actorPool) n += kv.Value.Count; return n; }
        }
    }
}
