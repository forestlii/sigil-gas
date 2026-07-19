// EditMode 测试：AddAttributeSet 同类型去重 —— P1-A5 回归。
// 修复前：只挡同一实例（List.Contains 引用相等），不挡同类型的不同实例；
// 两个 Loadout 勾同一属性集类型 → 两个实例都进 _attributeSets，GetAttributeSet 只命中第一个，
// 后一套读写静默无效。
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Likeon.GAS;

namespace Likeon.GAS.Tests
{
    public class AttributeSetDedupeEditTests
    {
        [Test]
        public void AddAttributeSet_SameType_Twice_IsRejectedAndWarns()
        {
            var go = new GameObject("ASC");
            var asc = go.AddComponent<AbilitySystemComponent>();
            try
            {
                var first = new AS_Health();
                asc.AddAttributeSet(first);

                // 第二个同类型实例应被拒绝并告警
                LogAssert.Expect(LogType.Warning, new Regex("重复的属性集类型"));
                asc.AddAttributeSet(new AS_Health());

                Assert.AreSame(first, asc.GetAttributeSet<AS_Health>(),
                    "属性解析应仍命中第一个实例，第二个被拒绝");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void AddAttributeSet_DifferentTypes_BothKept()
        {
            var go = new GameObject("ASC");
            var asc = go.AddComponent<AbilitySystemComponent>();
            try
            {
                asc.AddAttributeSet(new AS_Health());
                asc.AddAttributeSet(new AS_Stamina()); // 不同类型 → 应保留
                Assert.IsNotNull(asc.GetAttributeSet<AS_Health>(), "Health 集应在");
                Assert.IsNotNull(asc.GetAttributeSet<AS_Stamina>(), "Stamina 集应在（不同类型不受去重影响）");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
