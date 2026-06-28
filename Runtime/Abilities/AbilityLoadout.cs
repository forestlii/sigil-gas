// Copyright 2026 Likeon All Rights Reserved.
// 技能装载：把一组技能 + 常驻效果 + 属性集打包，一次性授予给 ASC，并返回句柄便于整批回收。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>
    /// 技能装载资产：声明要授予的技能、要常驻施加的效果、要添加的属性集类型。
    /// 由 ASC 一次性授予，授予结果用 <see cref="GrantedAbilityHandles"/> 整批回收。
    /// </summary>
    [CreateAssetMenu(fileName = "AbilityLoadout_New", menuName = "Likeon/GAS/Ability Loadout")]
    public class AbilityLoadout : ScriptableObject
    {
        [Serializable]
        public struct GrantedAbility
        {
            public GameplayAbility Ability;
            [Min(1)] public int Level;
        }

        [Header("授予的技能")]
        public List<GrantedAbility> GrantedAbilities = new List<GrantedAbility>();

        [Header("授予时常驻施加的效果（如初始化属性的 GE）")]
        public List<GameplayEffect> GrantedEffects = new List<GameplayEffect>();

        [Header("添加的属性集类型全名（如 Likeon.GAS.AS_Health）")]
        [Tooltip("运行时按类型名 Activator 创建并加入 ASC")]
        public List<string> GrantedAttributeSetTypes = new List<string>();
    }

    /// <summary>一次技能装载授予产出的句柄集合，用于整批撤销。</summary>
    public sealed class GrantedAbilityHandles
    {
        public readonly List<GameplayAbilitySpecHandle> AbilityHandles = new List<GameplayAbilitySpecHandle>();
        public readonly List<ActiveGameplayEffectHandle> EffectHandles = new List<ActiveGameplayEffectHandle>();
        public readonly List<AttributeSet> AddedAttributeSets = new List<AttributeSet>();

        /// <summary>从 ASC 整批撤销这次授予的技能 / 效果 / 属性集。</summary>
        public void RevokeFrom(AbilitySystemComponent asc)
        {
            if (asc == null) return;
            foreach (var h in AbilityHandles) asc.ClearAbility(h);
            foreach (var h in EffectHandles) asc.RemoveActiveGameplayEffect(h);
            foreach (var set in AddedAttributeSets) asc.RemoveAttributeSet(set);
            AbilityHandles.Clear();
            EffectHandles.Clear();
            AddedAttributeSets.Clear();
        }
    }
}
