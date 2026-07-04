// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 受击反应流程：对每个收到的 AttackResult 依次跑处理器链。
// 这里挂在受击方上，订阅 CombatSystemComponent.OnAttackResultReceived，本地直接跑处理器。

using System.Collections.Generic;
using UnityEngine;

namespace Likeon.GAS
{
    [AddComponentMenu("Sigil/GAS/Combat Flow")]
    [RequireComponent(typeof(CombatSystemComponent))]
    public class CombatFlowComponent : MonoBehaviour
    {
        [Tooltip("受击结果处理器链（按子类型添加：死亡 / 触发事件 / 命中 cue）")]
        [SerializeReference] private List<AttackResultProcessor> processors = new List<AttackResultProcessor>();

        private CombatSystemComponent _combat;
        private AbilitySystemComponent _asc;

        /// <summary>处理器链（运行时/代码配置用）。</summary>
        public List<AttackResultProcessor> Processors => processors;

        private void Awake()
        {
            _combat = GetComponent<CombatSystemComponent>();
            _asc = GetComponent<AbilitySystemComponent>();
        }

        private void OnEnable()
        {
            if (_combat != null) _combat.OnAttackResultReceived += HandleAttackResult;
        }

        private void OnDisable()
        {
            if (_combat != null) _combat.OnAttackResultReceived -= HandleAttackResult;
        }

        /// <summary>对一个受到的攻击结果依次跑处理器。</summary>
        public void HandleAttackResult(AttackResult result)
        {
            if (result == null) return;
            var ctx = new AttackFlowContext(gameObject, _asc, result.EffectContext?.SourceASC);
            for (int i = 0; i < processors.Count; i++)
                processors[i]?.Process(result, in ctx);
        }
    }
}
