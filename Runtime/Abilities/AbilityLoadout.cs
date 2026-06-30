// Copyright (c) 2026 Likeon. Licensed under the MIT License.
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
            [Tooltip("授予时附加到该技能实例的动态标签（对齐 UE 技能集授予项的动态标签）；参与 TagRelationship 匹配，如标 Slot.Primary")]
            public List<GameplayTag> DynamicTags;
        }

        [Header("授予的技能")]
        public List<GrantedAbility> GrantedAbilities = new List<GrantedAbility>();

        [Header("授予时常驻施加的效果（如初始化属性的 GE）")]
        public List<GameplayEffect> GrantedEffects = new List<GameplayEffect>();

        [Header("添加的属性集（强类型；Inspector 选具体子类，如 AS_Health）")]
        [Tooltip("对齐 UE TSoftClassPtr<UAttributeSet> 强类型引用；授予时按所选类型新建实例加入 ASC（比字符串类型名更安全：重命名不断链、Inspector 有类型选单）")]
        [SerializeReference] public List<AttributeSet> GrantedAttributeSets = new List<AttributeSet>();
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
