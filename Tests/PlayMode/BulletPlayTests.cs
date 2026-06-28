// PlayMode 测试：Bullet 投射物（BulletDefinition + BulletInstance + BulletLauncher）。
//  A) 单发命中敌人 → 施伤 → 失效。
//  B) 散射：BulletCount=3 生成 3 发不同朝向。
//  C) 穿透：PenetrateCharacter 决定命中一个还是穿透命中多个。
//  D) 生命到期 → 失效。
//  E) 友军穿过（阵营过滤，不施伤）。
// 注：用固定步长 Tick(dt) 驱动，绕开 headless 下 Time.deltaTime 极小的问题。
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Likeon.GAS;

namespace Likeon.GAS.PlayTests
{
    public class BulletPlayTests
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

        private GameObject NewTarget(Vector3 pos, int team, out AS_Health health)
        {
            var go = NewGo("Target");
            go.transform.position = pos;
            (go.AddComponent<CombatTeamAgent>()).SetTeamId(team);
            (go.AddComponent<SphereCollider>()).radius = 0.5f;
            var asc = go.AddComponent<AbilitySystemComponent>();
            health = new AS_Health(); asc.AddAttributeSet(health);
            return go;
        }

        private AttackDefinition MakeAttack(float dmg)
        {
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
            atk.SetByCallerMagnitudes.Add(new SetByCallerMagnitude { Tag = Tag("Data.Damage"), Value = dmg });
            return atk;
        }

        // 发射并纳入清理 + 关闭自动 Tick（测试手动驱动）
        private List<BulletInstance> FireTracked(BulletDefinition def, GameObject owner, Vector3 origin, Vector3 dir)
        {
            var asc = owner.GetComponent<AbilitySystemComponent>();
            var list = BulletLauncher.Fire(def, owner, asc, origin, dir);
            foreach (var b in list) { _spawned.Add(b.gameObject); b.AutoTick = false; }
            return list;
        }

        private void Drive(BulletInstance b, float dt, int maxSteps)
        {
            for (int i = 0; i < maxSteps && b != null && b.IsActive; i++) b.Tick(dt);
        }

        // ============ A) 单发命中施伤 ============
        [UnityTest]
        public IEnumerator A_Bullet_HitsAndDamages()
        {
            var owner = NewOwner();
            var target = NewTarget(new Vector3(0, 0, 5), 1, out var health);
            yield return new WaitForFixedUpdate();

            var def = NewAsset<BulletDefinition>();
            def.InitialSpeed = 15f; def.HitRadius = 0.2f; def.Duration = 5f;
            def.Attack = MakeAttack(20f);

            var bullet = FireTracked(def, owner, owner.transform.position, Vector3.forward)[0];
            int hits = 0; bullet.OnHit += (b, t, p) => hits++;
            float before = health.Health.CurrentValue;

            Drive(bullet, 0.05f, 50);

            Assert.AreEqual(1, hits, "应命中一次");
            Assert.AreEqual(before - 20f, health.Health.CurrentValue, 0.1f, "命中应扣血 20");
            Assert.IsFalse(bullet == null ? false : bullet.IsActive, "命中后子弹应失效");
        }

        // ============ B) 散射 ============
        [UnityTest]
        public IEnumerator B_Spread_FiresMultiple()
        {
            var owner = NewOwner();
            yield return null;

            var def = NewAsset<BulletDefinition>();
            def.BulletCount = 3; def.LaunchAngleInterval = 15f;

            var list = FireTracked(def, owner, owner.transform.position, Vector3.forward);
            Assert.AreEqual(3, list.Count, "应生成 3 发");
            Assert.Greater(Vector3.Angle(list[0].transform.forward, list[1].transform.forward), 1f, "相邻子弹朝向应不同");
            Assert.Greater(Vector3.Angle(list[1].transform.forward, list[2].transform.forward), 1f, "相邻子弹朝向应不同");
        }

        // ============ C) 穿透 ============
        [UnityTest]
        public IEnumerator C_Penetration()
        {
            var owner = NewOwner();
            NewTarget(new Vector3(0, 0, 3), 1, out _);
            NewTarget(new Vector3(0, 0, 6), 1, out _);
            yield return new WaitForFixedUpdate();

            // 穿透：命中 2 个
            var penDef = NewAsset<BulletDefinition>();
            penDef.InitialSpeed = 15f; penDef.HitRadius = 0.2f; penDef.Duration = 5f; penDef.PenetrateCharacter = true;
            var pen = FireTracked(penDef, owner, owner.transform.position, Vector3.forward)[0];
            int penHits = 0; pen.OnHit += (b, t, p) => penHits++;
            Drive(pen, 0.05f, 60);
            Assert.AreEqual(2, penHits, "穿透应命中 2 个目标");

            // 非穿透：命中 1 个即停
            var stopDef = NewAsset<BulletDefinition>();
            stopDef.InitialSpeed = 15f; stopDef.HitRadius = 0.2f; stopDef.Duration = 5f; stopDef.PenetrateCharacter = false;
            var stop = FireTracked(stopDef, owner, owner.transform.position, Vector3.forward)[0];
            int stopHits = 0; stop.OnHit += (b, t, p) => stopHits++;
            Drive(stop, 0.05f, 60);
            Assert.AreEqual(1, stopHits, "非穿透命中 1 个即停");
        }

        // ============ D) 生命到期 ============
        [UnityTest]
        public IEnumerator D_Lifetime_Expires()
        {
            var owner = NewOwner();
            yield return null;

            var def = NewAsset<BulletDefinition>();
            def.InitialSpeed = 10f; def.Duration = 0.2f; // 无目标
            var bullet = FireTracked(def, owner, owner.transform.position, Vector3.forward)[0];
            bool expired = false; bullet.OnExpired += b => expired = true;

            Drive(bullet, 0.05f, 20); // 0.05*N 超过 0.2s
            Assert.IsTrue(expired, "生命到期应失效");
        }

        // ============ E) 友军穿过 ============
        [UnityTest]
        public IEnumerator E_Friendly_PassThrough()
        {
            var owner = NewOwner();
            var friend = NewTarget(new Vector3(0, 0, 5), 0, out var friendHealth); // 同队
            yield return new WaitForFixedUpdate();

            var def = NewAsset<BulletDefinition>();
            def.InitialSpeed = 15f; def.HitRadius = 0.2f; def.Duration = 5f;
            def.Attack = MakeAttack(20f);
            var bullet = FireTracked(def, owner, owner.transform.position, Vector3.forward)[0];
            int hits = 0; bullet.OnHit += (b, t, p) => hits++;

            Drive(bullet, 0.05f, 30);

            Assert.AreEqual(0, hits, "友军不应被命中");
            Assert.AreEqual(friendHealth.MaxHealth.CurrentValue, friendHealth.Health.CurrentValue, 0.1f, "友军不应掉血");
        }
    }
}
