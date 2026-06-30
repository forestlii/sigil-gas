// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 战斗契约接口：参与战斗的角色/组件实现它，战斗系统据此查询目标/动作/武器/移动模式/死亡状态。
// 对齐 UE 战斗接口（类型适配 Unity：AActor→GameObject、USceneComponent→Transform、FGameplayTag→GameplayTag）。
// 这是"契约"——由宿主角色实现；GAS 核心通过 CombatInterface.Get(go) 取到它来查询。

using System.Collections.Generic;
using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>战斗角色契约。宿主角色实现，战斗系统/技能据此查询。对齐 UE 战斗接口。</summary>
    public interface ICombatInterface
    {
        // ---- 目标 ----
        /// <summary>当前战斗目标（GameObject）。</summary>
        GameObject GetCombatTargetActor();
        /// <summary>当前战斗目标对象（Transform，对齐 UE USceneComponent）。</summary>
        Transform GetCombatTargetObject();

        // ---- 能力动作 / 武器 ----
        /// <summary>按能力标签 + 施法者/目标状态查询该播的动作（通常委托给 AbilityActionLibrary）。</summary>
        bool QueryAbilityActions(GameplayTagContainer abilityTags, GameplayTagContainer sourceTags,
            GameplayTagContainer targetTags, List<AbilityAction> outActions);
        /// <summary>当前武器（对齐 UE 取武器 / QueryWeapon）。</summary>
        IWeapon GetCurrentWeapon();

        // ---- 输入 ----
        /// <summary>是否按住防御键。</summary>
        bool IsHoldingBlockInput();
        /// <summary>移动输入方向（世界空间）。</summary>
        Vector3 GetMovementInputDirection();

        // ---- 移动模式（标签化，对齐 UE RotationMode/MovementSet/MovementState）----
        void SetRotationMode(GameplayTag newRotationMode);
        GameplayTag GetRotationMode();
        void SetMovementSet(GameplayTag newMovementSet);
        GameplayTag GetMovementSet();
        void SetMovementState(GameplayTag newMovementState);
        GameplayTag GetMovementState();
        GameplayTag GetDesiredMovementState();

        // ---- 生命周期 ----
        void StartDeath();
        void FinishDeath();
        bool IsDead();
    }

    /// <summary>取战斗契约的辅助（对齐 UE 头注释推荐的 GetCombatInterface 用法）。</summary>
    public static class CombatInterface
    {
        /// <summary>从 GameObject（含父级）取 ICombatInterface 实现，无则 null。</summary>
        public static ICombatInterface Get(GameObject go) => go == null ? null : go.GetComponentInParent<ICombatInterface>();

        /// <summary>从 Component（含父级）取 ICombatInterface 实现，无则 null。</summary>
        public static ICombatInterface Get(Component component) => component == null ? null : component.GetComponentInParent<ICombatInterface>();
    }
}
