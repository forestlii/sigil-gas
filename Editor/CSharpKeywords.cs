// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 代码生成共用：C# 保留关键字表。生成标识符（属性名/类名/标签常量段）时用于避免生成不可编译的成员。

using System.Collections.Generic;

namespace Likeon.GAS.Editor
{
    /// <summary>C# 保留关键字集合，供代码生成器校验/转义标识符。</summary>
    internal static class CSharpKeywords
    {
        private static readonly HashSet<string> Set = new HashSet<string>
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
            "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
            "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
            "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
            "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
            "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
            "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true",
            "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual",
            "void", "volatile", "while"
        };

        /// <summary>是否为 C# 保留关键字（不能直接作标识符，需 @ 前缀或改名）。</summary>
        public static bool IsKeyword(string s) => s != null && Set.Contains(s);
    }
}
