// Copyright 2026 Likeon All Rights Reserved.
// GameplayEffect 的一次"实例化施加请求"，带等级、上下文、SetByCaller。

using System.Collections.Generic;

namespace Likeon.GAS
{
    /// <summary>
    /// 效果规格：把"哪个 GameplayEffect + 什么等级 + 谁发起 + 运行时传入的数值"打包。
    /// 同一个 GameplayEffect 资产可被多次实例化成不同 Spec。
    /// </summary>
    public class GameplayEffectSpec
    {
        public GameplayEffect Def { get; }
        public int Level { get; set; }
        public GameplayEffectContext Context { get; }

        // SetByCaller：运行时按标签传入的数值（如把本次攻击的伤害值塞进来）。
        private readonly Dictionary<GameplayTag, float> _setByCaller = new Dictionary<GameplayTag, float>();

        public GameplayEffectSpec(GameplayEffect def, GameplayEffectContext context, int level = 1)
        {
            Def = def;
            Context = context ?? new GameplayEffectContext();
            Level = level;
        }

        /// <summary>设置 SetByCaller 数值。</summary>
        public GameplayEffectSpec SetSetByCallerMagnitude(GameplayTag tag, float value)
        {
            if (tag.IsValid) _setByCaller[tag] = value;
            return this;
        }

        public float GetSetByCallerMagnitude(GameplayTag tag, float defaultValue = 0f)
            => _setByCaller.TryGetValue(tag, out float v) ? v : defaultValue;
    }
}
