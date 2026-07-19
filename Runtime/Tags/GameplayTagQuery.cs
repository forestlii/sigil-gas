// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 对一个标签容器做条件查询。
// 输入门控、技能交互规则、连段动作选择等场景
// 都用它"按角色当前状态标签选分支"，是状态驱动多态的判断核心。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>查询表达式的类型。</summary>
    public enum GameplayTagQueryExprType
    {
        /// <summary>目标容器需匹配 Tags 里的全部标签（层级）。</summary>
        AllTagsMatch,
        /// <summary>目标容器需匹配 Tags 里的任意标签。</summary>
        AnyTagsMatch,
        /// <summary>目标容器不能匹配 Tags 里的任何标签。</summary>
        NoTagsMatch,
        /// <summary>子表达式需全部为真。</summary>
        AllExprMatch,
        /// <summary>子表达式需任意为真。</summary>
        AnyExprMatch,
        /// <summary>子表达式需全部为假。</summary>
        NoExprMatch
    }

    /// <summary>
    /// 可序列化的标签查询。支持标签级条件与表达式嵌套组合。
    /// 空查询（无任何条件）默认匹配为 true。
    /// </summary>
    [Serializable]
    public class GameplayTagQuery
    {
        [SerializeField] private GameplayTagQueryExprType exprType = GameplayTagQueryExprType.AllTagsMatch;
        [SerializeField] private List<GameplayTag> tags = new List<GameplayTag>();
        [SerializeReference] private List<GameplayTagQuery> expressions = new List<GameplayTagQuery>();

        /// <summary>
        /// 是否为空查询（无有效条件）。空查询默认匹配为 true。
        /// 按内容运行时推导：标签类看 tags、表达式类看 expressions。
        /// 不再用序列化标志位——否则 Inspector 里默认构造的查询会因标志位恒为空而被永久放行，
        /// 导致面板配置的 tags / expressions 全部失效（见 decisions.md 2026-07-02）。
        /// </summary>
        public bool IsEmpty
        {
            get
            {
                switch (exprType)
                {
                    case GameplayTagQueryExprType.AllTagsMatch:
                    case GameplayTagQueryExprType.AnyTagsMatch:
                    case GameplayTagQueryExprType.NoTagsMatch:
                        return tags == null || tags.Count == 0;
                    case GameplayTagQueryExprType.AllExprMatch:
                    case GameplayTagQueryExprType.AnyExprMatch:
                    case GameplayTagQueryExprType.NoExprMatch:
                        return !HasAnyNonNullExpression();
                    default:
                        return true;
                }
            }
        }

        // 表达式列表里是否存在至少一个非 null 子表达式。
        // Inspector 默认 UI 下点 "+" 但尚未选具体类型时元素为 null，需据此判空避免误放行/求值 NRE。
        private bool HasAnyNonNullExpression()
        {
            if (expressions == null) return false;
            foreach (var e in expressions) if (e != null) return true;
            return false;
        }

        public GameplayTagQuery() { }

        private GameplayTagQuery(GameplayTagQueryExprType type, List<GameplayTag> t, List<GameplayTagQuery> exprs)
        {
            exprType = type;
            tags = t ?? new List<GameplayTag>();
            expressions = exprs ?? new List<GameplayTagQuery>();
        }

        /// <summary>对给定标签容器求值。空查询返回 true。::Matches。</summary>
        public bool Matches(GameplayTagContainer container)
        {
            if (IsEmpty) return true;
            if (container == null) container = new GameplayTagContainer();

            switch (exprType)
            {
                case GameplayTagQueryExprType.AllTagsMatch:
                    foreach (var t in tags) if (!container.HasTag(t)) return false;
                    return true;
                case GameplayTagQueryExprType.AnyTagsMatch:
                    foreach (var t in tags) if (container.HasTag(t)) return true;
                    return false;
                case GameplayTagQueryExprType.NoTagsMatch:
                    foreach (var t in tags) if (container.HasTag(t)) return false;
                    return true;
                case GameplayTagQueryExprType.AllExprMatch:
                    foreach (var e in expressions) if (e != null && !e.Matches(container)) return false;
                    return true;
                case GameplayTagQueryExprType.AnyExprMatch:
                    foreach (var e in expressions) if (e != null && e.Matches(container)) return true;
                    return false;
                case GameplayTagQueryExprType.NoExprMatch:
                    foreach (var e in expressions) if (e != null && e.Matches(container)) return false;
                    return true;
                default:
                    return false;
            }
        }

        // ---- 便捷构造（::Build* / Make*）----

        public static GameplayTagQuery MakeQuery_MatchAllTags(params GameplayTag[] t)
            => new GameplayTagQuery(GameplayTagQueryExprType.AllTagsMatch, new List<GameplayTag>(t), null);

        public static GameplayTagQuery MakeQuery_MatchAnyTags(params GameplayTag[] t)
            => new GameplayTagQuery(GameplayTagQueryExprType.AnyTagsMatch, new List<GameplayTag>(t), null);

        public static GameplayTagQuery MakeQuery_MatchNoTags(params GameplayTag[] t)
            => new GameplayTagQuery(GameplayTagQueryExprType.NoTagsMatch, new List<GameplayTag>(t), null);

        public static GameplayTagQuery All(params GameplayTagQuery[] exprs)
            => new GameplayTagQuery(GameplayTagQueryExprType.AllExprMatch, null, new List<GameplayTagQuery>(exprs));

        public static GameplayTagQuery Any(params GameplayTagQuery[] exprs)
            => new GameplayTagQuery(GameplayTagQueryExprType.AnyExprMatch, null, new List<GameplayTagQuery>(exprs));
    }
}
