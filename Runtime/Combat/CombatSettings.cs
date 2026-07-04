// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 战斗系统设置：项目级可配的战斗开关（对齐 UE 战斗系统设置 / UDeveloperSettings）。
// Unity 无 UDeveloperSettings，用 ScriptableObject + 静态 Active 单例（宿主在启动时 SetActive 一份资产即可）。

using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>
    /// 战斗系统设置资产。<see cref="Active"/> 是当前生效实例（宿主未设则用默认值实例）。
    /// 对齐 UE 战斗系统设置 的两个字段。
    /// </summary>
    [CreateAssetMenu(fileName = "CombatSettings", menuName = "Sigil/Combat/Combat Settings")]
    public class CombatSettings : ScriptableObject
    {
        [Tooltip("查询主骨骼网格用的标签名（对齐 UE CharacterMeshLookupTag，默认 'Main'）")]
        public GameplayTag MainMeshLookupTag;

        [Tooltip("禁用归属（敌我）检查——调试用，允许跨队伍伤害（对齐 UE 跨队伍伤害调试开关）")]
        public bool DisableAffiliationCheck = false;

        private static CombatSettings _active;

        /// <summary>当前生效的战斗设置；宿主未 SetActive 时返回一份默认值实例。</summary>
        public static CombatSettings Active => _active != null ? _active : (_active = CreateInstance<CombatSettings>());

        /// <summary>设置当前生效的战斗设置资产（传 null 则下次访问回退默认值实例）。</summary>
        public static void SetActive(CombatSettings settings) => _active = settings;
    }
}
