// EditMode 测试：GameplayTagQuery 按内容判空。
// 覆盖"面板/反序列化配置的 tags 与 expressions 真正参与求值"这一修复
// （此前序列化标志位默认恒为空，会让 Inspector 配的查询被永久放行）。
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Likeon.GAS;

namespace Likeon.GAS.Tests
{
    public class GameplayTagQueryEditTests
    {
        // 模拟 Unity 反序列化 / Inspector 编辑：默认构造后直接写私有序列化字段。
        static GameplayTagQuery Authored(GameplayTagQueryExprType type,
            List<GameplayTag> tags, List<GameplayTagQuery> exprs)
        {
            var q = new GameplayTagQuery();
            var t = typeof(GameplayTagQuery);
            t.GetField("exprType", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(q, type);
            if (tags != null)
                t.GetField("tags", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(q, tags);
            if (exprs != null)
                t.GetField("expressions", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(q, exprs);
            return q;
        }

        static GameplayTagContainer Container(params string[] tags)
        {
            var c = new GameplayTagContainer();
            foreach (var s in tags) c.AddTag(GameplayTag.RequestTag(s));
            return c;
        }

        [Test]
        public void DefaultConstructed_NoConditions_IsEmpty_MatchesAll()
        {
            var q = new GameplayTagQuery();
            Assert.IsTrue(q.IsEmpty, "无条件的查询应判为空");
            Assert.IsTrue(q.Matches(Container()), "空查询应匹配一切");
        }

        [Test]
        public void AuthoredTags_NotEmpty_AndEvaluated()
        {
            // 修复前：默认构造的标志位恒为空 → 这个查询永远放行（bug）。
            var q = Authored(GameplayTagQueryExprType.AllTagsMatch,
                new List<GameplayTag> { GameplayTag.RequestTag("State.Vulnerable") }, null);
            Assert.IsFalse(q.IsEmpty, "配了 tags 就不该判为空");
            Assert.IsFalse(q.Matches(Container()), "缺少要求的标签应不匹配");
            Assert.IsTrue(q.Matches(Container("State.Vulnerable")), "含要求的标签应匹配");
        }

        [Test]
        public void AuthoredExpressions_Nested_Evaluated()
        {
            // exprType=AnyExprMatch + 两个子查询：命中任一即真。修复前同样被短路。
            var exprs = new List<GameplayTagQuery>
            {
                GameplayTagQuery.MakeQuery_MatchAllTags(GameplayTag.RequestTag("State.Vulnerable")),
                GameplayTagQuery.MakeQuery_MatchAllTags(GameplayTag.RequestTag("State.Stunned")),
            };
            var q = Authored(GameplayTagQueryExprType.AnyExprMatch, null, exprs);
            Assert.IsFalse(q.IsEmpty, "配了 expressions 就不该判为空");
            Assert.IsTrue(q.Matches(Container("State.Stunned")), "命中任一子查询应为真");
            Assert.IsFalse(q.Matches(Container("State.Other")), "都不命中应为假");
        }

        [Test]
        public void EmptyExpressionList_TreatedAsEmpty()
        {
            var q = Authored(GameplayTagQueryExprType.AllExprMatch, null, new List<GameplayTagQuery>());
            Assert.IsTrue(q.IsEmpty, "表达式类但列表为空应判为空");
            Assert.IsTrue(q.Matches(Container()), "空查询匹配一切");
        }

        [Test]
        public void FactoryConstructed_StillWorks_NoRegression()
        {
            var q = GameplayTagQuery.MakeQuery_MatchAllTags(GameplayTag.RequestTag("State.Vulnerable"));
            Assert.IsFalse(q.IsEmpty);
            Assert.IsTrue(q.Matches(Container("State.Vulnerable")));
            Assert.IsFalse(q.Matches(Container()));

            var empty = GameplayTagQuery.MakeQuery_MatchAllTags();
            Assert.IsTrue(empty.IsEmpty, "工厂构造但无标签应判为空");
        }
    }
}
