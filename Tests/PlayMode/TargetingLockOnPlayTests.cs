// PlayMode 测试：TargetingSystemComponent 锁定系统。
//  A) Search 锁定正前方敌人；友军 / 背后(超视角) 被排除。
//  B) StaticSwitchToNewTarget 左右切换目标。
//  C) 目标死亡(Health<=0) → 自动解锁。
//  D) CanBeTargeted 过滤：超距 / 被屏蔽标签。
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Likeon.GAS;

namespace Likeon.GAS.PlayTests
{
    public class TargetingLockOnPlayTests
    {
        private static GameplayTag Tag(string s) => GameplayTag.RequestTag(s);

        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void Cleanup()
        {
            foreach (var go in _spawned) if (go != null) Object.Destroy(go);
            _spawned.Clear();
        }

        private GameObject NewGo(string name) { var go = new GameObject(name); _spawned.Add(go); return go; }

        // source：team 0 + 锁定组件
        private TargetingSystemComponent NewSource()
        {
            var go = NewGo("Source");
            (go.AddComponent<CombatTeamAgent>()).SetTeamId(0);
            return go.AddComponent<TargetingSystemComponent>();
        }

        // target：指定队伍 + 碰撞体 + ASC(+可选 Health)
        private GameObject NewTarget(string name, Vector3 pos, int team, bool withHealth, out AbilitySystemComponent asc)
        {
            var go = NewGo(name);
            go.transform.position = pos;
            (go.AddComponent<CombatTeamAgent>()).SetTeamId(team);
            (go.AddComponent<SphereCollider>()).radius = 0.5f;
            asc = go.AddComponent<AbilitySystemComponent>();
            if (withHealth) asc.AddAttributeSet(new AS_Health());
            return go;
        }

        // ============ A) 锁定正前方敌人，排除友军/背后 ============
        [UnityTest]
        public IEnumerator A_Search_LocksFrontEnemy()
        {
            var sys = NewSource();
            var enemy = NewTarget("Enemy", new Vector3(0, 0, 5), 1, true, out _);   // 正前方敌人
            NewTarget("Friend", new Vector3(1, 0, 5), 0, true, out _);              // 友军（同队）
            NewTarget("Behind", new Vector3(0, 0, -5), 1, true, out _);             // 背后敌人（超视角）
            yield return new WaitForFixedUpdate();

            sys.SearchForActorToTarget();

            Assert.IsTrue(sys.IsLockedOn, "应锁定到目标");
            Assert.AreSame(enemy, sys.TargetedActor, "应锁定正前方敌人（非友军、非背后）");
        }

        // ============ B) 左右切换 ============
        [UnityTest]
        public IEnumerator B_SwitchTarget_LeftRight()
        {
            var sys = NewSource();
            var front = NewTarget("Front", new Vector3(0, 0, 5), 1, true, out _);
            var right = NewTarget("Right", new Vector3(4, 0, 5), 1, true, out _);
            yield return new WaitForFixedUpdate();

            sys.SearchForActorToTarget();
            Assert.AreSame(front, sys.TargetedActor, "先锁正前方");

            sys.StaticSwitchToNewTarget(true);  // 向右
            Assert.AreSame(right, sys.TargetedActor, "右切应换到右侧目标");

            sys.StaticSwitchToNewTarget(false); // 向左
            Assert.AreSame(front, sys.TargetedActor, "左切应换回前方目标");
        }

        // ============ C) 目标死亡自动解锁 ============
        [UnityTest]
        public IEnumerator C_AutoDrop_OnTargetDeath()
        {
            var sys = NewSource();
            var enemy = NewTarget("Enemy", new Vector3(0, 0, 5), 1, true, out var enemyAsc);
            yield return new WaitForFixedUpdate();

            sys.SearchForActorToTarget();
            Assert.AreSame(enemy, sys.TargetedActor);

            // 把 Health 打到 0
            var hp = enemyAsc.GetAttributeSet<AS_Health>();
            enemyAsc.ApplyModToAttributeBase(hp.HealthAttribute, EAttributeModifierOp.Override, 0f);

            yield return null; // Update 检测目标失效 → 解锁
            Assert.IsFalse(sys.IsLockedOn, "目标死亡后应自动解锁");
        }

        // ============ D) CanBeTargeted 过滤 ============
        [UnityTest]
        public IEnumerator D_CanBeTargeted_RangeAndBlockedTag()
        {
            var sys = NewSource();
            sys.SearchRadius = 10f;
            var far = NewTarget("Far", new Vector3(0, 0, 50), 1, true, out _); // 超距
            var near = NewTarget("Near", new Vector3(0, 0, 5), 1, true, out var nearAsc);
            yield return new WaitForFixedUpdate();

            Assert.IsFalse(sys.CanBeTargeted(far), "超出搜索半径不可锁定");
            Assert.IsTrue(sys.CanBeTargeted(near), "范围内敌人可锁定");

            // 加屏蔽标签
            sys.BlockedTags.AddTag(Tag("State.Untargetable"));
            nearAsc.AddLooseGameplayTag(Tag("State.Untargetable"));
            Assert.IsFalse(sys.CanBeTargeted(near), "含屏蔽标签的目标不可锁定");
        }
    }
}
