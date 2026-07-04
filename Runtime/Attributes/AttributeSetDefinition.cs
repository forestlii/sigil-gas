// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 属性集「定义资产」：在编辑器里声明一个属性集要有哪些属性/默认值/钳制，
// 由 AttributeSetCodeGenerator 单向生成对应的 C# 属性集类（<类名>.g.cs）。
// —— 目标：像 UE 那样"属性必须写 C++"的痛点，在这里变成"编辑器点几下、工具替你写 C#"。
// 本资产是唯一真源；生成的 .g.cs 是只读产物，自定义逻辑写在手写 partial（<类名>.cs）里。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>属性集的编辑器定义（codegen 输入）。</summary>
    [CreateAssetMenu(fileName = "AttributeSetDefinition", menuName = "Sigil/GAS/Attribute Set Definition")]
    public class AttributeSetDefinition : ScriptableObject
    {
        [Tooltip("生成的属性集类名（如 AS_PlayerStats）")]
        public string ClassName = "AS_New";

        [Tooltip("生成类所在命名空间")]
        public string Namespace = "Game.Attributes";

        [Tooltip("要生成的属性")]
        public List<AttributeDef> Attributes = new List<AttributeDef>();

        /// <summary>单个属性的声明。</summary>
        [Serializable]
        public class AttributeDef
        {
            [Tooltip("属性名（生成为字段名，须是合法 C# 标识符，如 Health）")]
            public string Name = "NewAttribute";

            [Tooltip("默认基础值")]
            public float DefaultValue = 0f;

            [Header("钳制（生成到 PreAttributeChange）")]
            [Tooltip("勾选后把 CurrentValue 夹到下限 MinValue")]
            public bool ClampMin = false;
            public float MinValue = 0f;

            [Tooltip("非空时把 CurrentValue 上限夹到该属性的当前值（如 Health 填 MaxHealth）")]
            public string MaxAttribute = "";

            [Tooltip("Meta/瞬时属性：仅作注释标注。实际清零/映射（如 IncomingDamage→-Health）请写在手写 partial 的 PostGameplayEffectExecute")]
            public bool IsMeta = false;
        }
    }
}
