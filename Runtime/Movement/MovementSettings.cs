// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 注：Unity 用 m/s，默认值已换算，字段含义一致。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>
    /// 单个移动状态（Walk/Jog/Sprint）的参数。
    /// </summary>
    [Serializable]
    public struct MovementStateSetting
    {
        [Tooltip("此设置对应的移动状态标签（Movement.State.*）")]
        public GameplayTag State;

        [Tooltip("前进速度 (m/s)")]
        public float Speed;
        [Tooltip("侧移速度 (m/s)")]
        public float StrafeSpeed;
        [Tooltip("后退速度 (m/s)")]
        public float BackwardsSpeed;

        [Tooltip("加速度")]
        public float Acceleration;
        [Tooltip("制动减速度")]
        public float BrakingDeceleration;

        [Tooltip("旋转插值速度")]
        public float RotationInterpolationSpeed;

        public static MovementStateSetting Default(GameplayTag state, float speed) => new MovementStateSetting
        {
            State = state,
            Speed = speed,
            StrafeSpeed = speed * 0.8f,
            BackwardsSpeed = speed * 0.6f,
            Acceleration = 8f,
            BrakingDeceleration = 10f,
            RotationInterpolationSpeed = 12f
        };
    }

    /// <summary>
    /// 一个移动集（如 徒手 / 持大剑）：包含该集下各移动状态的参数。
    /// </summary>
    [Serializable]
    public class MovementSetSetting
    {
        [Tooltip("此移动集的标签（Movement.Set.*）")]
        public GameplayTag MovementSet;

        [Tooltip("该集下的各移动状态参数（Walk/Jog/Sprint）")]
        public List<MovementStateSetting> States = new List<MovementStateSetting>();

        /// <summary>按移动状态标签取参数；找不到返回列表首项（或默认）。</summary>
        public MovementStateSetting GetStateSetting(GameplayTag state)
        {
            foreach (var s in States)
                if (s.State.MatchesTagExact(state)) return s;
            return States.Count > 0 ? States[0] : MovementStateSetting.Default(state, 3.75f);
        }
    }
}
