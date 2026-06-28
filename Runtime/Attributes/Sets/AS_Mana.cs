// Copyright 2026 Likeon All Rights Reserved.

using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>法力属性集。</summary>
    public sealed class AS_Mana : AttributeSet
    {
        public static readonly GameplayTag TagMana = GameplayTag.RequestTag("Attribute.ManaSet.Mana");
        public static readonly GameplayTag TagMaxMana = GameplayTag.RequestTag("Attribute.ManaSet.MaxMana");

        public readonly GameplayAttributeData Mana = new GameplayAttributeData(100f);
        public readonly GameplayAttributeData MaxMana = new GameplayAttributeData(100f);

        public GameplayAttribute ManaAttribute => GetAttribute(nameof(Mana));
        public GameplayAttribute MaxManaAttribute => GetAttribute(nameof(MaxMana));

        protected override void RegisterAttributes()
        {
            Register(nameof(Mana), Mana);
            Register(nameof(MaxMana), MaxMana);
        }

        public override void PreAttributeChange(GameplayAttribute attribute, ref float newValue)
        {
            if (attribute == ManaAttribute)
                newValue = Mathf.Clamp(newValue, 0f, MaxMana.CurrentValue);
        }

        public override void PostGameplayEffectExecute(GameplayEffectModCallbackData data)
        {
            if (data.Attribute == ManaAttribute)
            {
                Mana.BaseValue = Mathf.Clamp(Mana.BaseValue, 0f, MaxMana.CurrentValue);
                Mana.CurrentValue = Mathf.Clamp(Mana.CurrentValue, 0f, MaxMana.CurrentValue);
            }
        }
    }
}
