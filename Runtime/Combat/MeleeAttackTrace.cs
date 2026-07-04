// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 在攻击动画时间轴用 Animation Event 标记判定帧，在判定区间
//      切判定开关，CollisionTraceInstance 按武器 socket 扫描碰撞，命中施加 AttackDefinition 的效果。
// Unity：判定帧改用 Animation Event 调 BeginAttackTrace(index)/EndAttackTrace；
//        碰撞用 Physics 球扫（在 socket 间、且在上一帧到当前帧之间扫，避免快速挥砍穿透）。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Likeon.GAS
{
    [AddComponentMenu("Sigil/GAS/Melee Attack Trace")]
    public class MeleeAttackTrace : MonoBehaviour
    {
        /// <summary>一条可由动画事件按下标触发的判定配置（攻击定义 + 判定点）。</summary>
        [Serializable]
        public struct AttackTraceEntry
        {
            public AttackDefinition Attack;
            public CollisionTraceDefinition Trace;
        }

        [Tooltip("可用的判定配置；Animation Event 用下标 BeginAttackTrace(index) 选择")]
        [SerializeField] private List<AttackTraceEntry> entries = new List<AttackTraceEntry>();

        [Tooltip("攻击方 ASC（留空自动查找父级）")]
        [SerializeField] private AbilitySystemComponent sourceAbilitySystem;
        [Tooltip("攻击方战斗组件（留空自动查找父级）")]
        [SerializeField] private CombatSystemComponent sourceCombat;

        [Tooltip("SetByCaller 里代表伤害的标签，用于驱动目标 Meta 伤害管线")]
        [SerializeField] private GameplayTag damageDataTag = GameplayTag.None;

        // 运行时
        private bool _active;
        private AttackTraceEntry _current;
        private readonly Dictionary<Transform, Vector3> _lastPositions = new Dictionary<Transform, Vector3>();
        private readonly HashSet<GameObject> _hitActors = new HashSet<GameObject>();
        private readonly RaycastHit[] _hitBuffer = new RaycastHit[16];

        public bool IsTracing => _active;

        /// <summary>判定配置列表（运行时/代码驱动配置用）。</summary>
        public List<AttackTraceEntry> Entries => entries;

        private void Awake()
        {
            if (sourceAbilitySystem == null) sourceAbilitySystem = GetComponentInParent<AbilitySystemComponent>();
            if (sourceCombat == null) sourceCombat = GetComponentInParent<CombatSystemComponent>();
        }

        /// <summary>显式设置攻击方来源（武器装备到角色后用，覆盖 Awake 的自动查找）。</summary>
        public void SetSource(AbilitySystemComponent asc, CombatSystemComponent combat = null)
        {
            sourceAbilitySystem = asc;
            sourceCombat = combat != null ? combat : (asc != null ? asc.GetComponent<CombatSystemComponent>() : null);
        }

        // ===================== 动画事件入口 =====================
        /// <summary>开启判定窗口（攻击动画的 Animation Event 调用）。</summary>
        public void BeginAttackTrace(int index)
        {
            if (index < 0 || index >= entries.Count) { Debug.LogWarning($"[Sigil] BeginAttackTrace 下标越界: {index}"); return; }
            _current = entries[index];
            _active = true;
            _hitActors.Clear();
            CacheSocketPositions();
        }

        /// <summary>无参版本：默认用第 0 个配置（方便单段攻击）。</summary>
        public void BeginAttackTrace() => BeginAttackTrace(0);

        /// <summary>关闭判定窗口。</summary>
        public void EndAttackTrace() => _active = false;

        // ===================== 判定扫描 =====================
        // 在动画 pose 应用后采样 socket 世界坐标，做球扫。
        private void LateUpdate()
        {
            if (!_active) return;
            var sockets = _current.Trace.SocketTransforms;
            if (sockets == null || sockets.Count == 0) return;

            float radius = Mathf.Max(0.01f, _current.Trace.TraceRadius);
            int layers = _current.Trace.HitLayers.value != 0 ? _current.Trace.HitLayers.value : ~0;

            foreach (var socket in sockets)
            {
                if (socket == null) continue;
                Vector3 cur = socket.position;
                Vector3 last = _lastPositions.TryGetValue(socket, out var p) ? p : cur;

                Vector3 delta = cur - last;
                float dist = delta.magnitude;

                if (dist > 0.0001f)
                {
                    // 从上一帧位置扫到当前位置，避免快速挥砍穿透
                    int count = Physics.SphereCastNonAlloc(last, radius, delta.normalized, _hitBuffer, dist, layers, QueryTriggerInteraction.Ignore);
                    for (int i = 0; i < count; i++) TryHit(_hitBuffer[i].collider, _hitBuffer[i].point);
                }
                else
                {
                    var overlaps = Physics.OverlapSphere(cur, radius, layers, QueryTriggerInteraction.Ignore);
                    foreach (var col in overlaps) TryHit(col, cur);
                }

                _lastPositions[socket] = cur;
            }
        }

        private void CacheSocketPositions()
        {
            _lastPositions.Clear();
            var sockets = _current.Trace.SocketTransforms;
            if (sockets == null) return;
            foreach (var s in sockets)
                if (s != null) _lastPositions[s] = s.position;
        }

        // 处理一次潜在命中：过滤自身/阵营/去重 → 结算
        private void TryHit(Collider col, Vector3 hitPoint)
        {
            if (col == null) return;
            var targetASC = col.GetComponentInParent<AbilitySystemComponent>();
            if (targetASC == null) return;

            GameObject targetGo = targetASC.gameObject;
            if (sourceAbilitySystem != null && targetGo == sourceAbilitySystem.gameObject) return; // 不打自己
            if (_hitActors.Contains(targetGo)) return; // 一次判定窗口内每个目标只命中一次

            // 阵营过滤：只打敌对
            var sourceGo = sourceAbilitySystem != null ? sourceAbilitySystem.gameObject : gameObject;
            if (!CombatTeamAgent.IsHostile(sourceGo, targetGo)) return;

            _hitActors.Add(targetGo);
            ProcessHit(targetASC, hitPoint);
        }

        // 命中结算：施加效果 + 生成 AttackResult + 受击反应
        private void ProcessHit(AbilitySystemComponent targetASC, Vector3 hitPoint)
        {
            var attack = _current.Attack;
            if (attack == null) return;

            var context = new GameplayEffectContext(sourceAbilitySystem)
            {
                HitLocation = hitPoint,
                HasHitLocation = true,
                EffectCauser = gameObject
            };

            float damageValue = 0f;

            // 主效果（伤害 GE）：带 SetByCaller
            if (attack.TargetEffect != null && sourceAbilitySystem != null)
            {
                var spec = new GameplayEffectSpec(attack.TargetEffect, context,
                    attack.TargetEffectLevel >= 1 ? attack.TargetEffectLevel : 1);
                spec.AddDynamicAssetTags(attack.AttackTags); // 攻击类型作动态资产标签注入 spec
                foreach (var sbc in attack.SetByCallerMagnitudes)
                {
                    spec.SetSetByCallerMagnitude(sbc.Tag, sbc.Value);
                    if (sbc.Tag.MatchesTagExact(damageDataTag)) damageValue = sbc.Value;
                }
                targetASC.ApplyGameplayEffectSpecToSelf(spec);
            }

            // 效果容器：批量额外效果
            ApplyContainerToTarget(attack.TargetEffectContainer, targetASC, context, attack);

            // 生成攻击结果
            var result = new AttackResult
            {
                ImpactResult = GameplayTag.RequestTag("Combat.Impact.Hit"),
                EffectContext = context,
                HitLocation = hitPoint
            };
            if (damageDataTag.IsValid) result.TaggedValues.Add(new TaggedValue(damageDataTag, damageValue));
            sourceAbilitySystem?.GetOwnedGameplayTags(result.AggregatedSourceTags);
            // 攻击类型标签并入来源聚合标签：让受击处理器能按"被什么类型攻击"做反应（如重击→硬直）
            foreach (var at in attack.AttackTags) if (at.IsValid) result.AggregatedSourceTags.AddTag(at);
            targetASC.GetOwnedGameplayTags(result.AggregatedTargetTags);

            // 登记到目标战斗组件 + 受击反应/顿帧
            var targetCombat = targetASC.GetComponent<CombatSystemComponent>();
            if (targetCombat != null)
            {
                targetCombat.RegisterAttackResult(result);
                if (attack.HitStallingDuration > 0f)
                    targetCombat.ApplyHitStop(attack.HitStallingDuration, attack.HitPlayRateFactor);
            }

            // 命中表现 cue
            foreach (var cue in attack.TargetGameplayCues)
                targetASC.SendGameplayEvent(cue, new GameplayEventData(cue) { Target = targetASC.gameObject, Context = context });

            // 攻击方自身顿帧 + 通知
            if (sourceCombat != null)
            {
                if (attack.HitStallingDuration > 0f) sourceCombat.ApplyHitStop(attack.HitStallingDuration, attack.HitPlayRateFactor);
                sourceCombat.NotifyDealtDamage(result);
            }
        }

        private static void ApplyContainerToTarget(GameplayEffectContainer container, AbilitySystemComponent targetASC,
            GameplayEffectContext context, AttackDefinition attack)
        {
            if (container.TargetGameplayEffects == null) return;
            foreach (var ge in container.TargetGameplayEffects)
            {
                if (ge == null) continue;
                var spec = new GameplayEffectSpec(ge, context, attack.TargetEffectLevel >= 1 ? attack.TargetEffectLevel : 1);
                spec.AddDynamicAssetTags(attack.AttackTags);
                foreach (var sbc in attack.SetByCallerMagnitudes)
                    spec.SetSetByCallerMagnitude(sbc.Tag, sbc.Value);
                targetASC.ApplyGameplayEffectSpecToSelf(spec);
            }
        }
    }
}
