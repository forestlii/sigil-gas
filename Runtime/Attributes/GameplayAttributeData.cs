// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 单个属性的数据，区分 BaseValue 与 CurrentValue。

using System;
using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>
    /// 单个属性的数值。
    /// BaseValue = 永久基础值（被 Instant/Periodic 效果改），
    /// CurrentValue = 叠加了 Duration/Infinite 效果后的当前值（这些效果移除后回退）。
    /// </summary>
    [Serializable]
    public class GameplayAttributeData
    {
        [SerializeField] private float baseValue;
        [NonSerialized] private float currentValue;

        public GameplayAttributeData() { }

        public GameplayAttributeData(float defaultValue)
        {
            baseValue = defaultValue;
            currentValue = defaultValue;
        }

        /// <summary>基础值（永久）。</summary>
        public float BaseValue
        {
            get => baseValue;
            set => baseValue = value;
        }

        /// <summary>当前值（含临时增益/减益）。</summary>
        public float CurrentValue
        {
            get => currentValue;
            set => currentValue = value;
        }

        /// <summary>用基础值初始化当前值（首次设置/反序列化后调用）。</summary>
        public void Initialize(float value)
        {
            baseValue = value;
            currentValue = value;
        }
    }
}
