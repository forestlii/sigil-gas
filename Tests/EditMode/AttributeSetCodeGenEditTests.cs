// EditMode 测试：AttributeSetCodeGenerator.Validate —— E2 回归。
// 修复前：Validate 不查 C# 关键字、不查"属性名==类名"、不查"{Name}Attribute 撞名"，
// 显示"通过"但生成物编译失败（CS0102/CS0542/关键字非法）。
using NUnit.Framework;
using UnityEngine;
using Likeon.GAS;
using Likeon.GAS.Editor;

namespace Likeon.GAS.Tests
{
    public class AttributeSetCodeGenEditTests
    {
        private static AttributeSetDefinition.AttributeDef Attr(string name) =>
            new AttributeSetDefinition.AttributeDef { Name = name };

        [Test]
        public void Validate_RejectsCSharpKeywordName()
        {
            var def = ScriptableObject.CreateInstance<AttributeSetDefinition>();
            def.ClassName = "AS_Test"; def.Namespace = "Game.Attributes";
            def.Attributes.Clear();
            def.Attributes.Add(Attr("event")); // C# 关键字
            var errors = AttributeSetCodeGenerator.Validate(def);
            Assert.IsTrue(errors.Exists(e => e.Contains("event")), "C# 关键字属性名应被拒绝: " + string.Join(";", errors));
            Object.DestroyImmediate(def);
        }

        [Test]
        public void Validate_RejectsNameEqualsClassName()
        {
            var def = ScriptableObject.CreateInstance<AttributeSetDefinition>();
            def.ClassName = "AS_Test"; def.Namespace = "Game.Attributes";
            def.Attributes.Clear();
            def.Attributes.Add(Attr("AS_Test")); // 与类名同名 → CS0542
            var errors = AttributeSetCodeGenerator.Validate(def);
            Assert.IsTrue(errors.Exists(e => e.Contains("与类名相同")), "属性名与类名相同应被拒绝: " + string.Join(";", errors));
            Object.DestroyImmediate(def);
        }

        [Test]
        public void Validate_RejectsHandleNameCollision()
        {
            var def = ScriptableObject.CreateInstance<AttributeSetDefinition>();
            def.ClassName = "AS_Test"; def.Namespace = "Game.Attributes";
            def.Attributes.Clear();
            def.Attributes.Add(Attr("Health"));            // 生成句柄 HealthAttribute
            def.Attributes.Add(Attr("HealthAttribute"));   // 与上面的句柄撞名
            var errors = AttributeSetCodeGenerator.Validate(def);
            Assert.IsTrue(errors.Exists(e => e.Contains("句柄名")), "{Name}Attribute 撞名应被拒绝: " + string.Join(";", errors));
            Object.DestroyImmediate(def);
        }

        [Test]
        public void Validate_PassesValidDefinition()
        {
            var def = ScriptableObject.CreateInstance<AttributeSetDefinition>();
            def.ClassName = "AS_Test"; def.Namespace = "Game.Attributes";
            def.Attributes.Clear();
            def.Attributes.Add(Attr("Health"));
            def.Attributes.Add(Attr("MaxHealth"));
            var errors = AttributeSetCodeGenerator.Validate(def);
            Assert.AreEqual(0, errors.Count, "合法定义应无错误: " + string.Join(";", errors));
            Object.DestroyImmediate(def);
        }
    }
}
