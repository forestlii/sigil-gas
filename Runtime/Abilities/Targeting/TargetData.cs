// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// GAS 层的"目标数据"模型：把一次目标采集（trace/选取）的结果打包，供施加效果、传给技能/事件。
// 单机实现：不含网络序列化/预测，保留数据结构与常用查询。

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>
    /// 命中结果（的常用字段）。HasHit=false 表示"只到达终点、未命中阻挡物"。
    /// </summary>
    public struct TargetHitResult
    {
        public bool HasHit;          // 是否命中了阻挡物（对应 bBlockingHit）
        public GameObject HitActor;  // 命中的物体
        public Collider Collider;
        public Vector3 Point;        // 命中点（对应 ImpactPoint）
        public Vector3 Normal;       // 命中法线（对应 ImpactNormal）
        public Vector3 TraceStart;
        public Vector3 TraceEnd;
        public float Distance;

        public bool IsValidBlockingHit => HasHit && HitActor != null;

        /// <summary>取命中物体上的 ASC（无则 null）。</summary>
        public AbilitySystemComponent GetTargetASC()
            => HitActor != null ? HitActor.GetComponent<AbilitySystemComponent>() : null;

        public static TargetHitResult FromRaycast(RaycastHit hit, Vector3 start, Vector3 end)
        {
            return new TargetHitResult
            {
                HasHit = hit.collider != null,
                HitActor = hit.collider != null ? hit.collider.attachedRigidbody != null
                            ? hit.collider.attachedRigidbody.gameObject : hit.collider.gameObject : null,
                Collider = hit.collider,
                Point = hit.point,
                Normal = hit.normal,
                TraceStart = start,
                TraceEnd = end,
                Distance = hit.distance
            };
        }

        /// <summary>无命中、仅到达终点的伪命中。</summary>
        public static TargetHitResult EndpointOnly(Vector3 start, Vector3 end)
            => new TargetHitResult { HasHit = false, TraceStart = start, TraceEnd = end, Point = end, Distance = Vector3.Distance(start, end) };
    }

    /// <summary>目标数据基类。</summary>
    public abstract class TargetData
    {
        public virtual bool HasHitResult => false;
        public virtual TargetHitResult GetHitResult() => default;
        public virtual IReadOnlyList<GameObject> GetActors() => System.Array.Empty<GameObject>();
    }

    /// <summary>单命中结果型目标数据。</summary>
    public class TargetData_SingleTargetHit : TargetData
    {
        public TargetHitResult HitResult;
        private readonly GameObject[] _actorCache = new GameObject[1];

        public TargetData_SingleTargetHit(TargetHitResult hit) { HitResult = hit; }

        public override bool HasHitResult => true;
        public override TargetHitResult GetHitResult() => HitResult;
        public override IReadOnlyList<GameObject> GetActors()
        {
            _actorCache[0] = HitResult.HitActor;
            return HitResult.HitActor != null ? _actorCache : System.Array.Empty<GameObject>();
        }
    }

    /// <summary>actor 数组型目标数据。</summary>
    public class TargetData_ActorArray : TargetData
    {
        public readonly List<GameObject> Actors = new List<GameObject>();
        public TargetData_ActorArray() { }
        public TargetData_ActorArray(IEnumerable<GameObject> actors) { if (actors != null) Actors.AddRange(actors); }
        public override IReadOnlyList<GameObject> GetActors() => Actors;
    }

    /// <summary>
    /// 目标数据句柄：一次采集可产出多条 TargetData。
    /// </summary>
    public class TargetDataHandle : IEnumerable<TargetData>
    {
        private readonly List<TargetData> _data = new List<TargetData>();

        public int Count => _data.Count;
        public TargetData this[int index] => _data[index];
        public bool IsEmpty => _data.Count == 0;

        public void Add(TargetData data) { if (data != null) _data.Add(data); }

        /// <summary>取第 index 条数据的命中结果（无则返回空命中）。</summary>
        public TargetHitResult GetHitResult(int index = 0)
            => index >= 0 && index < _data.Count ? _data[index].GetHitResult() : default;

        /// <summary>聚合所有数据涉及的 actor（去重）。</summary>
        public List<GameObject> GetActors()
        {
            var result = new List<GameObject>();
            foreach (var d in _data)
                foreach (var a in d.GetActors())
                    if (a != null && !result.Contains(a)) result.Add(a);
            return result;
        }

        /// <summary>聚合所有数据涉及的 ASC（去重，跳过无 ASC 的）。</summary>
        public List<AbilitySystemComponent> GetTargetASCs()
        {
            var result = new List<AbilitySystemComponent>();
            foreach (var a in GetActors())
            {
                var asc = a.GetComponent<AbilitySystemComponent>();
                if (asc != null && !result.Contains(asc)) result.Add(asc);
            }
            return result;
        }

        public IEnumerator<TargetData> GetEnumerator() => _data.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _data.GetEnumerator();
    }
}
