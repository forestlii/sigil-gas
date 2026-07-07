// EditMode 测试：GameplayTagScanner.ExtractTags 剥注释后再扫 RequestTag（注释掉的标签不该被扫进来）。
using NUnit.Framework;
using Likeon.GAS.Editor;

namespace Likeon.GAS.Tests
{
    public class GameplayTagScannerEditTests
    {
        [Test]
        public void RealRequestTag_IsFound()
        {
            var tags = GameplayTagScanner.ExtractTags("var t = GameplayTag.RequestTag(\"Ability.Melee\");");
            Assert.IsTrue(tags.Contains("Ability.Melee"));
            Assert.AreEqual(1, tags.Count);
        }

        [Test]
        public void LineCommentedRequestTag_IsIgnored()
        {
            var src = "// var t = RequestTag(\"Dead.LineComment\");\nRequestTag(\"Live.Tag\");";
            var tags = GameplayTagScanner.ExtractTags(src);
            Assert.IsFalse(tags.Contains("Dead.LineComment"), "行注释里的 RequestTag 不该被扫进来");
            Assert.IsTrue(tags.Contains("Live.Tag"));
            Assert.AreEqual(1, tags.Count);
        }

        [Test]
        public void BlockCommentedRequestTag_IsIgnored()
        {
            var src = "/* RequestTag(\"Dead.Block\");\n   RequestTag(\"Dead.Block2\"); */\nRequestTag(\"Live.Tag\");";
            var tags = GameplayTagScanner.ExtractTags(src);
            Assert.IsFalse(tags.Contains("Dead.Block"));
            Assert.IsFalse(tags.Contains("Dead.Block2"));
            Assert.IsTrue(tags.Contains("Live.Tag"));
            Assert.AreEqual(1, tags.Count);
        }

        [Test]
        public void SlashesInsideStringLiteral_DoNotBreakFollowingTag()
        {
            // 字符串里的 // 不该被当成行注释，从而误吞掉后面的真 RequestTag
            var src = "var url = \"http://example.com\"; RequestTag(\"Real.AfterUrl\");";
            var tags = GameplayTagScanner.ExtractTags(src);
            Assert.IsTrue(tags.Contains("Real.AfterUrl"));
        }

        [Test]
        public void MultipleRealTags_AllFound()
        {
            var src = "RequestTag(\"A.One\"); RequestTag( \"A.Two\" );\nRequestTag(\"A.Three\");";
            var tags = GameplayTagScanner.ExtractTags(src);
            Assert.AreEqual(3, tags.Count);
            Assert.IsTrue(tags.Contains("A.One"));
            Assert.IsTrue(tags.Contains("A.Two"));
            Assert.IsTrue(tags.Contains("A.Three"));
        }
    }
}
