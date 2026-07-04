// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 通用碰撞检测：一组 socket 点持续 OverlapSphere 检测命中（每次激活内对每个 actor 去重），开关状态 + 命中/状态回调。
// 精简实现：单组件 + 去重，无对象池/外部 targeting 依赖。
//
// 与 MeleeAttackTrace 的分工：MeleeAttackTrace 绑 AttackDefinition、做快速挥砍防穿透球扫、直接施伤；
// CollisionTrace 是更底层的【通用命中产出】（陷阱、AOE 区域、环境伤害区、持续碰撞体），只产出命中事件，
// 施伤/响应由订阅 OnHit 的一方决定。需要近战挥砍判定请用 MeleeAttackTrace。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Likeon.GAS
{
    [AddComponentMenu("Sigil/GAS/Collision Trace")]
    public class CollisionTrace : MonoBehaviour
    {
        [Tooltip("检测点（每个点做一次 OverlapSphere）")]
        [SerializeField] private Transform[] traceSockets;
        [SerializeField] private float radius = 0.3f;
        [SerializeField] private LayerMask layerMask = ~0;
        [Tooltip("本 trace 的标识标签（供命中处理区分来源）")]
        public GameplayTag TraceGameplayTag;

        /// <summary>命中一个新目标的碰撞体时触发。</summary>
        public event Action<Collider> OnHit;
        /// <summary>trace 激活状态切换时触发。</summary>
        public event Action<bool> OnTraceStateChanged;
        /// <summary>可选命中过滤（如阵营/标签）；返回 false 则忽略该目标。</summary>
        public Func<GameObject, bool> HitFilter;

        private bool _active;
        private float _activeTime;
        private readonly HashSet<GameObject> _hitActors = new HashSet<GameObject>();

        public bool IsActive => _active;
        public float ActiveTime => _activeTime;
        public int HitCount => _hitActors.Count;

        public void SetSockets(params Transform[] sockets) => traceSockets = sockets;
        public float Radius { get => radius; set => radius = value; }
        public LayerMask LayerMask { get => layerMask; set => layerMask = value; }

        /// <summary>开关 trace。激活时重置计时与命中去重集；状态变化广播 OnTraceStateChanged。</summary>
        public void ToggleTraceState(bool newState)
        {
            if (_active == newState) return;
            _active = newState;
            if (_active) { _activeTime = 0f; _hitActors.Clear(); }
            OnTraceStateChanged?.Invoke(newState);
        }

        /// <summary>是否可命中该目标（默认走 HitFilter；可子类重写做阵营等过滤）。</summary>
        public virtual bool CanHitActor(GameObject actor) => HitFilter == null || HitFilter(actor);

        private void Update()
        {
            if (!_active) return;
            _activeTime += Time.deltaTime;
            DoTrace();
        }

        /// <summary>立即检测一次（绕过 Update timing，供测试/外部驱动）；仅在激活时有效。</summary>
        public void ForceTrace()
        {
            if (_active) DoTrace();
        }

        private void DoTrace()
        {
            if (traceSockets == null) return;
            for (int i = 0; i < traceSockets.Length; i++)
            {
                var s = traceSockets[i];
                if (s == null) continue;

                var cols = Physics.OverlapSphere(s.position, radius, layerMask, QueryTriggerInteraction.Ignore);
                for (int j = 0; j < cols.Length; j++)
                {
                    var col = cols[j];
                    var go = col.attachedRigidbody != null ? col.attachedRigidbody.gameObject : col.gameObject;
                    if (go == gameObject || _hitActors.Contains(go)) continue; // 忽略自身 + 本次激活已命中
                    if (!CanHitActor(go)) continue;

                    _hitActors.Add(go);
                    OnHit?.Invoke(col);
                }
            }
        }
    }
}
