// AS_Poise 手写 partial（Post 钩子）——阶段B 生成，逻辑复刻原核心 AS_Poise。
using Likeon.GAS;
using UnityEngine;

namespace Likeon.GAS.Tests
{
    public partial class AS_Poise
    {
        public override void PostAttributeChange(GameplayAttribute attribute, float oldValue, float newValue)
        {
            if (attribute == MaxPoiseAttribute && !Mathf.Approximately(oldValue, newValue))
            {
                float pct = oldValue > 0f ? Poise.CurrentValue / oldValue : 1f;
                float v = newValue * pct;
                Poise.BaseValue = v; Poise.CurrentValue = v;
            }
        }

        public override void PostGameplayEffectExecute(GameplayEffectModCallbackData data)
        {
            if (data.Attribute == IncomingPoiseDamageAttribute)
            {
                float dmg = IncomingPoiseDamage.BaseValue; IncomingPoiseDamage.Initialize(0f);
                if (dmg > 0f) Owner.ApplyModToAttributeBase(PoiseAttribute, EAttributeModifierOp.Add, -dmg, data.Spec?.Context);
            }
            if (data.Attribute == PoiseAttribute)
            {
                Poise.BaseValue = Mathf.Clamp(Poise.BaseValue, 0f, MaxPoise.CurrentValue);
                Poise.CurrentValue = Mathf.Clamp(Poise.CurrentValue, 0f, MaxPoise.CurrentValue);
            }
        }
    }
}
