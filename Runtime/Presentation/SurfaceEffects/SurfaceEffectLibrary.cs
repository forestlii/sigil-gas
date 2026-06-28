// Copyright 2026 Likeon All Rights Reserved.
// 表面效果库：把 (效果标签 + 情景标签) 映射到 音效/粒子。
// 例：脚步 + 草地表面 → 播草地脚步声 + 草屑粒子。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>世界物体上的表面标记。用此组件标注物体表面类型（如 SurfaceType.Grass）。</summary>
    [AddComponentMenu("Likeon/GAS/Surface Type")]
    public class SurfaceType : MonoBehaviour
    {
        [Tooltip("表面类型标签，如 SurfaceType.Grass / Stone")]
        public GameplayTag Surface;
    }

    /// <summary>一条表面效果映射。</summary>
    [Serializable]
    public struct SurfaceEffectEntry
    {
        [Tooltip("效果标签，如脚步 / 命中")]
        public GameplayTag EffectTag;
        [Tooltip("生效所需的情景标签（如表面=Grass）；全部满足才匹配")]
        public List<GameplayTag> Contexts;
        public AudioClip[] Sounds;
        public ParticleSystem ParticlePrefab;
    }

    /// <summary>表面效果库资产。</summary>
    [CreateAssetMenu(fileName = "SurfaceEffects_New", menuName = "Likeon/GAS/Surface Effect Library")]
    public class SurfaceEffectLibrary : ScriptableObject
    {
        public List<SurfaceEffectEntry> Entries = new List<SurfaceEffectEntry>();

        /// <summary>找出匹配（效果标签相符 且 情景标签全部具备）的记录。</summary>
        public IEnumerable<SurfaceEffectEntry> Match(GameplayTag effectTag, GameplayTagContainer activeContexts)
        {
            foreach (var e in Entries)
            {
                if (!e.EffectTag.MatchesTagExact(effectTag)) continue;
                bool allContextsPresent = true;
                if (e.Contexts != null)
                    foreach (var c in e.Contexts)
                        if (c.IsValid && (activeContexts == null || !activeContexts.HasTag(c))) { allContextsPresent = false; break; }
                if (allContextsPresent) yield return e;
            }
        }
    }
}
