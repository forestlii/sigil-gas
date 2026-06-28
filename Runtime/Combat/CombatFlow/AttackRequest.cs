// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 一次攻击请求的数据封装。
// 把"这一击做什么"打包：近战引用 AttackDefinition + 要控制的判定标签；
// 远程引用 BulletDefinition + 发射起点来源。用 [SerializeReference] 多态，技能/招式数据里内联配置。
// 注：连段/招式选择（按状态选不同动作）已由 CombatTypes.cs 的 AbilityActionSet(Layered) 承担。

using System;
using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>发射/瞄准起点来源类型。（取常用子集）。</summary>
    public enum EAttackTargetingSource
    {
        /// <summary>从 Pawn 位置、沿 Pawn 朝向。</summary>
        PawnForward,
        /// <summary>从武器枪口、沿 Pawn 朝向。</summary>
        WeaponForward,
        /// <summary>自定义（由调用方提供变换）。</summary>
        Custom
    }

    /// <summary>攻击请求基类。</summary>
    [Serializable]
    public abstract class AttackRequest
    {
        /// <summary>本次攻击的攻击定义（伤害等）。</summary>
        public abstract AttackDefinition GetAttackDefinition();
    }

    /// <summary>近战攻击请求。</summary>
    [Serializable]
    public class AttackRequest_Melee : AttackRequest
    {
        [Tooltip("近战攻击定义（伤害/cue/受击反应）")]
        public AttackDefinition Attack;
        [Tooltip("此击期间要开启的判定标签（与 MeleeAttackTrace 的判定配置对应）")]
        public GameplayTagContainer TracesToControl = new GameplayTagContainer();

        public override AttackDefinition GetAttackDefinition() => Attack;
    }

    /// <summary>远程（子弹）攻击请求。</summary>
    [Serializable]
    public class AttackRequest_Bullet : AttackRequest
    {
        [Tooltip("子弹定义")]
        public BulletDefinition Bullet;
        [Tooltip("发射起点来源")]
        public EAttackTargetingSource TargetingSource = EAttackTargetingSource.PawnForward;
        [Tooltip("起点附加偏移（本地）")]
        public Vector3 LocationOffset = Vector3.zero;

        /// <summary>子弹的攻击定义取自 BulletDefinition.Attack。</summary>
        public override AttackDefinition GetAttackDefinition() => Bullet != null ? Bullet.Attack : null;
    }
}
