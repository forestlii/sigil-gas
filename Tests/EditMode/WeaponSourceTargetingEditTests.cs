// EditMode 测试：#30 武器来源对象（SourceObject）+ 瞄准开关（ToggleTargeting）+ 多 trace 段（额外判定实例同开同关）。
using NUnit.Framework;
using UnityEngine;
using Likeon.GAS;

namespace Likeon.GAS.Tests
{
    public class WeaponSourceTargetingEditTests
    {
        private static WeaponComponent MakeWeapon(out GameObject go)
        {
            go = new GameObject("Weapon");
            return go.AddComponent<WeaponComponent>();
        }

        private static MeleeAttackTrace MakeTraceWithEntry(out GameObject go)
        {
            go = new GameObject("Trace");
            var t = go.AddComponent<MeleeAttackTrace>();
            t.Entries.Add(new MeleeAttackTrace.AttackTraceEntry()); // 至少 1 个配置，下标 0 可用
            return t;
        }

        [Test]
        public void SourceObject_RoundTrips()
        {
            var weapon = MakeWeapon(out var go);
            Assert.IsNull(weapon.SourceObject, "默认无来源对象");

            var data = ScriptableObject.CreateInstance<AttackDefinition>(); // 任意 UnityEngine.Object 作"背靠数据"
            weapon.SourceObject = data;
            Assert.AreSame(data, ((IWeapon)weapon).SourceObject, "SourceObject 经 IWeapon 应可取回所设来源对象");

            Object.DestroyImmediate(data);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void Targeting_TogglesAndFiresEventOnce()
        {
            var weapon = MakeWeapon(out var go);
            int fired = 0;
            bool last = false;
            weapon.OnTargetingChanged += v => { fired++; last = v; };

            Assert.IsFalse(weapon.IsTargeting, "默认非瞄准态");

            weapon.SetTargeting(true);
            Assert.IsTrue(weapon.IsTargeting);
            Assert.AreEqual(1, fired, "进入瞄准应触发一次事件");
            Assert.IsTrue(last);

            weapon.SetTargeting(true); // 幂等：同值不再触发
            Assert.AreEqual(1, fired, "重复设同值不应再触发");

            weapon.ToggleTargeting(); // → false
            Assert.IsFalse(weapon.IsTargeting);
            Assert.AreEqual(2, fired);
            Assert.IsFalse(last);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void MultiTrace_DrivesPrimaryAndAdditionalTogether()
        {
            var weapon = MakeWeapon(out var wgo);
            var primary = MakeTraceWithEntry(out var pgo);
            var extra = MakeTraceWithEntry(out var ego);

            weapon.MeleeTrace = primary;
            weapon.AdditionalTraces.Add(new WeaponComponent.WeaponTraceInstance { Trace = extra, EntryIndex = 0 });

            Assert.IsFalse(primary.IsTracing);
            Assert.IsFalse(extra.IsTracing);

            weapon.SetWeaponActive(true);
            Assert.IsTrue(primary.IsTracing, "激活应驱动主判定");
            Assert.IsTrue(extra.IsTracing, "激活应同时驱动额外判定段（多 trace）");

            weapon.SetWeaponActive(false);
            Assert.IsFalse(primary.IsTracing, "停用应关主判定");
            Assert.IsFalse(extra.IsTracing, "停用应同时关额外判定段");

            Object.DestroyImmediate(wgo);
            Object.DestroyImmediate(pgo);
            Object.DestroyImmediate(ego);
        }

        [Test]
        public void Unequip_ResetsTargeting()
        {
            var weapon = MakeWeapon(out var go);
            weapon.SetTargeting(true);
            Assert.IsTrue(weapon.IsTargeting);

            weapon.Unequip(); // 卸下应复位瞄准态
            Assert.IsFalse(weapon.IsTargeting, "卸下武器应复位瞄准态");

            Object.DestroyImmediate(go);
        }
    }
}
