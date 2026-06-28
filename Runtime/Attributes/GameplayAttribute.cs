// Copyright 2026 Likeon All Rights Reserved.
// 指向"某个 AttributeSet 上的某个属性"的引用。
// Unity 没有等价反射属性指针，这里用 (集合类型 + 属性名) 定位。

using System;
using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>
    /// 属性引用：标识"哪个 AttributeSet 的哪个属性"，用于 GameplayEffect 的修改目标。
    /// 可在 Inspector 序列化（存类型全名 + 属性名），也可在代码里用 <see cref="From{T}"/> 构造。
    /// </summary>
    [Serializable]
    public struct GameplayAttribute : IEquatable<GameplayAttribute>
    {
        [SerializeField] private string attributeSetType; // AttributeSet 子类的 Type.FullName
        [SerializeField] private string attributeName;    // 属性名，如 "Health"

        public string AttributeSetTypeName => attributeSetType;
        public string AttributeName => attributeName;
        public bool IsValid => !string.IsNullOrEmpty(attributeSetType) && !string.IsNullOrEmpty(attributeName);

        public GameplayAttribute(Type setType, string name)
        {
            attributeSetType = setType?.FullName;
            attributeName = name;
        }

        /// <summary>代码内便捷构造：From&lt;AS_Health&gt;("Health")。</summary>
        public static GameplayAttribute From<T>(string name) where T : AttributeSet
            => new GameplayAttribute(typeof(T), name);

        /// <summary>从指定 ASC 上解析出对应的 GameplayAttributeData（找不到返回 null）。</summary>
        public GameplayAttributeData ResolveData(AbilitySystemComponent asc)
        {
            if (asc == null || !IsValid) return null;
            var set = asc.GetAttributeSet(attributeSetType);
            return set?.GetAttributeData(attributeName);
        }

        public float GetCurrentValue(AbilitySystemComponent asc)
        {
            var data = ResolveData(asc);
            return data?.CurrentValue ?? 0f;
        }

        public float GetBaseValue(AbilitySystemComponent asc)
        {
            var data = ResolveData(asc);
            return data?.BaseValue ?? 0f;
        }

        public bool Equals(GameplayAttribute other)
            => string.Equals(attributeSetType, other.attributeSetType, StringComparison.Ordinal)
            && string.Equals(attributeName, other.attributeName, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is GameplayAttribute o && Equals(o);
        public override int GetHashCode() => (attributeSetType, attributeName).GetHashCode();
        public override string ToString() => IsValid ? $"{attributeSetType}.{attributeName}" : "(invalid attribute)";

        public static bool operator ==(GameplayAttribute a, GameplayAttribute b) => a.Equals(b);
        public static bool operator !=(GameplayAttribute a, GameplayAttribute b) => !a.Equals(b);
    }
}
