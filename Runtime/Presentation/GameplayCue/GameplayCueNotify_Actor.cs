// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 有状态 Cue：持续型效果施加时 spawn 一个持久表现实例，效果移除时销毁。

using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>
    /// 有状态 Cue（对齐 UE GameplayCueNotify_Actor）：与无状态的
    /// <see cref="GameplayCueNotify_Static"/> 不同，本类为 Duration/Infinite 效果生成一个
    /// 持久实例（挂在 target 上跟随），生命周期 OnActive → WhileActive(逐帧) → OnRemove。
    ///
    /// 直接用法（零代码）：填 <see cref="SpawnPrefab"/>（一个 loop 粒子/光环预制），
    /// OnActive 自动 spawn 并挂到 target、OnRemove 自动销毁。
    /// 需要动态行为（跟随强度/颜色随剩余时间变化等）时，子类重写
    /// <see cref="OnActive"/> / <see cref="WhileActive"/> / <see cref="OnRemove"/>。
    ///
    /// 实例的生命周期由 <see cref="GameplayCueManager"/> 按 (notify, target) 托管：
    /// 同一 target 上同一 Cue 只有一个活跃实例；WhileActive 逐帧 tick 由实例自驱。
    /// </summary>
    [CreateAssetMenu(fileName = "Cue_New_Actor", menuName = "Sigil/GAS/Gameplay Cue Notify (Actor)")]
    public class GameplayCueNotify_Actor : GameplayCueNotify
    {
        [Header("持久表现")]
        [Tooltip("OnActive 时生成的持久表现预制体（可空；子类也可纯代码建表现）")]
        public GameObject SpawnPrefab;
        [Tooltip("生成的实例是否挂到 target 下（跟随 target 移动）")]
        public bool AttachToTarget = true;

        [Header("移除")]
        [Tooltip("Removed 时是否自动销毁生成的实例")]
        public bool AutoDestroyOnRemove = true;
        [Tooltip("销毁前延迟秒数（0=立即；>0 可用于淡出）")]
        public float AutoDestroyDelay = 0f;

        /// <summary>持续效果施加：实例已 spawn（<paramref name="spawnedInstance"/> 为 SpawnPrefab 实例，可空）。子类重写做初始化。</summary>
        public virtual void OnActive(GameObject target, GameObject spawnedInstance, GameplayCueParameters parameters) { }

        /// <summary>持续期间逐帧回调（由实例自驱）。子类重写更新表现（跟随、强度衰减等）。</summary>
        public virtual void WhileActive(GameObject target, GameObject spawnedInstance, GameplayCueParameters parameters, float deltaTime) { }

        /// <summary>效果移除：实例即将销毁。子类重写做清理（停粒子、淡出等）。</summary>
        public virtual void OnRemove(GameObject target, GameObject spawnedInstance, GameplayCueParameters parameters) { }

        // 有状态 Cue 的生命周期须走 Manager 的实例表托管，这里委托 Manager。
        public sealed override void HandleCue(GameObject target, EGameplayCueEvent cueEvent, GameplayCueParameters parameters)
            => GameplayCueManager.Instance.DispatchActorCue(this, target, cueEvent, parameters);
    }
}
