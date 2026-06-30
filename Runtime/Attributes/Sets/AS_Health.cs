// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 忠实说明：源码 AS_Health.cpp 的 PreAttributeChange 把 Health 夹到 [0,MaxHealth]，
// PostAttributeChange 在 MaxHealth 变化时按比例调整 Health，PostGameplayEffectExecute 只 clamp Health。
// 源码里 IncomingDamage/IncomingHealing 被声明为 Meta 属性（注释写"映射为 ±Health"），
// 但该映射不在 AS_Health.cpp 内（委托给 AttributeSystemComponent / 实际伤害走 AS_Combat 的执行计算）。
// 这里补上标准 Meta 映射，让 IncomingDamage 有消费者；如需 100% 对齐源码可删掉映射段。

namespace Likeon.GAS
{
    /// <summary>生命属性集。Health/MaxHealth + IncomingDamage/IncomingHealing（Meta）。</summary>
    public sealed class AS_Health : AttributeSet
    {
        // 标签名沿用源码: "Attribute.HealthSet.*"
        public static readonly GameplayTag TagHealth = GameplayTag.RequestTag("Attribute.HealthSet.Health");
        public static readonly GameplayTag TagMaxHealth = GameplayTag.RequestTag("Attribute.HealthSet.MaxHealth");

        public readonly GameplayAttributeData Health = new GameplayAttributeData(100f);
        public readonly GameplayAttributeData MaxHealth = new GameplayAttributeData(100f);
        public readonly GameplayAttributeData IncomingHealing = new GameplayAttributeData(0f);
        public readonly GameplayAttributeData IncomingDamage = new GameplayAttributeData(0f);

        // 便捷的属性句柄
        public GameplayAttribute HealthAttribute => GetAttribute(nameof(Health));
        public GameplayAttribute MaxHealthAttribute => GetAttribute(nameof(MaxHealth));
        public GameplayAttribute IncomingDamageAttribute => GetAttribute(nameof(IncomingDamage));
        public GameplayAttribute IncomingHealingAttribute => GetAttribute(nameof(IncomingHealing));

        protected override void RegisterAttributes()
        {
            Register(nameof(Health), Health);
            Register(nameof(MaxHealth), MaxHealth);
            Register(nameof(IncomingHealing), IncomingHealing);
            Register(nameof(IncomingDamage), IncomingDamage);
        }

        public override void PreAttributeChange(GameplayAttribute attribute, ref float newValue)
        {
            // 对应源码：Health 夹到 [0, MaxHealth]
            if (attribute == HealthAttribute)
                newValue = UnityEngine.Mathf.Clamp(newValue, 0f, MaxHealth.CurrentValue);
        }

        public override void PostAttributeChange(GameplayAttribute attribute, float oldValue, float newValue)
        {
            // 对应源码：MaxHealth 变化时按比例调整 Health
            if (attribute == MaxHealthAttribute)
                AdjustAttributeForMaxChange(Health, oldValue, newValue, HealthAttribute);
        }

        public override void PostGameplayEffectExecute(GameplayEffectModCallbackData data)
        {
            // ---- 标准 Meta 伤害管线（见文件头说明）----
            if (data.Attribute == IncomingDamageAttribute)
            {
                float dmg = IncomingDamage.BaseValue;
                IncomingDamage.Initialize(0f); // 清空 Meta
                if (dmg > 0f)
                    Owner.ApplyModToAttributeBase(HealthAttribute, EAttributeModifierOp.Add, -dmg, data.Spec?.Context);
            }
            else if (data.Attribute == IncomingHealingAttribute)
            {
                float heal = IncomingHealing.BaseValue;
                IncomingHealing.Initialize(0f);
                if (heal > 0f)
                    Owner.ApplyModToAttributeBase(HealthAttribute, EAttributeModifierOp.Add, heal, data.Spec?.Context);
            }

            // 对应源码：Health 被直接结算后 clamp 到 [0, MaxHealth]
            if (data.Attribute == HealthAttribute)
            {
                Health.BaseValue = UnityEngine.Mathf.Clamp(Health.BaseValue, 0f, MaxHealth.CurrentValue);
                Health.CurrentValue = UnityEngine.Mathf.Clamp(Health.CurrentValue, 0f, MaxHealth.CurrentValue);
            }
        }

        /// <summary>MaxHealth 变化时按比例维持 Health 百分比。对应源码 AdjustAttributeForMaxChange。</summary>
        private void AdjustAttributeForMaxChange(GameplayAttributeData affected, float oldMax, float newMax, GameplayAttribute affectedAttr)
        {
            if (UnityEngine.Mathf.Approximately(oldMax, newMax)) return;
            float current = affected.CurrentValue;
            float newValue = oldMax > 0f ? current * newMax / oldMax : newMax;
            affected.BaseValue = newValue;
            affected.CurrentValue = newValue;
        }
    }
}
