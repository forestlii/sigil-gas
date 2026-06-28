// Copyright 2026 Likeon All Rights Reserved.

using System;
using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>
    /// 一次输入携带的数据：值 + 已持续时间。的精简。
    /// 数字键=Value.x，轴/方向=Value，布尔=Value.x>0.5。
    /// </summary>
    [Serializable]
    public struct InputActionData
    {
        public Vector2 Value;
        public float ElapsedSeconds;

        public InputActionData(Vector2 value, float elapsedSeconds = 0f)
        {
            Value = value;
            ElapsedSeconds = elapsedSeconds;
        }

        public InputActionData(float value, float elapsedSeconds = 0f)
        {
            Value = new Vector2(value, 0f);
            ElapsedSeconds = elapsedSeconds;
        }

        public float AxisValue => Value.x;
        public bool IsPressed => Value.sqrMagnitude > 0.25f;

        public static readonly InputActionData Empty = new InputActionData(Vector2.zero, 0f);
    }
}
