// EditMode 测试：Epic 教程审计剩余小缺口——PostAttributeBaseChange / TryActivateAbilityByClass /
// GetAbilitySystem(接口) / AbilityTask_WaitAttributeChange。
using NUnit.Framework;
using UnityEngine;
using Likeon.GAS;
using UnityEngine.TestTools;

namespace Likeon.GAS.Tests
{
    // ④ 带一个 meta 属性的测试属性集
    public class TestAS_Meta : AttributeSet
    {
        public GameplayAttributeData Health = new GameplayAttributeData(100f);
        public GameplayAttributeData IncomingDamage = new GameplayAttributeData(0f);
        protected override void RegisterAttributes()
        {
            Register("Health", Health);
            Register("IncomingDamage", IncomingDamage);
            MarkMeta("IncomingDamage");
        }
        public GameplayAttribute IncomingDamageAttribute => GetAttribute("IncomingDamage");
    }
    // ② 记录 PostAttributeBaseChange 调用的测试属性集
    public class TestAS_PostBase : AttributeSet
    {
        public GameplayAttributeData Value = new GameplayAttributeData(100f);
        public int Calls; public float LastOld, LastNew;
        protected override void RegisterAttributes() => Register("Value", Value);
        public override void PostAttributeBaseChange(GameplayAttribute a, float o, float n)
        { Calls++; LastOld = o; LastNew = n; }
        public GameplayAttribute ValueAttribute => GetAttribute("Value");
    }

    // ① 激活时起一个 WaitAttributeChange task 的测试技能
    public class TestAbility_WaitAttr : GameplayAbility
    {
        public GameplayAttribute WatchAttribute;
        public int ChangeCount; public AttributeChangeData Last;
        protected override void OnActivateAbility(GameplayEventData triggerData)
        {
            var t = AbilityTask_WaitAttributeChange.WaitAttributeChange(this, WatchAttribute);
            t.OnChanged += d => { ChangeCount++; Last = d; };
            t.Activate();
        }
    }

    // ③ 用接口指出 ASC 的代理对象
    public class TestAscProvider : MonoBehaviour, IAbilitySystemInterface
    {
        public AbilitySystemComponent Target;
        public AbilitySystemComponent GetAbilitySystemComponent() => Target;
    }

    public class EpicAuditGapsEditTests
    {
        // ===== ② PostAttributeBaseChange =====
        [Test]
        public void PostAttributeBaseChange_FiresOnInstantEffect()
        {
            var go = new GameObject("ASC");
            var asc = go.AddComponent<AbilitySystemComponent>();
            var set = new TestAS_PostBase();
            asc.AddAttributeSet(set);

            var ge = ScriptableObject.CreateInstance<GameplayEffect>();
            ge.DurationType = EGameplayEffectDurationType.Instant;
            ge.Modifiers.Add(new GameplayModifierInfo
            {
                Attribute = set.ValueAttribute,
                Operation = EAttributeModifierOp.Add,
                Magnitude = GameplayModifierMagnitude.ScalableFloat(25f)
            });
            asc.ApplyGameplayEffectToSelf(ge);

            Assert.AreEqual(1, set.Calls, "Instant 改 BaseValue 应触发 PostAttributeBaseChange");
            Assert.AreEqual(100f, set.LastOld, 0.01f);
            Assert.AreEqual(125f, set.LastNew, 0.01f, "100 + 25 = 125");

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(ge);
        }

        // ===== ③ TryActivateAbilityByClass =====
        [Test]
        public void TryActivateAbilityByClass_ActivatesMatchingType()
        {
            var go = new GameObject("ASC");
            var asc = go.AddComponent<AbilitySystemComponent>();
            var tmpl = ScriptableObject.CreateInstance<TestAbility_Instant>();
            asc.GiveAbility(tmpl);

            Assert.IsTrue(asc.TryActivateAbilityByClass<TestAbility_Instant>(), "按类型应激活已授予技能");
            Assert.IsTrue(asc.TryActivateAbilityByClass(typeof(TestAbility_Instant)), "Type 重载同样能激活");
            Assert.IsFalse(asc.TryActivateAbilityByClass<TestAbility_Hold>(), "未授予的类型应返回 false");

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(tmpl);
        }

