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

        /// <summary>移除：回调 OnRemove，按配置销毁宿主。</summary>
        internal void Remove(GameplayCueParameters parameters)
        {
            if (_removed) return;
            _removed = true;
            _notify?.OnRemove(_target, _spawned, parameters ?? _parameters);

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
    }
}
