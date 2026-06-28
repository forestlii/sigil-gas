// EditMode 测试：表现层纯逻辑（情景特效库匹配）。
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Likeon.GAS;

namespace Likeon.GAS.Tests
{
    public class PresentationEditTests
    {
        private static GameplayTag Tag(string s) => GameplayTag.RequestTag(s);

        [Test]
        public void SurfaceEffectLibrary_MatchesBySurface()
        {
            var lib = ScriptableObject.CreateInstance<SurfaceEffectLibrary>();
            var footstep = Tag("ContextEffect.Footstep");
            var grass = Tag("SurfaceType.Grass");
            lib.Entries.Add(new SurfaceEffectEntry
            {
                EffectTag = footstep,
                Contexts = new List<GameplayTag> { grass }
            });

            var ctxGrass = new GameplayTagContainer(); ctxGrass.AddTag(grass);
            var ctxStone = new GameplayTagContainer(); ctxStone.AddTag(Tag("SurfaceType.Stone"));

            int onGrass = 0; foreach (var _ in lib.Match(footstep, ctxGrass)) onGrass++;
            int onStone = 0; foreach (var _ in lib.Match(footstep, ctxStone)) onStone++;
            int wrongEffect = 0; foreach (var _ in lib.Match(Tag("ContextEffect.Hit"), ctxGrass)) wrongEffect++;

            Assert.AreEqual(1, onGrass, "草地脚步应匹配");
            Assert.AreEqual(0, onStone, "石头表面不应匹配草地记录");
            Assert.AreEqual(0, wrongEffect, "效果标签不符不应匹配");

            Object.DestroyImmediate(lib);
        }
    }
}
