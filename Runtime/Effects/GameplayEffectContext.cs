// Copyright 2026 Likeon All Rights Reserved.
// 效果来源信息（谁施加的、用哪个技能、命中点等）。

using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>
    /// 效果上下文：记录效果的发起方信息，供结算与表现使用。
    ///（这里取常用字段）。
    /// </summary>
    public class GameplayEffectContext
    {
        /// <summary>发起效果的 ASC（伤害来源）。</summary>
        public AbilitySystemComponent SourceASC;

        /// <summary>发起方 Actor（GameObject）。</summary>
        public GameObject Instigator;

        /// <summary>实际造成命中的物体（如武器、子弹）。</summary>
        public GameObject EffectCauser;

        /// <summary>触发该效果的技能（可空）。</summary>
        public GameplayAbility SourceAbility;

        /// <summary>命中点（可选，受击表现用）。</summary>
        public Vector3 HitLocation;
        public bool HasHitLocation;

        public GameplayEffectContext() { }

        public GameplayEffectContext(AbilitySystemComponent source)
        {
            SourceASC = source;
            Instigator = source != null ? source.gameObject : null;
            EffectCauser = Instigator;
        }
    }
}
