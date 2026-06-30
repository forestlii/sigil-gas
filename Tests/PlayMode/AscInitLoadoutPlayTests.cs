// PlayMode 测试：ASC.Awake 自动应用 initialLoadouts（对齐 UE GGA_AbilitySystem.DefaultAbilitySet）。
// 放 PlayMode：Awake 仅在播放时触发；用 SetActive(false)→配置→SetActive(true) 赶在 Awake 前填 loadout。
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Likeon.GAS;

namespace Likeon.GAS.PlayTests
{
    public class AscInitLoadoutPlayTests
    {
        [UnityTest]
        public IEnumerator Awake_AppliesInitialLoadouts_AddsAttributeSet()
        {
            var go = new GameObject("ASC");
            go.SetActive(false); // 赶在 Awake 前配 initialLoadouts
            var asc = go.AddComponent<AbilitySystemComponent>();

            var loadout = ScriptableObject.CreateInstance<AbilityLoadout>();
            loadout.GrantedAttributeSets.Add(new AS_Health());
            asc.InitialLoadouts.Add(loadout);

            go.SetActive(true); // 触发 Awake → 自动 GrantLoadout
            yield return null;

            Assert.IsNotNull(asc.GetAttributeSet<AS_Health>(), "Awake 应自动应用 initialLoadouts、添加 AS_Health");
            Assert.AreEqual(1, asc.GrantedLoadoutHandles.Count, "应记录 1 个 loadout 授予句柄");

            Object.Destroy(go);
            Object.Destroy(loadout);
            yield return null;
        }
    }
}
