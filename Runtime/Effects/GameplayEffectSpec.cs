// Copyright (c) 2026 Likeon. Licensed under the MIT License.
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

        /// <summary>
        /// 运行时动态注入的资产标签（对齐 UE FGameplayEffectSpec 的 dynamic AssetTags）。
        /// 如攻击定义把"近战/远程、劈砍/打击"AttackTags 加到本次伤害 spec，供目标按攻击类型查询。
        /// </summary>
        public readonly GameplayTagContainer DynamicAssetTags = new GameplayTagContainer();

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

        /// <summary>把若干标签加进动态资产标签（如攻击类型）。</summary>
        public void AddDynamicAssetTags(IEnumerable<GameplayTag> tags)
        {
            if (tags == null) return;
            foreach (var t in tags) if (t.IsValid) DynamicAssetTags.AddTag(t);
        }

        /// <summary>本 spec 的全部资产标签 = 定义静态 AssetTags + 运行时动态注入。</summary>
        public IEnumerable<GameplayTag> GetAllAssetTags()
        {
            if (Def != null)
                foreach (var t in Def.AssetTags) yield return t;
            foreach (var t in DynamicAssetTags) yield return t;
        }
    }
}
