// Copyright 2026 Likeon All Rights Reserved.
// 战斗结算用的中间属性集。
// Damage / DamageNegation / GuardDamageNegation / StaminaDamage / StaminaDamageNegation。
// 真实伤害管线：攻击的 GameplayEffect 用 Execution 把 Damage 减去 (Guard)DamageNegation，
// 再映射成目标的 -Health / -Stamina（见 GameplayEffectExecutionCalculation 示例）。

namespace Likeon.GAS
{
    /// <summary>战斗中间属性集：承载一次结算的伤害/减伤中间量。</summary>
    public sealed class AS_Combat : AttributeSet
    {
        public readonly GameplayAttributeData Damage = new GameplayAttributeData(0f);
        public readonly GameplayAttributeData DamageNegation = new GameplayAttributeData(0f);
        public readonly GameplayAttributeData GuardDamageNegation = new GameplayAttributeData(0f);
        public readonly GameplayAttributeData StaminaDamage = new GameplayAttributeData(0f);
        public readonly GameplayAttributeData StaminaDamageNegation = new GameplayAttributeData(0f);

        public GameplayAttribute DamageAttribute => GetAttribute(nameof(Damage));
        public GameplayAttribute DamageNegationAttribute => GetAttribute(nameof(DamageNegation));
        public GameplayAttribute GuardDamageNegationAttribute => GetAttribute(nameof(GuardDamageNegation));
        public GameplayAttribute StaminaDamageAttribute => GetAttribute(nameof(StaminaDamage));
        public GameplayAttribute StaminaDamageNegationAttribute => GetAttribute(nameof(StaminaDamageNegation));

        protected override void RegisterAttributes()
        {
            Register(nameof(Damage), Damage);
            Register(nameof(DamageNegation), DamageNegation);
            Register(nameof(GuardDamageNegation), GuardDamageNegation);
            Register(nameof(StaminaDamage), StaminaDamage);
            Register(nameof(StaminaDamageNegation), StaminaDamageNegation);
        }
    }
}
