// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 数据驱动 + GameplayTag 状态化的移动系统基类。
// 覆盖范围：移动状态机（MovementSet/State、LocomotionMode、RotationMode、输入方向、移动定义栈）
//          + 与 GAS 的"状态总线"对接。
//        节点无 Unity 等价物，Unity 用 Animator/Playables 自行实现）；网络同步（阶段 6）。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Likeon.GAS
{
    [AddComponentMenu("Likeon/GAS/Movement System Component")]
    public class MovementSystemComponent : MonoBehaviour
    {
        // ===================== GameplayTags（状态总线） =====================
        [SerializeField] private GameplayTagContainer ownedTags = new GameplayTagContainer();
        private AbilitySystemComponent _tagsProvider;

        /// <summary>
        /// 把标签源指向 ASC。指向后：① GetGameplayTags 合并 ASC 拥有标签；
        /// ② 移动状态变化会把 MovementState/LocomotionMode 标签镜像到 ASC（让输入/技能层读到，如"冲刺→滑铲"）。
        ///—— "用 ASC 的 Tags 驱动移动状态"。
        /// </summary>
        public void SetGameplayTagsProvider(AbilitySystemComponent provider)
        {
            _tagsProvider = provider;
            SyncStateTagsToProvider(GameplayTag.None, GameplayTag.None); // 立即把当前状态推上去
        }

        /// <summary>合并 OwnedTags 与 Provider(ASC) 的标签。</summary>
        public GameplayTagContainer GetGameplayTags()
        {
            var result = new GameplayTagContainer(ownedTags);
            if (_tagsProvider != null)
            {
                var provided = new GameplayTagContainer();
                _tagsProvider.GetOwnedGameplayTags(provided);
                result.AppendTags(provided);
            }
            return result;
        }

        public void AddGameplayTag(GameplayTag tag) => ownedTags.AddTag(tag);
        public void RemoveGameplayTag(GameplayTag tag) => ownedTags.RemoveTag(tag);
        public void SetGameplayTags(GameplayTagContainer tags)
        {
            ownedTags.Clear();
            if (tags != null) ownedTags.AppendTags(tags);
        }

        // 把当前 MovementState / LocomotionMode 镜像到 ASC（移除旧、加新），实现跨系统状态总线
        private void SyncStateTagsToProvider(GameplayTag prevState, GameplayTag prevLocomotion)
        {
            if (_tagsProvider == null) return;
            if (prevState.IsValid && prevState != movementState) _tagsProvider.RemoveLooseGameplayTag(prevState);
            if (prevLocomotion.IsValid && prevLocomotion != locomotionMode) _tagsProvider.RemoveLooseGameplayTag(prevLocomotion);
            if (movementState.IsValid) _tagsProvider.AddLooseGameplayTag(movementState);
            if (locomotionMode.IsValid) _tagsProvider.AddLooseGameplayTag(locomotionMode);
        }

        // ===================== 移动定义栈 =====================
        [Header("移动定义（栈，最后一个为当前）")]
        [SerializeField] private List<MovementDefinition> movementDefinitions = new List<MovementDefinition>();
        private MovementDefinition _currentDefinition;

        public MovementDefinition GetMovementDefinition() => _currentDefinition;

        /// <summary>压入一个移动定义（如装备大剑）。</summary>
        public void PushAvailableMovementDefinition(MovementDefinition newDefinition, bool popCurrent = true)
        {
            if (newDefinition == null) return;
            if (popCurrent && movementDefinitions.Count > 0)
                movementDefinitions[movementDefinitions.Count - 1] = newDefinition;
            else
                movementDefinitions.Add(newDefinition);
            _currentDefinition = newDefinition;
            RefreshMovementSetSetting();
        }

        /// <summary>弹出最后一个移动定义。</summary>
        public void PopAvailableMovementDefinition()
        {
            if (movementDefinitions.Count > 0) movementDefinitions.RemoveAt(movementDefinitions.Count - 1);
            _currentDefinition = movementDefinitions.Count > 0 ? movementDefinitions[movementDefinitions.Count - 1] : null;
            RefreshMovementSetSetting();
        }

        // ===================== MovementSet / State Setting =====================
        [Header("当前移动集 / 状态")]
        [SerializeField] private GameplayTag movementSet;
        private MovementSetSetting _currentSetSetting;
        private MovementStateSetting _currentStateSetting;

        public GameplayTag GetMovementSet() => movementSet;
        public MovementSetSetting GetMovementSetSetting() => _currentSetSetting;
        public MovementStateSetting GetMovementStateSetting() => _currentStateSetting;

        public event Action<GameplayTag> OnMovementSetChanged; // 参数=前一个 set

        /// <summary>切换移动集（如徒手↔持械）。</summary>
        public void SetMovementSet(GameplayTag newMovementSet)
        {
            if (movementSet == newMovementSet) return;
            var prev = movementSet;
            movementSet = newMovementSet;
            RefreshMovementSetSetting();
            OnMovementSetChanged?.Invoke(prev);
        }

        // 刷新链：Set 设置 → State 设置 → 应用。*Setting / ApplyMovementSetting。
        protected virtual void RefreshMovementSetSetting()
        {
            _currentSetSetting = _currentDefinition != null ? _currentDefinition.FindMovementSet(movementSet) : null;
            RefreshMovementStateSetting();
        }

        protected virtual void RefreshMovementStateSetting()
        {
            if (_currentSetSetting != null)
                _currentStateSetting = _currentSetSetting.GetStateSetting(movementState);
            ApplyMovementSetting();
        }

        /// <summary>子类把当前状态参数（速度等）应用到实际移动。</summary>
        protected virtual void ApplyMovementSetting() { }

        // ===================== Desired / 实际 MovementState =====================
        [SerializeField] private GameplayTag desiredMovementState = MovementTags.MovementState_Jog;
        private GameplayTag movementState = MovementTags.MovementState_Jog;

        public GameplayTag GetDesiredMovementState() => desiredMovementState;
        public GameplayTag GetMovementState() => movementState;

        public event Action<GameplayTag> OnMovementStateChanged; // 参数=前一个 state

        /// <summary>设置想要的移动状态（Walk/Jog/Sprint）。</summary>
        public void SetDesiredMovement(GameplayTag newDesired)
        {
            if (!newDesired.IsValid) return;
            desiredMovementState = newDesired;
            RefreshMovementState();
        }

        /// <summary>在 Walk→Jog→Sprint 间循环。</summary>
        public void CycleDesiredMovementState(bool forward = true)
        {
            GameplayTag[] order = { MovementTags.MovementState_Walk, MovementTags.MovementState_Jog, MovementTags.MovementState_Sprint };
            int idx = Array.FindIndex(order, t => t == desiredMovementState);
            if (idx < 0) idx = 1;
            idx = (idx + (forward ? 1 : order.Length - 1)) % order.Length;
            SetDesiredMovement(order[idx]);
        }

        protected virtual void RefreshMovementState()
        {
            var actual = CalculateActualMovementState();
            if (actual == movementState) return;
            var prev = movementState;
            movementState = actual;
            RefreshMovementStateSetting();           // 状态变了，刷新速度参数
            SyncStateTagsToProvider(prev, GameplayTag.None); // 镜像到 ASC（状态总线）
            OnMovementStateChanged?.Invoke(prev);
        }

        /// <summary>由想要状态算出实际状态（可在此按条件限制，如体力不足不能冲刺）。</summary>
        protected virtual GameplayTag CalculateActualMovementState() => desiredMovementState;

        // ===================== LocomotionMode =====================
        private GameplayTag locomotionMode = MovementTags.LocomotionMode_Grounded;
        public GameplayTag GetLocomotionMode() => locomotionMode;

        public event Action<GameplayTag> OnLocomotionModeChanged;

        public void SetLocomotionMode(GameplayTag newMode)
        {
            if (locomotionMode == newMode || !newMode.IsValid) return;
            var prev = locomotionMode;
            locomotionMode = newMode;
            SyncStateTagsToProvider(GameplayTag.None, prev);
            OnLocomotionModeChanged?.Invoke(prev);
        }

        // ===================== RotationMode =====================
        [SerializeField] private GameplayTag desiredRotationMode = MovementTags.RotationMode_ViewDirection;
        private GameplayTag rotationMode = MovementTags.RotationMode_ViewDirection;

        public GameplayTag GetDesiredRotationMode() => desiredRotationMode;
        public GameplayTag GetRotationMode() => rotationMode;
        public void SetDesiredRotationMode(GameplayTag mode)
        {
            if (mode.IsValid) { desiredRotationMode = mode; rotationMode = mode; }
        }

        // ===================== 输入方向 =====================
        private Vector3 _inputDirection;
        public Vector3 GetInputDirection() => _inputDirection;
        /// <summary>设置移动输入方向（世界空间，已归一化）。</summary>
        public void SetInputDirection(Vector3 dir)
        {
            _inputDirection = dir.sqrMagnitude > 1f ? dir.normalized : dir;
        }

        // ===================== 初始化 =====================
        protected virtual void Awake()
        {
            _currentDefinition = movementDefinitions.Count > 0 ? movementDefinitions[movementDefinitions.Count - 1] : null;
            // 若同物体有 ASC，自动作为标签 provider（开箱即用的状态总线）
            if (_tagsProvider == null)
            {
                var asc = GetComponent<AbilitySystemComponent>();
                if (asc != null) SetGameplayTagsProvider(asc);
            }
            RefreshMovementSetSetting();
        }
    }
}
