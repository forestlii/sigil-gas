// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 表面效果组件：按情景（命中表面等）播放音效/粒子。
// 从命中物体的 SurfaceType 组件解析表面标签，再从效果库里查匹配记录播放。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Likeon.GAS
{
    [AddComponentMenu("Likeon/GAS/Surface Effect Component")]
    public class SurfaceEffectComponent : MonoBehaviour
    {
        [Header("情景")]
        [Tooltip("无法解析表面时的备用表面标签")]
        [SerializeField] private GameplayTag fallbackSurface;
        [Tooltip("默认情景标签（始终参与匹配）")]
        [SerializeField] private List<GameplayTag> defaultContexts = new List<GameplayTag>();

        [Header("效果库")]
        [SerializeField] private List<SurfaceEffectLibrary> libraries = new List<SurfaceEffectLibrary>();

        private readonly GameplayTagContainer _currentContexts = new GameplayTagContainer();

        /// <summary>每次播放表面效果时广播（供测试/调试，不依赖真实音画）。(effectTag, contexts, location)</summary>
        public event Action<GameplayTag, GameplayTagContainer, Vector3> OnSurfaceEffect;

        /// <summary>更新当前情景标签（覆盖）。</summary>
        public void SetActiveContexts(IEnumerable<GameplayTag> contexts)
        {
            _currentContexts.Clear();
            if (contexts != null) foreach (var c in contexts) _currentContexts.AddTag(c);
        }

        public void AddLibrary(SurfaceEffectLibrary lib) { if (lib != null && !libraries.Contains(lib)) libraries.Add(lib); }

        /// <summary>
        /// 播放一个表面效果。情景 = 默认 + 当前 + 传入表面；命中库里匹配记录则播音效+粒子。
        /// </summary>
        public void PlaySurfaceEffect(GameplayTag effectTag, Vector3 location, GameplayTag surface = default, Vector3 normal = default)
        {
            var contexts = AggregateContexts(surface);

            foreach (var lib in libraries)
            {
                if (lib == null) continue;
                foreach (var entry in lib.Match(effectTag, contexts))
                    SpawnEntry(entry, location, normal == default ? Vector3.up : normal);
            }

            OnSurfaceEffect?.Invoke(effectTag, contexts, location);
        }

        /// <summary>从一次射线命中播放表面效果。表面从命中物体的 SurfaceType 解析。</summary>
        public void PlaySurfaceEffectFromHit(GameplayTag effectTag, RaycastHit hit)
        {
            PlaySurfaceEffect(effectTag, hit.point, ResolveSurface(hit.collider), hit.normal);
        }

        // 解析命中物体的表面标签。
        private GameplayTag ResolveSurface(Collider col)
        {
            if (col != null)
            {
                var st = col.GetComponentInParent<SurfaceType>();
                if (st != null && st.Surface.IsValid) return st.Surface;
            }
            return fallbackSurface;
        }

        private GameplayTagContainer AggregateContexts(GameplayTag surface)
        {
            var contexts = new GameplayTagContainer(defaultContexts);
            contexts.AppendTags(_currentContexts);
            if (surface.IsValid) contexts.AddTag(surface);
            return contexts;
        }

        private static void SpawnEntry(SurfaceEffectEntry entry, Vector3 location, Vector3 normal)
        {
            if (entry.ParticlePrefab != null)
            {
                var ps = Instantiate(entry.ParticlePrefab, location, Quaternion.LookRotation(normal));
                ps.Play();
                Destroy(ps.gameObject, 5f);
            }
            if (entry.Sounds != null && entry.Sounds.Length > 0)
            {
                var clip = entry.Sounds[UnityEngine.Random.Range(0, entry.Sounds.Length)];
                if (clip != null) AudioSource.PlayClipAtPoint(clip, location);
            }
        }
    }
}
