// EditMode 测试：#6 能力动作库（AbilityActionLibrary）按标签选动作 + CombatSystemComponent 接入。
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Likeon.GAS;

namespace Likeon.GAS.Tests
{
    public class AbilityActionLibraryEditTests
    {
        private static GameplayTagContainer Tags(params string[] names)
        {
            var c = new GameplayTagContainer();
            foreach (var n in names) c.AddTag(GameplayTag.RequestTag(n));
            return c;
        }

        private static AbilityActionSet MakeSet(string abilityTag, string baseStateName)
        {
            var set = new AbilityActionSet { AbilityTag = GameplayTag.RequestTag(abilityTag) };
            var a = AbilityAction.Default; a.StateName = baseStateName;
            set.Actions.Add(a);
            return set;
        }

        [Test]
        public void SelectBestAbilityActions_MatchesByAbilityTag()
        {
            var lib = ScriptableObject.CreateInstance<AbilityActionLibrary>();
            lib.ActionSets.Add(MakeSet("Ability.Attack.Light", "LightSwing"));

            var outActions = new List<AbilityAction>();
            bool ok = lib.SelectBestAbilityActions(Tags("Ability.Attack.Light"), null, null, outActions);

            Assert.IsTrue(ok, "命中能力标签应选到动作");
            Assert.AreEqual(1, outActions.Count);
            Assert.AreEqual("LightSwing", outActions[0].StateName);

            // 不匹配标签
            Assert.IsFalse(lib.SelectBestAbilityActions(Tags("Ability.Attack.Heavy"), null, null, outActions), "无匹配 set 应返回 false");
            Assert.AreEqual(0, outActions.Count, "无匹配时输出应清空");

            Object.DestroyImmediate(lib);
        }

        [Test]
        public void SelectBestAbilityActions_LayeredTakesPriority()
        {
            var lib = ScriptableObject.CreateInstance<AbilityActionLibrary>();
            var set = MakeSet("Ability.Attack.Light", "LightSwing");
            // Layered（query 为 null = 匹配所有）→ 应优先于基础 Actions
            var layeredAction = AbilityAction.Default; layeredAction.StateName = "LightSwing_Aerial";
            set.Layered.Add(new AbilityActionsWithQuery { Actions = new List<AbilityAction> { layeredAction } });
            lib.ActionSets.Add(set);

            var outActions = new List<AbilityAction>();
            Assert.IsTrue(lib.SelectBestAbilityActions(Tags("Ability.Attack.Light"), Tags("State.InAir"), null, outActions));
            Assert.AreEqual("LightSwing_Aerial", outActions[0].StateName, "满足查询的 Layered 应优先于基础动作");

            Object.DestroyImmediate(lib);
        }

        [Test]
        public void CombatSystemComponent_QueryAbilityActions_DelegatesToLibrary()
        {
            var go = new GameObject("Combatant");
            var combat = go.AddComponent<CombatSystemComponent>();

            var lib = ScriptableObject.CreateInstance<AbilityActionLibrary>();
            lib.ActionSets.Add(MakeSet("Ability.Attack.Light", "LightSwing"));
            combat.ActionLibrary = lib;

            var outActions = new List<AbilityAction>();
            Assert.IsTrue(combat.QueryAbilityActions(Tags("Ability.Attack.Light"), null, null, outActions), "组件应委托给库选到动作");
            Assert.AreEqual("LightSwing", outActions[0].StateName);

            // 未配库 → false
            combat.ActionLibrary = null;
            Assert.IsFalse(combat.QueryAbilityActions(Tags("Ability.Attack.Light"), null, null, outActions), "未配库应返回 false");

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(lib);
        }
    }
}
