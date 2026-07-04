// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 响应某个 Cue 标签的表现处理器（播特效/音效）。

using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>Cue 处理器基类（ScriptableObject 资产）。</summary>
    public abstract class GameplayCueNotify : ScriptableObject
    {
        [Tooltip("本处理器响应的 Cue 标签（层级匹配：响应它及其子标签）")]
        public GameplayTag CueTag;

        /// <summary>处理一次 cue 事件。</summary>
        public abstract void HandleCue(GameObject target, EGameplayCueEvent cueEvent, GameplayCueParameters parameters);
    }

    /// <summary>
    /// 通用静态 Cue：在事件点生成粒子 + 播音效。
    /// </summary>
    [CreateAssetMenu(fileName = "Cue_New", menuName = "Sigil/GAS/Gameplay Cue Notify (Static)")]
    public class GameplayCueNotify_Static : GameplayCueNotify
    {
        [Header("表现")]
        [Tooltip("命中/激活时生成的粒子预制体")]
        public ParticleSystem ParticlePrefab;
        [Tooltip("随机播放其一的音效")]
        public AudioClip[] SoundCues;
        [Tooltip("粒子自动销毁时间(秒)")]
        public float ParticleLifetime = 3f;

        [Header("触发时机")]
        public bool OnExecuted = true;
        public bool OnActive = true;

        public override void HandleCue(GameObject target, EGameplayCueEvent cueEvent, GameplayCueParameters p)
        {
            bool trigger = (cueEvent == EGameplayCueEvent.Executed && OnExecuted)
                        || (cueEvent == EGameplayCueEvent.OnActive && OnActive);
            if (!trigger) return;

            Vector3 loc = p != null ? p.Location : (target != null ? target.transform.position : Vector3.zero);
            Vector3 normal = p != null ? p.Normal : Vector3.up;

            if (ParticlePrefab != null)
            {
                var ps = Instantiate(ParticlePrefab, loc, Quaternion.LookRotation(normal.sqrMagnitude > 0.001f ? normal : Vector3.up));
                ps.Play();
                if (ParticleLifetime > 0f) Destroy(ps.gameObject, ParticleLifetime);
            }

            if (SoundCues != null && SoundCues.Length > 0)
            {
                var clip = SoundCues[Random.Range(0, SoundCues.Length)];
                if (clip != null) AudioSource.PlayClipAtPoint(clip, loc);
            }
        }
    }
}
