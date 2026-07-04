// AS_Stamina 手写 partial（Post 钩子）——阶段B 生成。去掉 Incoming Meta（消除与 AS_Health 撞名）。
using Likeon.GAS;
using UnityEngine;

namespace Likeon.GAS.Tests
{
    public partial class AS_Stamina
    {
        public override void PostAttributeChange(GameplayAttribute attribute, float oldValue, float newValue)
        {
            if (attribute == MaxStaminaAttribute)
            {
                if (Mathf.Approximately(oldValue, newValue)) return;
                float pct = oldValue > 0f ? Stamina.CurrentValue / oldValue : 1f;
                float v = newValue * pct;
                Stamina.BaseValue = v; Stamina.CurrentValue = v;
            }
        }

        public override void PostGameplayEffectExecute(GameplayEffectModCallbackData data)
        {
            if (data.Attribute == StaminaAttribute)
            {
                Stamina.BaseValue = Mathf.Clamp(Stamina.BaseValue, 0f, MaxStamina.CurrentValue);
                Stamina.CurrentValue = Mathf.Clamp(Stamina.CurrentValue, 0f, MaxStamina.CurrentValue);
            }
        }
    }
}
