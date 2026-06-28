// Copyright 2026 Likeon All Rights Reserved.
// 忠实说明：源码 AS_Poise 只持有三个属性 + clamp + Max 变化按比例调整，
// **不含**"破防/硬直/恢复 tick"逻辑。
// 本文件实现这三个属性；削韧的"机制"（破防→硬直→按 PoiseRecover 恢复）见 PoiseComponent（标注为补充）。
// IncomingPoiseDamage 是与 AS_Health/AS_Stamina 一致的 Meta 入口，方便用 GE 施加削韧伤害（house style 补充）。

using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>
    /// 削韧（抗打击）属性集。Poise/MaxPoise/PoiseRecover（每秒恢复量）+ IncomingPoiseDamage（Meta）。
    ///（标签前缀与本包其余属性集一致）。
    /// </summary>
    public sealed class AS_Poise : AttributeSet
    {
        public static readonly GameplayTag TagPoise = GameplayTag.RequestTag("Attribute.PoiseSet.Poise");
        public static readonly GameplayTag TagMaxPoise = GameplayTag.RequestTag("Attribute.PoiseSet.MaxPoise");
        public static readonly GameplayTag TagPoiseRecover = GameplayTag.RequestTag("Attribute.PoiseSet.PoiseRecover");

        // 源码默认值：Poise=3, MaxPoise=3, PoiseRecover=1
        public readonly GameplayAttributeData Poise = new GameplayAttributeData(3f);
        public readonly GameplayAttributeData MaxPoise = new GameplayAttributeData(3f);
        public readonly GameplayAttributeData PoiseRecover = new GameplayAttributeData(1f);
        public readonly GameplayAttributeData IncomingPoiseDamage = new GameplayAttributeData(0f); // Meta（补充）

        public GameplayAttribute PoiseAttribute => GetAttribute(nameof(Poise));
        public GameplayAttribute MaxPoiseAttribute => GetAttribute(nameof(MaxPoise));
        public GameplayAttribute PoiseRecoverAttribute => GetAttribute(nameof(PoiseRecover));
        public GameplayAttribute IncomingPoiseDamageAttribute => GetAttribute(nameof(IncomingPoiseDamage));

        protected override void RegisterAttributes()
        {
            Register(nameof(Poise), Poise);
            Register(nameof(MaxPoise), MaxPoise);
            Register(nameof(PoiseRecover), PoiseRecover);
            Register(nameof(IncomingPoiseDamage), IncomingPoiseDamage);
        }

        public override void PreAttributeChange(GameplayAttribute attribute, ref float newValue)
        {
            // 对应源码：Poise 夹到 [0, MaxPoise]
            if (attribute == PoiseAttribute)
                newValue = Mathf.Clamp(newValue, 0f, MaxPoise.CurrentValue);
        }

        public override void PostAttributeChange(GameplayAttribute attribute, float oldValue, float newValue)
        {
            // 对应源码：MaxPoise 变化时按比例调整 Poise
            if (attribute == MaxPoiseAttribute && !Mathf.Approximately(oldValue, newValue))
            {
                float pct = oldValue > 0f ? Poise.CurrentValue / oldValue : 1f;
                float v = newValue * pct;
                Poise.BaseValue = v; Poise.CurrentValue = v;
            }
        }

        public override void PostGameplayEffectExecute(GameplayEffectModCallbackData data)
        {
            // IncomingPoiseDamage（Meta，补充）→ 扣 Poise
            if (data.Attribute == IncomingPoiseDamageAttribute)
            {
                float dmg = IncomingPoiseDamage.BaseValue;
                IncomingPoiseDamage.Initialize(0f);
                if (dmg > 0f)
                    Owner.ApplyModToAttributeBase(PoiseAttribute, EAttributeModifierOp.Add, -dmg);
            }

            // 对应源码：Poise 结算后 clamp 到 [0, MaxPoise]
            if (data.Attribute == PoiseAttribute)
            {
                Poise.BaseValue = Mathf.Clamp(Poise.BaseValue, 0f, MaxPoise.CurrentValue);
                Poise.CurrentValue = Mathf.Clamp(Poise.CurrentValue, 0f, MaxPoise.CurrentValue);
            }
        }
    }
}
