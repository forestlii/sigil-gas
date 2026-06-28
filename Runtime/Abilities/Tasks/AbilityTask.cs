// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 技能内的异步任务基类。
// Unity 无蓝图，改用：C# 协程驱动 + Action 回调订阅。用法：
//   var t = AbilityTask_WaitDelay.WaitDelay(this, 0.3f);
//   t.OnFinish += () => { ...下一步... };
//   t.Activate();
// 技能 EndAbility 时会统一 ExternalCancel 所有未结束的任务，避免悬挂协程。

using System.Collections;
using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>
    /// 技能任务基类。由具体任务的静态工厂创建并绑定到 owning <see cref="GameplayAbility"/>，
    /// 订阅其回调后调用 <see cref="Activate"/> 启动。
    /// </summary>
    public abstract class AbilityTask
    {
        /// <summary>拥有本任务的技能。</summary>
        public GameplayAbility Ability { get; private set; }

        /// <summary>便捷取技能所在的 ASC（协程宿主 / 事件源）。</summary>
        protected AbilitySystemComponent ASC => Ability != null ? Ability.ASC : null;

        /// <summary>是否已 Activate 且尚未结束。</summary>
        public bool IsActive { get; private set; }

        private Coroutine _coroutine;
        private bool _ended;

        /// <summary>由具体任务的工厂调用，把任务挂到技能上。绑定 OwningAbility。</summary>
        internal void InitTask(GameplayAbility ability)
        {
            Ability = ability;
            ability?.AddTask(this);
        }

        /// <summary>启动任务。()。重复调用无副作用。</summary>
        public AbilityTask Activate()
        {
            if (IsActive || _ended) return this;
            IsActive = true;
            OnActivate();
            return this;
        }

        /// <summary>子类实现：任务启动逻辑（绑事件 / 起协程）。内部。</summary>
        protected abstract void OnActivate();

        /// <summary>协程驱动的任务用它起协程（挂在 ASC 这个 MonoBehaviour 上）。</summary>
        protected void RunCoroutine(IEnumerator routine)
        {
            if (ASC != null) _coroutine = ASC.StartCoroutine(routine);
        }

        /// <summary>任务自然结束（delay 到点 / 收到目标事件）。</summary>
        public void EndTask() => FinishInternal(abilityEnded: false);

        /// <summary>被外部取消（技能结束或被打断时由 <see cref="GameplayAbility"/> 调）。</summary>
        public void ExternalCancel() => FinishInternal(abilityEnded: true);

        private void FinishInternal(bool abilityEnded)
        {
            if (_ended) return;
            _ended = true;
            IsActive = false;
            if (_coroutine != null && ASC != null) { ASC.StopCoroutine(_coroutine); _coroutine = null; }
            OnDestroy(abilityEnded);
            Ability?.RemoveTask(this);
        }

        /// <summary>清理钩子（解绑事件等）。(bool AbilityEnded)。</summary>
        protected virtual void OnDestroy(bool abilityEnded) { }
    }
}
