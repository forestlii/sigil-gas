// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 数据驱动的移动定义资产（可被压栈切换）。

using System.Collections.Generic;
using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>
    /// 移动定义资产：持有若干移动集（MovementSetSetting）。
    /// 运行时按 MovementSet 标签查出对应集。可在 MovementSystemComponent 上压栈/弹栈
    /// （如装备大剑时 push 一套大剑移动定义）。
    /// </summary>
    [CreateAssetMenu(fileName = "MovementDef_New", menuName = "Likeon/GAS/Movement Definition")]
    public class MovementDefinition : ScriptableObject
    {
        [Tooltip("本定义包含的移动集")]
        public List<MovementSetSetting> MovementSets = new List<MovementSetSetting>();

        /// <summary>按移动集标签查出设置；找不到返回首个（或 null）。</summary>
        public MovementSetSetting FindMovementSet(GameplayTag movementSet)
        {
            foreach (var s in MovementSets)
                if (s != null && s.MovementSet.MatchesTagExact(movementSet)) return s;
            return MovementSets.Count > 0 ? MovementSets[0] : null;
        }
    }
}
