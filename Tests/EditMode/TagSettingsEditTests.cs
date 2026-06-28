// EditMode 测试：标签注册表（GameplayTagsSettings）的去重/排序/删除逻辑。
using NUnit.Framework;
using UnityEngine;
using Likeon.GAS;

namespace Likeon.GAS.Tests
{
    public class TagSettingsEditTests
    {
        [Test]
        public void AddTag_DedupesAndSorts()
        {
            var s = ScriptableObject.CreateInstance<GameplayTagsSettings>();

            Assert.IsTrue(s.AddTag("State.Combat.Block"));
            Assert.IsTrue(s.AddTag("Ability.Slide"));
            Assert.IsTrue(s.AddTag("State.Combat.Attack"));
            Assert.IsFalse(s.AddTag("Ability.Slide"), "重复标签不应再次加入");
            Assert.IsFalse(s.AddTag("  "), "空白标签应被拒绝");

            Assert.AreEqual(3, s.Count);
            Assert.IsTrue(s.Contains("Ability.Slide"));

            // 已按序数排序
            Assert.AreEqual("Ability.Slide", s.Tags[0]);
            Assert.AreEqual("State.Combat.Attack", s.Tags[1]);
            Assert.AreEqual("State.Combat.Block", s.Tags[2]);

            Assert.IsTrue(s.RemoveTag("Ability.Slide"));
            Assert.IsFalse(s.Contains("Ability.Slide"));
            Assert.AreEqual(2, s.Count);

            Object.DestroyImmediate(s);
        }
    }
}
