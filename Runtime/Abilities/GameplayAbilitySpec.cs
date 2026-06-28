// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 被授予的技能在 ASC 内的登记项。

namespace Likeon.GAS
{
    /// <summary>技能授予句柄。</summary>
    public readonly struct GameplayAbilitySpecHandle
    {
        public readonly int Id;
        public GameplayAbilitySpecHandle(int id) { Id = id; }
        public bool IsValid => Id > 0;
        public static readonly GameplayAbilitySpecHandle Invalid = new GameplayAbilitySpecHandle(0);
        public override int GetHashCode() => Id;
        public override bool Equals(object obj) => obj is GameplayAbilitySpecHandle h && h.Id == Id;
    }

    /// <summary>
    /// 一条被授予的技能记录：技能实例 + 等级 + 句柄。
    /// 本实现采用 InstancedPerActor：每次授予克隆一个技能实例，持有自己的激活状态。
    /// </summary>
    public sealed class GameplayAbilitySpec
    {
        public GameplayAbilitySpecHandle Handle { get; }
        public GameplayAbility Ability { get; }
        public int Level { get; set; }
        public object SourceObject { get; set; }

        /// <summary>当前是否处于激活中。</summary>
        public bool IsActive { get; internal set; }

        public GameplayAbilitySpec(GameplayAbilitySpecHandle handle, GameplayAbility ability, int level)
        {
            Handle = handle;
            Ability = ability;
            Level = level;
        }
    }
}
