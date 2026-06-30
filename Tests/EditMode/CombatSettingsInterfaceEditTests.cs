// EditMode 测试：#4 CombatSettings 归属检查开关 + #5 ICombatInterface 契约。
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Likeon.GAS;

namespace Likeon.GAS.Tests
{
    // 测试用战斗契约实现：最小存根，验证 CombatInterface.Get 能取到 + 几个方法 round-trip。
    public class TestCombatant : MonoBehaviour, ICombatInterface
    {
        public GameObject Target;
        public bool Dead;
        public GameplayTag MovementState;

        public GameObject GetCombatTargetActor() => Target;
        public Transform GetCombatTargetObject() => Target != null ? Target.transform : null;
        public bool QueryAbilityActions(GameplayTagContainer a, GameplayTagContainer s, GameplayTagContainer t, List<AbilityAction> o) { o?.Clear(); return false; }
        public IWeapon GetCurrentWeapon() => null;
        public bool IsHoldingBlockInput() => false;
        public Vector3 GetMovementInputDirection() => Vector3.forward;
        public void SetRotationMode(GameplayTag m) { }
        public GameplayTag GetRotationMode() => GameplayTag.None;
        public void SetMovementSet(GameplayTag m) { }
        public GameplayTag GetMovementSet() => GameplayTag.None;
        public void SetMovementState(GameplayTag m) { MovementState = m; }
        public GameplayTag GetMovementState() => MovementState;
        public GameplayTag GetDesiredMovementState() => MovementState;
        public void StartDeath() { Dead = true; }
        public void FinishDeath() { }
        public bool IsDead() => Dead;
    }

    public class CombatSettingsInterfaceEditTests
    {
        // ---- #4 CombatSettings 归属检查 ----

        [Test]
        public void DisableAffiliationCheck_AllowsCrossTeamDamage()
        {
            var a = new GameObject("A"); a.AddComponent<CombatTeamAgent>().SetTeamId(0);
            var b = new GameObject("B"); b.AddComponent<CombatTeamAgent>().SetTeamId(0); // 同队

            Assert.IsFalse(CombatTeamAgent.IsHostile(a, b), "默认：同队不可命中");

            var settings = ScriptableObject.CreateInstance<CombatSettings>();
            settings.DisableAffiliationCheck = true;
            CombatSettings.SetActive(settings);
            Assert.IsTrue(CombatTeamAgent.IsHostile(a, b), "禁用归属检查后：同队也可命中");

            CombatSettings.SetActive(null); // 还原，避免影响其它测试
            Object.DestroyImmediate(settings);
            Object.DestroyImmediate(a);
            Object.DestroyImmediate(b);
        }

        [Test]
        public void CombatSettings_DefaultActive_KeepsAffiliation()
        {
            CombatSettings.SetActive(null);
            Assert.IsFalse(CombatSettings.Active.DisableAffiliationCheck, "默认设置不禁用归属检查（保既有行为）");
        }

        // ---- #5 ICombatInterface 契约 ----

        [Test]
        public void CombatInterface_Get_FindsImplementor_AndRoundTrips()
        {
            var go = new GameObject("Combatant");
            var impl = go.AddComponent<TestCombatant>();

            var found = CombatInterface.Get(go);
            Assert.AreSame(impl, found, "应从 GameObject 取到 ICombatInterface 实现");

            // 父级查找
            var child = new GameObject("Child"); child.transform.SetParent(go.transform);
            Assert.AreSame(impl, CombatInterface.Get(child), "应能从子物体向上取到");

            // round-trip
            found.SetMovementState(GameplayTag.RequestTag("Movement.State.Sprint"));
            Assert.AreEqual("Movement.State.Sprint", found.GetMovementState().TagName);
            Assert.IsFalse(found.IsDead());
            found.StartDeath();
            Assert.IsTrue(found.IsDead(), "StartDeath 后 IsDead 应为真");

            Assert.IsNull(CombatInterface.Get((GameObject)null), "null 输入应返回 null");

            Object.DestroyImmediate(go);
        }
    }
}