        // ===== ③ GetAbilitySystem(接口优先，退回 GetComponent) =====
        [Test]
        public void GetAbilitySystem_PrefersInterface_ThenGetComponent()
        {
            var ascGo = new GameObject("ASC");
            var asc = ascGo.AddComponent<AbilitySystemComponent>();

            Assert.AreSame(asc, AbilitySystemComponent.GetAbilitySystem(ascGo), "同物体应经 GetComponent 解析");

            var proxyGo = new GameObject("Proxy");
            var proxy = proxyGo.AddComponent<TestAscProvider>();
            proxy.Target = asc;
            Assert.AreSame(asc, AbilitySystemComponent.GetAbilitySystem(proxyGo), "别处对象应经接口解析到指定 ASC");

            Assert.IsNull(AbilitySystemComponent.GetAbilitySystem(null), "null 应安全返回 null");

            Object.DestroyImmediate(ascGo);
            Object.DestroyImmediate(proxyGo);
        }

        // ===== ① AbilityTask_WaitAttributeChange =====
        [Test]
        public void WaitAttributeChange_FiresOnWatchedAttributeChange()
        {
            var go = new GameObject("ASC");
            var asc = go.AddComponent<AbilitySystemComponent>();
            var health = new AS_Health();
            asc.AddAttributeSet(health);

            var tmpl = ScriptableObject.CreateInstance<TestAbility_WaitAttr>();
            tmpl.WatchAttribute = health.MaxHealthAttribute;
            var handle = asc.GiveAbility(tmpl);
            var inst = (TestAbility_WaitAttr)asc.FindAbilitySpec(handle).Ability;
            asc.TryActivateAbility(handle); // 激活 → 起 WaitAttributeChange task

            var ge = ScriptableObject.CreateInstance<GameplayEffect>();
            ge.DurationType = EGameplayEffectDurationType.Instant;
            ge.Modifiers.Add(new GameplayModifierInfo
            {
                Attribute = health.MaxHealthAttribute,
                Operation = EAttributeModifierOp.Override,
                Magnitude = GameplayModifierMagnitude.ScalableFloat(150f)
            });
            asc.ApplyGameplayEffectToSelf(ge);

            Assert.AreEqual(1, inst.ChangeCount, "被监听属性(MaxHealth)变化应触发一次");
            Assert.AreEqual(150f, inst.Last.NewValue, 0.01f, "载荷应带新值");

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(tmpl);
            Object.DestroyImmediate(ge);
        }

        // ===== ④ Meta 属性标记 + 持续 modifier 误用警告 =====
        [Test]
        public void IsMeta_ReflectsMarkMeta()
        {
            var set = new TestAS_Meta();
            Assert.IsTrue(set.IsMeta("IncomingDamage"), "MarkMeta 标记的应为 meta");
            Assert.IsFalse(set.IsMeta("Health"), "普通属性不是 meta");
        }

        [Test]
        public void DurationModifierOnMetaAttribute_Warns()
        {
            var go = new GameObject("ASC");
            var asc = go.AddComponent<AbilitySystemComponent>();
            asc.AddAttributeSet(new TestAS_Meta());

            var ge = ScriptableObject.CreateInstance<GameplayEffect>();
            ge.name = "TestDurationGE";
            ge.DurationType = EGameplayEffectDurationType.HasDuration;
            ge.Duration = 5f;
            ge.Modifiers.Add(new GameplayModifierInfo
            {
                Attribute = new TestAS_Meta().IncomingDamageAttribute,
                Operation = EAttributeModifierOp.Add,
                Magnitude = GameplayModifierMagnitude.ScalableFloat(10f)
            });

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("meta 属性"));
            asc.ApplyGameplayEffectToSelf(ge);

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(ge);
        }

        [Test]
        public void InstantModifierOnMetaAttribute_DoesNotWarn()
        {
            var go = new GameObject("ASC");
            var asc = go.AddComponent<AbilitySystemComponent>();
            asc.AddAttributeSet(new TestAS_Meta());

            var ge = ScriptableObject.CreateInstance<GameplayEffect>();
            ge.DurationType = EGameplayEffectDurationType.Instant;
            ge.Modifiers.Add(new GameplayModifierInfo
            {
                Attribute = new TestAS_Meta().IncomingDamageAttribute,
                Operation = EAttributeModifierOp.Add,
                Magnitude = GameplayModifierMagnitude.ScalableFloat(10f)
            });

            asc.ApplyGameplayEffectToSelf(ge); // Instant 写 meta 属性是正常的，不应警告
            LogAssert.NoUnexpectedReceived();

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(ge);
        }
    }
}
