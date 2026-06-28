// PlayMode 测试：WeaponComponent 武器系统。
//  A) 装备注入武器标签到持有者 ASC；卸下移除 + 事件。
//  B) 激活态切换：事件 + 驱动挂载的 MeleeAttackTrace。
//  C) 远程：FireProjectile 从枪口发射子弹命中目标。
//  D) IWeapon 接口访问。
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Likeon.GAS;

namespace Likeon.GAS.PlayTests
{
    public class WeaponPlayTests
    {
        private static GameplayTag Tag(string s) => GameplayTag.RequestTag(s);

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private readonly List<Object> _assets = new List<Object>();

        [TearDown]
        public void Cleanup()
        {
            foreach (var go in _spawned) if (go != null) Object.Destroy(go);
            foreach (var a in _assets) if (a != null) Object.Destroy(a);
            _spawned.Clear(); _assets.Clear();
        }

        private GameObject NewGo(string name) { var go = new GameObject(name); _spawned.Add(go); return go; }
        private T NewAsset<T>() where T : ScriptableObject { var a = ScriptableObject.CreateInstance<T>(); _assets.Add(a); return a; }

        private GameObject NewOwner()
        {
            var go = NewGo("Owner");
            (go.AddComponent<CombatTeamAgent>()).SetTeamId(0);
            go.AddComponent<AbilitySystemComponent>();
            return go;
        }

        private WeaponComponent NewWeapon(string tag)
        {
            var go = NewGo("Weapon");
            var w = go.AddComponent<WeaponComponent>();
            if (tag != null) w.WeaponTags.AddTag(Tag(tag));
            return w;
        }

        // ============ A) 装备注入/卸下移除武器标签 ============
        [UnityTest]
        public IEnumerator A_Equip_InjectsWeaponTags()
        {
            var owner = NewOwner();
            var asc = owner.GetComponent<AbilitySystemComponent>();
            var weapon = NewWeapon("Weapon.Sword");
            yield return null;

            int equipped = 0, unequipped = 0;
            weapon.OnEquipped += _ => equipped++;
            weapon.OnUnequipped += () => unequipped++;

            weapon.Equip(owner);
            Assert.IsTrue(asc.HasMatchingGameplayTag(Tag("Weapon.Sword")), "装备应把武器标签注入持有者");
            Assert.AreSame(owner, weapon.WeaponOwner);
            Assert.AreEqual(1, equipped);

            weapon.Unequip();
            Assert.IsFalse(asc.HasMatchingGameplayTag(Tag("Weapon.Sword")), "卸下应移除武器标签");
            Assert.AreEqual(1, unequipped);
        }

        // ============ B) 激活态切换 + 驱动挥砍判定 ============
        [UnityTest]
        public IEnumerator B_SetActive_DrivesMeleeTrace()
        {
            var owner = NewOwner();
            var weapon = NewWeapon("Weapon.Sword");
            var trace = weapon.gameObject.AddComponent<MeleeAttackTrace>();
            trace.Entries.Add(new MeleeAttackTrace.AttackTraceEntry
            {
                Attack = NewAsset<AttackDefinition>(),
                Trace = new CollisionTraceDefinition
                {
                    SocketTransforms = new List<Transform> { weapon.transform },
                    TraceRadius = 0.5f
                }
            });
            weapon.MeleeTrace = trace;
            weapon.Equip(owner);
            yield return null;

            bool lastEvent = false; int events = 0;
            weapon.OnWeaponActiveStateChanged += a => { lastEvent = a; events++; };

            weapon.SetWeaponActive(true);
            Assert.IsTrue(weapon.IsWeaponActive, "应进入激活态");
            Assert.IsTrue(trace.IsTracing, "激活应开启挥砍判定");
            Assert.IsTrue(lastEvent);

            weapon.SetWeaponActive(false);
            Assert.IsFalse(weapon.IsWeaponActive, "应退出激活态");
            Assert.IsFalse(trace.IsTracing, "停用应关闭挥砍判定");
            Assert.AreEqual(2, events, "状态切换两次应各触发一次事件");
        }

        // ============ C) 远程：从枪口发射命中目标 ============
        [UnityTest]
        public IEnumerator C_FireProjectile_FromMuzzle()
        {
            var owner = NewOwner();
            var weapon = NewWeapon("Weapon.Gun");
            weapon.transform.position = owner.transform.position; // 枪口=武器自身，朝 +Z
            weapon.Equip(owner);

            // 目标
            var target = NewGo("Target");
            target.transform.position = new Vector3(0, 0, 5);
            (target.AddComponent<CombatTeamAgent>()).SetTeamId(1);
            (target.AddComponent<SphereCollider>()).radius = 0.5f;
            var tAsc = target.AddComponent<AbilitySystemComponent>();
            var health = new AS_Health(); tAsc.AddAttributeSet(health);
            yield return new WaitForFixedUpdate();

            var ge = NewAsset<GameplayEffect>();
            ge.DurationType = EGameplayEffectDurationType.Instant;
            ge.Modifiers.Add(new GameplayModifierInfo
            {
                Attribute = GameplayAttribute.From<AS_Health>("IncomingDamage"),
                Operation = EAttributeModifierOp.Add,
                Magnitude = GameplayModifierMagnitude.SetByCaller(Tag("Data.Damage"))
            });
            var atk = NewAsset<AttackDefinition>();
            atk.TargetEffect = ge;
            atk.SetByCallerMagnitudes.Add(new SetByCallerMagnitude { Tag = Tag("Data.Damage"), Value = 15f });

            var def = NewAsset<BulletDefinition>();
            def.InitialSpeed = 15f; def.HitRadius = 0.2f; def.Duration = 5f; def.Attack = atk;

            float before = health.Health.CurrentValue;
            var bullets = weapon.FireProjectile(def);
            Assert.AreEqual(1, bullets.Count);
            var bullet = bullets[0]; bullet.AutoTick = false; _spawned.Add(bullet.gameObject);

            for (int i = 0; i < 50 && bullet != null && bullet.IsActive; i++) bullet.Tick(0.05f);

            Assert.AreEqual(before - 15f, health.Health.CurrentValue, 0.1f, "枪口发射的子弹应命中扣血 15");
        }

        // ============ D) IWeapon 接口 ============
        [UnityTest]
        public IEnumerator D_IWeapon_Interface()
        {
            var weapon = NewWeapon("Weapon.Bow");
            yield return null;

            IWeapon iw = weapon;
            Assert.IsTrue(iw.WeaponTags.HasTag(Tag("Weapon.Bow")), "接口应暴露武器标签");
            Assert.IsNotNull(iw.MuzzleTransform, "接口应给出枪口变换");
            Assert.IsFalse(iw.IsWeaponActive, "初始未激活");
        }
    }
}
