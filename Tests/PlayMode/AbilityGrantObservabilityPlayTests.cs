// PlayMode 测试：ASC 技能授予/移除可观测性——供 loadout 驱动的技能栏订阅。
//  A) GiveAbility 触发 OnAbilityGiven + 进入只读枚举；
//  B) ClearAbility 触发 OnAbilityRemoved + 退出枚举，且回调在销毁前（spec.Ability 仍可读）；
//  C) GrantLoadout 批量授予逐个触发 OnAbilityGiven；
//  D) RevokeFrom 整批撤销逐个触发 OnAbilityRemoved。
// 放 PlayMode：ClearAbility 内部用 Object.Destroy，EditMode 禁用。
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Likeon.GAS;

namespace Likeon.GAS.PlayTests
{
    // 激活即结束的最小测试技能
    public class GrantTestAbility : GameplayAbility
    {
        protected override void OnActivateAbility(GameplayEventData triggerData) => EndAbility();
    }

    public class AbilityGrantObservabilityPlayTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private readonly List<Object> _assets = new List<Object>();

        [TearDown]
        public void Cleanup()
        {
            foreach (var go in _spawned) if (go != null) Object.Destroy(go);
            foreach (var a in _assets) if (a != null) Object.Destroy(a);
            _spawned.Clear(); _assets.Clear();
        }

        private AbilitySystemComponent NewASC()
        {
            var go = new GameObject("ASC"); _spawned.Add(go);
            return go.AddComponent<AbilitySystemComponent>();
        }

        private GrantTestAbility NewAbilityAsset()
        {
            var a = ScriptableObject.CreateInstance<GrantTestAbility>(); _assets.Add(a);
            return a;
        }

        // ============ A) GiveAbility 触发 Given + 只读枚举 ============
        [UnityTest]
        public IEnumerator A_GiveAbility_FiresGiven_AndAppearsInEnumeration()
        {
            var asc = NewASC();
            GameplayAbilitySpec given = null;
            int givenCount = 0;
            asc.OnAbilityGiven += s => { given = s; givenCount++; };

            var handle = asc.GiveAbility(NewAbilityAsset());

            Assert.IsTrue(handle.IsValid, "授予应返回有效句柄");
            Assert.AreEqual(1, givenCount, "应恰好触发一次 OnAbilityGiven");
            Assert.IsNotNull(given, "Given 回调应携带 spec");
            Assert.IsTrue(given.Handle.Equals(handle), "回调 spec 句柄应与返回句柄一致");
            Assert.AreEqual(1, asc.GetGrantedAbilities().Count, "只读枚举应含 1 个已授予技能");
            yield return null;
        }

        // ============ B) ClearAbility 触发 Removed + 回调在销毁前 ============
        [UnityTest]
        public IEnumerator B_ClearAbility_FiresRemoved_BeforeDestroy()
        {
            var asc = NewASC();
            GameplayAbilitySpec removed = null;
            bool abilityAliveInCallback = false;
            int removedCount = 0;
            asc.OnAbilityRemoved += s =>
            {
                removed = s; removedCount++;
                abilityAliveInCallback = s.Ability != null; // 回调应在 Destroy 前，能读到实例
            };

            var handle = asc.GiveAbility(NewAbilityAsset());
            Assert.AreEqual(1, asc.GetGrantedAbilities().Count);

            asc.ClearAbility(handle);

            Assert.AreEqual(1, removedCount, "应恰好触发一次 OnAbilityRemoved");
            Assert.IsNotNull(removed, "Removed 回调应携带 spec");
            Assert.IsTrue(abilityAliveInCallback, "回调应在销毁前触发，spec.Ability 仍可读");
            Assert.AreEqual(0, asc.GetGrantedAbilities().Count, "移除后枚举应清空");
            Assert.IsNull(asc.FindAbilitySpec(handle), "按句柄已查不到");
            yield return null;
        }

        // ============ C) GrantLoadout 批量逐个触发 Given ============
        [UnityTest]
        public IEnumerator C_GrantLoadout_FiresGivenPerAbility()
        {
            var asc = NewASC();
            int givenCount = 0;
            asc.OnAbilityGiven += _ => givenCount++;

            var loadout = ScriptableObject.CreateInstance<AbilityLoadout>(); _assets.Add(loadout);
            loadout.GrantedAbilities.Add(new AbilityLoadout.GrantedAbility { Ability = NewAbilityAsset(), Level = 1 });
            loadout.GrantedAbilities.Add(new AbilityLoadout.GrantedAbility { Ability = NewAbilityAsset(), Level = 1 });

            asc.GrantLoadout(loadout);

            Assert.AreEqual(2, givenCount, "loadout 两个技能应各触发一次 Given");
            Assert.AreEqual(2, asc.GetGrantedAbilities().Count, "枚举应含 2 个已授予技能");
            yield return null;
        }

        // ============ D) RevokeFrom 整批撤销逐个触发 Removed ============
        [UnityTest]
        public IEnumerator D_RevokeLoadout_FiresRemovedPerAbility()
        {
            var asc = NewASC();
            int removedCount = 0;
            asc.OnAbilityRemoved += _ => removedCount++;

            var loadout = ScriptableObject.CreateInstance<AbilityLoadout>(); _assets.Add(loadout);
            loadout.GrantedAbilities.Add(new AbilityLoadout.GrantedAbility { Ability = NewAbilityAsset(), Level = 1 });
            loadout.GrantedAbilities.Add(new AbilityLoadout.GrantedAbility { Ability = NewAbilityAsset(), Level = 1 });

            var handles = asc.GrantLoadout(loadout);
            Assert.AreEqual(2, asc.GetGrantedAbilities().Count);

            handles.RevokeFrom(asc);

            Assert.AreEqual(2, removedCount, "撤销两个技能应各触发一次 Removed");
            Assert.AreEqual(0, asc.GetGrantedAbilities().Count, "撤销后枚举应清空");
            yield return null;
        }
    }
}
