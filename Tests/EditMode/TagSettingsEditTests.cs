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

        // E1 回归：格式非法的标签应被拦在注册表外（否则击穿常量生成器 → 生成不可编译代码）。
        [Test]
        public void AddTag_RejectsMalformedNames()
        {
            var s = ScriptableObject.CreateInstance<GameplayTagsSettings>();

            Assert.IsFalse(s.AddTag("A..B"), "双点空段应拒绝");
            Assert.IsFalse(s.AddTag(".A"), "前导点应拒绝");
            Assert.IsFalse(s.AddTag("A."), "尾随点应拒绝");
            Assert.IsFalse(s.AddTag("A.\"B"), "含引号应拒绝");
            Assert.IsFalse(s.AddTag("A B"), "含空格应拒绝");
            Assert.AreEqual(0, s.Count, "非法标签都不应进注册表");

            Assert.IsTrue(s.AddTag("State.Combat.Block-Heavy_2"), "合法名（字母/数字/下划线/连字符）应通过");

            Assert.IsTrue(GameplayTagsSettings.IsValidTagName("A.B_c-1"));
            Assert.IsFalse(GameplayTagsSettings.IsValidTagName("A..B"));
            Assert.IsFalse(GameplayTagsSettings.IsValidTagName("A.\"B"));

            Object.DestroyImmediate(s);
        }
    }
}
