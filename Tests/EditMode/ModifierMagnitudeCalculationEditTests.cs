// EditMode 测试：自定义 ModifierMagnitudeCalculation（MMC）——CustomCalculationClass magnitude 分支。
// 覆盖：固定值 / 读目标属性做联动公式 / 按 spec.Level 缩放 / null 安全 / source≠target 传递 / 老分支不回归。
using NUnit.Framework;
using UnityEngine;
using Likeon.GAS;

namespace Likeon.GAS.Tests
{
    // 返回一个固定值（Override 用）。
    public class TestMMC_Fixed : ModifierMagnitudeCalculation
    {
        public float Value;
        public override float CalculateBaseMagnitude(GameplayEffectSpec spec, AbilitySystemComponent sourceASC, AbilitySystemComponent targetASC)
            => Value;
    }

    // 读目标某属性 × 系数（模拟"当前值的百分比"这类属性联动公式）。
    public class TestMMC_ReadTargetAttribute : ModifierMagnitudeCalculation
    {
        public GameplayAttribute Attribute;
        public float Coefficient = 1f;
        public override float CalculateBaseMagnitude(GameplayEffectSpec spec, AbilitySystemComponent sourceASC, AbilitySystemComponent targetASC)
            => (targetASC != null ? targetASC.GetAttributeValue(Attribute) : 0f) * Coefficient;
    }

    // 按等级缩放：level × 每级值。
    public class TestMMC_ByLevel : ModifierMagnitudeCalculation
    {
        public float PerLevel = 10f;
        public override float CalculateBaseMagnitude(GameplayEffectSpec spec, AbilitySystemComponent sourceASC, AbilitySystemComponent targetASC)
            => (spec?.Level ?? 1) * PerLevel;
    }

    // 记录被传入的 source/target ASC，供验证传参正确。
    public class TestMMC_Record : ModifierMagnitudeCalculation
    {
        public AbilitySystemComponent SeenSource;
        public AbilitySystemComponent SeenTarget;
        public override float CalculateBaseMagnitude(GameplayEffectSpec spec, AbilitySystemComponent sourceASC, AbilitySystemComponent targetASC)
        {
            SeenSource = sourceASC;
            SeenTarget = targetASC;
            return 0f;
        }
    }

    public class ModifierMagnitudeCalculationEditTests
    {
        [Test]
        public void CustomMMC_FixedValue_OverridesAttribute()
        {
            var go = new GameObject("ASC");
            var asc = go.AddComponent<AbilitySystemComponent>();
            var health = new AS_Health();
            asc.AddAttributeSet(health);

            var mmc = ScriptableObject.CreateInstance<TestMMC_Fixed>();
            mmc.Value = 42f;

            var ge = ScriptableObject.CreateInstance<GameplayEffect>();
            ge.DurationType = EGameplayEffectDurationType.Instant;
            ge.Modifiers.Add(new GameplayModifierInfo
            {
                Attribute = health.MaxHealthAttribute,
                Operation = EAttributeModifierOp.Override,
                Magnitude = GameplayModifierMagnitude.Custom(mmc)
            });

            asc.ApplyGameplayEffectToSelf(ge);
            Assert.AreEqual(42f, health.MaxHealth.CurrentValue, 0.01f, "MMC 返回值应经 Override 写入属性");

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(ge);
            Object.DestroyImmediate(mmc);
        }

        [Test]
        public void CustomMMC_ReadsTargetAttribute_ForLinkedFormula()
        {
            var go = new GameObject("ASC");
            var asc = go.AddComponent<AbilitySystemComponent>();
            var health = new AS_Health();
            asc.AddAttributeSet(health); // MaxHealth = 100

            var mmc = ScriptableObject.CreateInstance<TestMMC_ReadTargetAttribute>();
            mmc.Attribute = health.MaxHealthAttribute;
            mmc.Coefficient = 0.5f; // 目标 MaxHealth 的一半

            var ge = ScriptableObject.CreateInstance<GameplayEffect>();
            ge.DurationType = EGameplayEffectDurationType.Instant;
            ge.Modifiers.Add(new GameplayModifierInfo
            {
                Attribute = health.HealthAttribute,
                Operation = EAttributeModifierOp.Override,
                Magnitude = GameplayModifierMagnitude.Custom(mmc)
            });

            asc.ApplyGameplayEffectToSelf(ge);
            Assert.AreEqual(50f, health.Health.CurrentValue, 0.01f, "MMC 应读到目标 MaxHealth(100)×0.5=50 并写入 Health");

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(ge);
            Object.DestroyImmediate(mmc);
        }

