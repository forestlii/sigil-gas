// Copyright 2026 Likeon All Rights Reserved.

using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>耐力属性集。Stamina/MaxStamina + Incoming（Meta）。</summary>
    public sealed class AS_Stamina : AttributeSet
    {
        public static readonly GameplayTag TagStamina = GameplayTag.RequestTag("Attribute.StaminaSet.Stamina");
        public static readonly GameplayTag TagMaxStamina = GameplayTag.RequestTag("Attribute.StaminaSet.MaxStamina");

        public readonly GameplayAttributeData Stamina = new GameplayAttributeData(100f);
        public readonly GameplayAttributeData MaxStamina = new GameplayAttributeData(100f);
        public readonly GameplayAttributeData IncomingHealing = new GameplayAttributeData(0f);
        public readonly GameplayAttributeData IncomingDamage = new GameplayAttributeData(0f);

        public GameplayAttribute StaminaAttribute => GetAttribute(nameof(Stamina));
        public GameplayAttribute MaxStaminaAttribute => GetAttribute(nameof(MaxStamina));
        public GameplayAttribute IncomingDamageAttribute => GetAttribute(nameof(IncomingDamage));
        public GameplayAttribute IncomingHealingAttribute => GetAttribute(nameof(IncomingHealing));

        protected override void RegisterAttributes()
        {
            Register(nameof(Stamina), Stamina);
            Register(nameof(MaxStamina), MaxStamina);
            Register(nameof(IncomingHealing), IncomingHealing);
            Register(nameof(IncomingDamage), IncomingDamage);
        }

        public override void PreAttributeChange(GameplayAttribute attribute, ref float newValue)
        {
            if (attribute == StaminaAttribute)
                newValue = Mathf.Clamp(newValue, 0f, MaxStamina.CurrentValue);
        }

        public override void PostAttributeChange(GameplayAttribute attribute, float oldValue, float newValue)
        {
            if (attribute == MaxStaminaAttribute && !Mathf.Approximately(oldValue, newValue))
            {
                float pct = oldValue > 0f ? Stamina.CurrentValue / oldValue : 1f;
                float v = newValue * pct;
                Stamina.BaseValue = v; Stamina.CurrentValue = v;
            }
        }

        public override void PostGameplayEffectExecute(GameplayEffectModCallbackData data)
        {
            if (data.Attribute == IncomingDamageAttribute)
            {
                float dmg = IncomingDamage.BaseValue; IncomingDamage.Initialize(0f);
                if (dmg > 0f) Owner.ApplyModToAttributeBase(StaminaAttribute, EAttributeModifierOp.Add, -dmg);
            }
            else if (data.Attribute == IncomingHealingAttribute)
            {
                float heal = IncomingHealing.BaseValue; IncomingHealing.Initialize(0f);
                if (heal > 0f) Owner.ApplyModToAttributeBase(StaminaAttribute, EAttributeModifierOp.Add, heal);
            }
            if (data.Attribute == StaminaAttribute)
            {
                Stamina.BaseValue = Mathf.Clamp(Stamina.BaseValue, 0f, MaxStamina.CurrentValue);
                Stamina.CurrentValue = Mathf.Clamp(Stamina.CurrentValue, 0f, MaxStamina.CurrentValue);
            }
        }
    }
}
