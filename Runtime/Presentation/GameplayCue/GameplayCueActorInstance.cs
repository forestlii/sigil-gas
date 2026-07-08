// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 单个活跃有状态 Cue 的运行时托管实例（生命周期 + 逐帧 tick）。

using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>
    /// 托管一个活跃的 <see cref="GameplayCueNotify_Actor"/> 实例：
    /// 持有 spawn 出的表现宿主，逐帧自驱 WhileActive，移除时按配置销毁。
    /// 由 <see cref="GameplayCueManager"/> 创建/销毁，不应手动 AddComponent。
    /// </summary>
    public sealed class GameplayCueActorInstance : MonoBehaviour
    {
        private GameplayCueNotify_Actor _notify;
        private GameObject _target;
        private GameObject _spawned;
        private GameplayCueParameters _parameters;
        private bool _removed;

        /// <summary>生成的表现实例（SpawnPrefab 克隆，可空）。</summary>
        public GameObject Spawned => _spawned;

        /// <summary>创建并激活一个有状态 Cue 实例。</summary>
        internal static GameplayCueActorInstance Create(GameplayCueNotify_Actor notify, GameObject target, GameplayCueParameters parameters)
        {
            var host = new GameObject(notify != null ? $"CueActor_{notify.name}" : "CueActor");
            if (notify != null && notify.AttachToTarget && target != null)
                host.transform.SetParent(target.transform, false);
            else if (parameters != null)
                host.transform.position = parameters.Location;

            var inst = host.AddComponent<GameplayCueActorInstance>();
            inst._notify = notify;
            inst._target = target;
            inst._parameters = parameters;

            if (notify != null && notify.SpawnPrefab != null)
                inst._spawned = Instantiate(notify.SpawnPrefab, host.transform.position, host.transform.rotation, host.transform);

            notify?.OnActive(target, inst._spawned, parameters);
            return inst;
        }

        /// <summary>从池取出复用时：重新挂 target、启用、回调 OnActive（不重建 spawn 实例）。</summary>
        internal void Reactivate(GameObject target, GameplayCueParameters parameters)
        {
            _target = target;
            _parameters = parameters;
            _removed = false;
            var t = transform;
            if (_notify != null && _notify.AttachToTarget && target != null)
                t.SetParent(target.transform, false);
            else
            {
                t.SetParent(null, false);
                if (parameters != null) t.position = parameters.Location;
            }
            gameObject.SetActive(true);
            _notify?.OnActive(target, _spawned, parameters);
        }

        // 逐帧自驱 WhileActive（play mode）。ASC 不主动发 WhileActive，故持续 tick 在此。
        private void Update()
        {
            if (_removed || _notify == null) return;
            _notify.WhileActive(_target, _spawned, _parameters, Time.deltaTime);
        }

        /// <summary>手动驱动一次 WhileActive（供外部/测试主动 tick；生产逐帧由 Update 自驱）。</summary>
        internal void Tick(GameplayCueParameters parameters, float deltaTime)
        {
            if (_removed || _notify == null) return;
            _notify.WhileActive(_target, _spawned, parameters ?? _parameters, deltaTime);
        }

        /// <summary>移除第一步：回调 OnRemove（不销毁、不停用；后续由 Manager 决定回池还是销毁）。</summary>
        internal void NotifyRemove(GameplayCueParameters parameters)
        {
            if (_removed) return;
            _removed = true;
            _notify?.OnRemove(_target, _spawned, parameters ?? _parameters);
        }

        /// <summary>停用 + detach，准备回收进池（不销毁 spawn 实例，供下次 Reactivate 复用）。</summary>
        internal void ParkForPool()
        {
            transform.SetParent(null, false);
            if (gameObject != null) gameObject.SetActive(false);
        }

        /// <summary>不回池的收尾：按配置延迟/立即销毁；AutoDestroyOnRemove=false 则保留实例（用户手动管）。OnRemove 已由 <see cref="NotifyRemove"/> 调过。</summary>
        internal void DestroyOrKeep()
        {
            bool auto = _notify == null || _notify.AutoDestroyOnRemove;
            if (!auto) return;
            float delay = _notify != null ? _notify.AutoDestroyDelay : 0f;
            if (!Application.isPlaying)
                DestroyImmediate(gameObject);        // 编辑器/EditMode：无帧循环，立即销毁（延迟无意义）
            else if (delay > 0f)
                Destroy(gameObject, delay);          // 运行时：延迟销毁（可用于淡出）
            else
                Destroy(gameObject);
        }

        /// <summary>直接销毁（清池时用）。</summary>
        internal void DestroyNow()
        {
            if (gameObject == null) return;
            if (!Application.isPlaying) DestroyImmediate(gameObject);
            else Destroy(gameObject);
        }
    }
}