        [Test]
        public void CustomMMC_ScalesWithSpecLevel()
        {
            var go = new GameObject("ASC");
            var asc = go.AddComponent<AbilitySystemComponent>();
            var health = new AS_Health();
            asc.AddAttributeSet(health);

            var mmc = ScriptableObject.CreateInstance<TestMMC_ByLevel>();
            mmc.PerLevel = 50f;

            var ge = ScriptableObject.CreateInstance<GameplayEffect>();
            ge.DurationType = EGameplayEffectDurationType.Instant;
            ge.Modifiers.Add(new GameplayModifierInfo
            {
                Attribute = health.MaxHealthAttribute,
                Operation = EAttributeModifierOp.Override,
                Magnitude = GameplayModifierMagnitude.Custom(mmc)
            });

            asc.ApplyGameplayEffectToSelf(ge, 3); // level 3 → 3×50 = 150
            Assert.AreEqual(150f, health.MaxHealth.CurrentValue, 0.01f, "MMC 应能读 spec.Level 缩放（3×50）");

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(ge);
            Object.DestroyImmediate(mmc);
        }

        [Test]
        public void CustomMMC_NullCalculation_ReturnsZero_NoCrash()
        {
            // 选了 CustomCalculationClass 但没挂 MMC 资产 → 安全返回 0，不抛异常
            var mag = GameplayModifierMagnitude.Custom(null);
            Assert.AreEqual(0f, mag.Evaluate(null), 0.01f, "空 MMC 应返回 0 而非崩溃");
        }

        [Test]
        public void CustomMMC_PassesSourceAndTargetDistinctly()
        {
            var sourceGo = new GameObject("Source");
            var sourceAsc = sourceGo.AddComponent<AbilitySystemComponent>();
            var targetGo = new GameObject("Target");
            var targetAsc = targetGo.AddComponent<AbilitySystemComponent>();
            targetAsc.AddAttributeSet(new AS_Health());

            var mmc = ScriptableObject.CreateInstance<TestMMC_Record>();

            var ge = ScriptableObject.CreateInstance<GameplayEffect>();
            ge.DurationType = EGameplayEffectDurationType.Instant;
            var healthAttr = new AS_Health().IncomingDamageAttribute;
            ge.Modifiers.Add(new GameplayModifierInfo
            {
                Attribute = healthAttr,
                Operation = EAttributeModifierOp.Add,
                Magnitude = GameplayModifierMagnitude.Custom(mmc)
            });

            // 手动构造 source≠target 的 spec，施加到 target
            var spec = new GameplayEffectSpec(ge, new GameplayEffectContext(sourceAsc), 1);
            targetAsc.ApplyGameplayEffectSpecToSelf(spec);

            Assert.AreSame(sourceAsc, mmc.SeenSource, "MMC 应收到效果来源 ASC");
            Assert.AreSame(targetAsc, mmc.SeenTarget, "MMC 应收到效果目标 ASC");

            Object.DestroyImmediate(sourceGo);
            Object.DestroyImmediate(targetGo);
            Object.DestroyImmediate(ge);
            Object.DestroyImmediate(mmc);
        }

        [Test]
        public void LegacyMagnitudeBranches_StillWork_AfterSignatureChange()
        {
            // Evaluate 加了可选 ASC 参数后，老的单参调用与老分支不应回归
            Assert.AreEqual(25f, GameplayModifierMagnitude.ScalableFloat(25f).Evaluate(null), 0.01f);

            var ge = ScriptableObject.CreateInstance<GameplayEffect>();
            var spec = new GameplayEffectSpec(ge, new GameplayEffectContext(), 3);
            Assert.AreEqual(35f, GameplayModifierMagnitude.ScalableFloat(25f, 5f).Evaluate(spec), 0.01f, "base25 + 5×(3-1)=35");
            Object.DestroyImmediate(ge);
        }
    }
}
