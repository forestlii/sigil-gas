// AS_Health 手写 partial（Post 钩子）——阶段B 生成，逻辑复刻原核心 AS_Health。
using Likeon.GAS;
using UnityEngine;

namespace Likeon.GAS.Tests
{
    public partial class AS_Health
    {
        public override void PostAttributeChange(GameplayAttribute attribute, float oldValue, float newValue)
        {
            if (attribute == MaxHealthAttribute)
            {
                if (Mathf.Approximately(oldValue, newValue)) return;
                float cur = Health.CurrentValue;
                float v = oldValue > 0f ? cur * newValue / oldValue : newValue;
                Health.BaseValue = v; Health.CurrentValue = v;
            }
        }

        public override void PostGameplayEffectExecute(GameplayEffectModCallbackData data)
        {
            if (data.Attribute == IncomingDamageAttribute)
            {
                float dmg = IncomingDamage.BaseValue; IncomingDamage.Initialize(0f);
                if (dmg > 0f) Owner.ApplyModToAttributeBase(HealthAttribute, EAttributeModifierOp.Add, -dmg, data.Spec?.Context);
            }
            else if (data.Attribute == IncomingHealingAttribute)
            {
                float heal = IncomingHealing.BaseValue; IncomingHealing.Initialize(0f);
                if (heal > 0f) Owner.ApplyModToAttributeBase(HealthAttribute, EAttributeModifierOp.Add, heal, data.Spec?.Context);
            }
            if (data.Attribute == HealthAttribute)
            {
                Health.BaseValue = Mathf.Clamp(Health.BaseValue, 0f, MaxHealth.CurrentValue);
                Health.CurrentValue = Mathf.Clamp(Health.CurrentValue, 0f, MaxHealth.CurrentValue);
            }
        }
    }
}
