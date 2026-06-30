// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 曲线表：一组命名曲线，按等级（或任意输入）查值。供数值随等级初始化 / 成长的数据驱动配置使用。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>
    /// 曲线表资产：多行命名曲线（X=等级，Y=值）。
    /// 供 <see cref="GameplayModifierMagnitude"/> 的曲线模式按等级查值，
    /// 实现属性初始化 / 数值随等级成长的数据驱动配置（策划集中配、跨效果复用）。
    /// </summary>
    [CreateAssetMenu(fileName = "CurveTable_New", menuName = "Likeon/GAS/Curve Table")]
    public class CurveTable : ScriptableObject
    {
        /// <summary>一行命名曲线。</summary>
        [Serializable]
        public struct Row
        {
            [Tooltip("行名（被 magnitude 按名引用）")]
            public string Name;
            [Tooltip("X=等级，Y=该等级的值")]
            public AnimationCurve Curve;
        }

        [SerializeField] private List<Row> rows = new List<Row>();

        public IReadOnlyList<Row> Rows => rows;

        /// <summary>按行名 + 输入（等级）查值。找到返回 true。</summary>
        public bool TryEvaluate(string rowName, float input, out float value)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Name == rowName)
                {
                    value = rows[i].Curve != null ? rows[i].Curve.Evaluate(input) : 0f;
                    return true;
                }
            }
            value = 0f;
            return false;
        }

        /// <summary>按行名 + 输入（等级）查值，找不到返回 fallback。</summary>
        public float Evaluate(string rowName, float input, float fallback = 0f)
            => TryEvaluate(rowName, input, out var v) ? v : fallback;

        /// <summary>运行时/测试用：追加一行命名曲线。</summary>
        public void AddRow(string name, AnimationCurve curve) => rows.Add(new Row { Name = name, Curve = curve });
    }
}
