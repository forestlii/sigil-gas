// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 战斗锁定（Lock-On）系统。
// Unity 无该插件，这里自研等价物：OverlapSphere 收集候选 → 过滤链（阵营/死亡/标签/视角/视线）→
// 选择最佳（最近 / 视角最小）；锁定/解锁、左右切换目标、视角计算、锁定事件。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Likeon.GAS
{
    [AddComponentMenu("Sigil/GAS/Targeting System Component")]
    public class TargetingSystemComponent : MonoBehaviour
    {
        [Header("搜索")]
        [Tooltip("搜索半径")]
        [SerializeField] private float searchRadius = 15f;
        [Tooltip("候选所在层")]
        [SerializeField] private LayerMask candidateLayers = ~0;
        [Tooltip("视角来源（算角度/视线的起点，留空用自身 transform，常设为相机）")]
        [SerializeField] private Transform viewSource;

        [Header("过滤")]
        [Tooltip(">0 时只锁定视角锥内的目标（度，相对 viewSource.forward 水平面）")]
        [SerializeField] private float maxViewAngle = 60f;
        [Tooltip("只锁定敌对阵营（用 CombatTeamAgent）")]
        [SerializeField] private bool onlyHostile = true;
        [Tooltip("过滤掉已死亡目标（生命属性<=0）")]
        [SerializeField] private bool filterDead = true;
        [Tooltip("判定死亡读的生命属性名（跨属性集按名解析）")]
        [SerializeField] private string healthAttributeName = "Health";
        [Tooltip("需要视线无遮挡")]
        [SerializeField] private bool requireLineOfSight = false;
        [Tooltip("视线遮挡层")]
        [SerializeField] private LayerMask obstacleLayers = 0;
        [Tooltip("候选 ASC 必须拥有的全部标签")]
        [SerializeField] private GameplayTagContainer requiredTags = new GameplayTagContainer();
        [Tooltip("候选 ASC 不可拥有的任一标签")]
        [SerializeField] private GameplayTagContainer blockedTags = new GameplayTagContainer();

        [Header("自动维持")]
        [Tooltip("每帧检查当前目标是否仍有效，失效则自动解锁")]
        [SerializeField] private bool autoDropInvalidTarget = true;

        /// <summary>锁定新目标时触发（参数=新目标）。</summary>
        public event Action<GameObject> OnTargetLockOn;
        /// <summary>解锁目标时触发（参数=原目标）。</summary>
        public event Action<GameObject> OnTargetLockOff;

        public GameObject TargetedActor { get; private set; }
        public bool IsLockedOn => TargetedActor != null;
        public IReadOnlyList<GameObject> PotentialTargets => _potentialTargets;

        // ---- 运行时可调配置 ----
        public float SearchRadius { get => searchRadius; set => searchRadius = value; }
        public float MaxViewAngle { get => maxViewAngle; set => maxViewAngle = value; }
        public bool OnlyHostile { get => onlyHostile; set => onlyHostile = value; }
        public bool FilterDead { get => filterDead; set => filterDead = value; }
        public Transform ViewSource { get => viewSource; set => viewSource = value; }
        public GameplayTagContainer RequiredTags => requiredTags;
        public GameplayTagContainer BlockedTags => blockedTags;

        private readonly List<GameObject> _potentialTargets = new List<GameObject>();
        private readonly Collider[] _overlapBuffer = new Collider[64];
        private CombatTeamAgent _team;

        private Transform View => viewSource != null ? viewSource : transform;

        private void Awake() => _team = GetComponent<CombatTeamAgent>();

        private void Update()
        {
            if (autoDropInvalidTarget && TargetedActor != null && !CanBeTargeted(TargetedActor))
                SetTargetedActor(null);
        }

        // ===================== 收集 + 选择 =====================

        /// <summary>收集潜在目标并锁定最佳。</summary>
        public void SearchForActorToTarget()
        {
            RefreshPotentialTargets();
            SetTargetedActor(SelectBestActor());
        }

        /// <summary>切换锁定 / 解锁（已锁则解，未锁则搜索锁定）。锁定键常用。</summary>
        public void ToggleLock()
        {
            if (IsLockedOn) SetTargetedActor(null);
            else SearchForActorToTarget();
        }

        /// <summary>用 OverlapSphere 收集并过滤候选。</summary>
        public void RefreshPotentialTargets()
        {
            _potentialTargets.Clear();
            var origin = View.position;
            int count = Physics.OverlapSphereNonAlloc(origin, searchRadius, _overlapBuffer, candidateLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var go = _overlapBuffer[i].attachedRigidbody != null
                    ? _overlapBuffer[i].attachedRigidbody.gameObject : _overlapBuffer[i].gameObject;
                if (_potentialTargets.Contains(go)) continue;
                if (CanBeTargeted(go)) _potentialTargets.Add(go);
            }
        }

        /// <summary>从潜在目标里选最佳：优先视角最小，其次最近。</summary>
        public GameObject SelectBestActor()
        {
            GameObject best = null;
            float bestScore = float.MaxValue;
            var origin = View.position;
            foreach (var t in _potentialTargets)
            {
                if (t == null) continue;
                float angle = CalculateViewAngle(t);
                float dist = Vector3.Distance(origin, t.transform.position);
                // 角度为主、距离为辅的打分（角度权重更大）
                float score = angle * 1f + dist * 0.1f;
                if (score < bestScore) { bestScore = score; best = t; }
            }
            return best;
        }

        /// <summary>选最近的潜在目标。</summary>
        public GameObject SelectClosestActor()
        {
            GameObject best = null;
            float bestDist = float.MaxValue;
            var origin = View.position;
            foreach (var t in _potentialTargets)
            {
                if (t == null) continue;
                float d = Vector3.Distance(origin, t.transform.position);
                if (d < bestDist) { bestDist = d; best = t; }
            }
            return best;
        }

        /// <summary>向左/右切换到相邻目标（按相对当前目标的水平方位角）。</summary>
        public void StaticSwitchToNewTarget(bool rightDirection)
        {
            if (TargetedActor == null) { SearchForActorToTarget(); return; }
            RefreshPotentialTargets();

            var origin = View.position;
            var up = Vector3.up;
            Vector3 curDir = Flatten(TargetedActor.transform.position - origin);

            GameObject best = null;
            float bestAngle = float.MaxValue;
            foreach (var t in _potentialTargets)
            {
                if (t == null || t == TargetedActor) continue;
                Vector3 dir = Flatten(t.transform.position - origin);
                float signed = Vector3.SignedAngle(curDir, dir, up); // 右为正
                // 取目标侧、角度最小的那个
                bool onSide = rightDirection ? signed > 1f : signed < -1f;
                if (!onSide) continue;
                float mag = Mathf.Abs(signed);
                if (mag < bestAngle) { bestAngle = mag; best = t; }
            }
            if (best != null) SetTargetedActor(best);
        }

        // ===================== 设置 / 查询 =====================

        /// <summary>设置当前锁定目标，触发锁定/解锁事件。</summary>
        public void SetTargetedActor(GameObject newActor)
        {
            if (newActor == TargetedActor) return;
            var prev = TargetedActor;
            TargetedActor = newActor;
            if (prev != null) OnTargetLockOff?.Invoke(prev);
            if (newActor != null) OnTargetLockOn?.Invoke(newActor);
        }

        /// <summary>某 actor 是否可被锁定（跑完整过滤链）。</summary>
        public bool CanBeTargeted(GameObject actor)
        {
            if (actor == null || actor == gameObject) return false;

            var origin = View.position;
            var pos = actor.transform.position;
            if (Vector3.Distance(origin, pos) > searchRadius + 0.01f) return false;

            // 阵营
            if (onlyHostile && _team != null && _team.GetAttitudeTowards(actor) != ETeamAttitude.Hostile) return false;

            var asc = actor.GetComponent<AbilitySystemComponent>();

            // 死亡
            if (filterDead && IsDead(asc)) return false;

            // 标签要求
            if (asc != null)
            {
                if (requiredTags != null)
                    foreach (var t in requiredTags) if (!asc.HasMatchingGameplayTag(t)) return false;
                if (blockedTags != null)
                    foreach (var t in blockedTags) if (asc.HasMatchingGameplayTag(t)) return false;
            }

            // 视角锥
            if (maxViewAngle > 0f && CalculateViewAngle(actor) > maxViewAngle) return false;

            // 视线
            if (requireLineOfSight)
            {
                var dir = pos - origin;
                float dist = dir.magnitude;
                if (dist > 0.01f && Physics.Raycast(origin, dir / dist, dist, obstacleLayers, QueryTriggerInteraction.Ignore))
                    return false;
            }
            return true;
        }

        /// <summary>到目标的视角夹角（度，相对 viewSource.forward，水平面）。</summary>
        public float CalculateViewAngle(GameObject target)
        {
            if (target == null) return 180f;
            Vector3 fwd = Flatten(View.forward);
            Vector3 dir = Flatten(target.transform.position - View.position);
            if (dir.sqrMagnitude < 1e-6f) return 0f;
            return Vector3.Angle(fwd, dir);
        }

        private bool IsDead(AbilitySystemComponent asc)
        {
            if (asc == null) return false;
            var hp = asc.GetAttributeDataByName(healthAttributeName);
            return hp != null && hp.CurrentValue <= 0f;
        }

        private static Vector3 Flatten(Vector3 v) { v.y = 0f; return v; }
    }
}
