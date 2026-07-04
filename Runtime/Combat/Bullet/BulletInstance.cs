// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 单个子弹实例（自走 + 命中检测 + 穿透 + 施伤 + 子弹链）。
// 这里用普通 MonoBehaviour 自己积分运动，球扫命中；去掉网络。
// 关键：提供 Tick(dt) 解耦 Unity 时间——便于测试用固定步长驱动、也便于宿主自定义时间/暂停。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Likeon.GAS
{
    [AddComponentMenu("Sigil/GAS/Bullet Instance")]
    public class BulletInstance : MonoBehaviour
    {
        public BulletDefinition Definition { get; private set; }
        public GameObject Owner { get; private set; }
        public AbilitySystemComponent SourceASC { get; private set; }
        public bool IsActive { get; private set; }

        /// <summary>是否在 Update 里自动按 Time.deltaTime 推进（测试可关掉，改用手动 Tick）。</summary>
        public bool AutoTick { get; set; } = true;

        /// <summary>命中时触发（targetASC 为命中的角色 ASC；命中地图几何时为 null）。</summary>
        public event Action<BulletInstance, AbilitySystemComponent, Vector3> OnHit;
        /// <summary>子弹失效（命中停下 / 生命到期）时触发。</summary>
        public event Action<BulletInstance> OnExpired;

        private Vector3 _velocity;
        private float _life;
        private readonly HashSet<GameObject> _hitActors = new HashSet<GameObject>();
        private readonly RaycastHit[] _hitBuffer = new RaycastHit[16];

        /// <summary>发射子弹。</summary>
        public void Launch(BulletDefinition def, GameObject owner, AbilitySystemComponent sourceASC, Vector3 position, Vector3 direction)
        {
            Definition = def;
            Owner = owner;
            SourceASC = sourceASC;
            transform.position = position;
            if (direction.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(direction.normalized);
            _velocity = transform.forward * (def != null ? def.InitialSpeed : 0f);
            _life = 0f;
            _hitActors.Clear();
            IsActive = true;
        }

        private void Update()
        {
            if (IsActive && AutoTick) Tick(Time.deltaTime);
        }

        /// <summary>推进一步：重力 → 移动（沿途球扫命中）→ 生命到期。可被测试/宿主手动驱动。</summary>
        public void Tick(float dt)
        {
            if (!IsActive || Definition == null || dt <= 0f) return;

            _velocity += Physics.gravity * Definition.GravityScale * dt;

            Vector3 start = transform.position;
            Vector3 step = _velocity * dt;
            float dist = step.magnitude;

            if (dist > 1e-5f)
            {
                Vector3 dir = step / dist;
                float radius = Mathf.Max(0.005f, Definition.HitRadius);
                int count = Physics.SphereCastNonAlloc(start, radius, dir, _hitBuffer, dist, Definition.HitLayers, QueryTriggerInteraction.Ignore);
                if (count > 0)
                {
                    Array.Sort(_hitBuffer, 0, count, HitDistanceComparer.Instance);
                    for (int i = 0; i < count; i++)
                    {
                        if (ProcessHit(_hitBuffer[i])) return; // 被吸收（停下并失效）
                    }
                }
                transform.position = start + step;
            }

            _life += dt;
            if (Definition.Duration > 0f && _life >= Definition.Duration)
                Expire(transform.position);
        }

        // 返回 true = 子弹被此命中吸收（停下）
        private bool ProcessHit(RaycastHit hit)
        {
            var col = hit.collider;
            if (col == null) return false;
            var go = col.attachedRigidbody != null ? col.attachedRigidbody.gameObject : col.gameObject;
            if (go == Owner) return false; // 不打发射者自身

            var targetASC = go.GetComponentInParent<AbilitySystemComponent>();
            if (targetASC != null)
            {
                // 角色：去重 + 阵营（友军穿过）
                if (_hitActors.Contains(targetASC.gameObject)) return false;
                if (Owner != null && !CombatTeamAgent.IsHostile(Owner, targetASC.gameObject)) return false;

                _hitActors.Add(targetASC.gameObject);
                if (Definition.Attack != null)
                    AttackApplication.ApplyAttack(Definition.Attack, SourceASC, gameObject, targetASC, hit.point);
                OnHit?.Invoke(this, targetASC, hit.point);

                if (Definition.PenetrateCharacter) return false; // 穿透继续
                Expire(hit.point);
                return true;
            }
            else
            {
                // 地图几何
                OnHit?.Invoke(this, null, hit.point);
                if (Definition.PenetrateMap) return false;
                Expire(hit.point);
                return true;
            }
        }

        /// <summary>失效：生成子弹链（若配置）、触发事件、销毁。</summary>
        private void Expire(Vector3 position)
        {
            if (!IsActive) return;
            IsActive = false;
            transform.position = position;

            if (Definition.HitBulletDefinition != null && Definition.HitBulletDefinition != Definition)
                BulletLauncher.Fire(Definition.HitBulletDefinition, Owner, SourceASC, position, transform.rotation);

            OnExpired?.Invoke(this);
            Destroy(gameObject);
        }

        private sealed class HitDistanceComparer : IComparer<RaycastHit>
        {
            public static readonly HitDistanceComparer Instance = new HitDistanceComparer();
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }
    }
}
