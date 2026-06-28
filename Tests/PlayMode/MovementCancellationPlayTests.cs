// PlayMode 测试：MovementCancellation（动画 root motion 取消窗口）。
//  A) 窗口期 Tick(true) 禁 root motion / Tick(false) 恢复 / EndWindow 恢复；
//  B) 窗口未开时 Tick 不改 root motion；C) 无 CharacterController 时 IsMoving=false。
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Likeon.GAS;

namespace Likeon.GAS.PlayTests
{
    public class MovementCancellationPlayTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void Cleanup()
        {
            foreach (var go in _spawned) if (go != null) Object.Destroy(go);
            _spawned.Clear();
        }

        // 造带 Animator(applyRootMotion=true) 的角色 + MovementCancellation
        private MovementCancellation NewComp(out Animator anim)
        {
            var go = new GameObject("Char"); _spawned.Add(go);
            anim = go.AddComponent<Animator>();
            anim.applyRootMotion = true;
            return go.AddComponent<MovementCancellation>(); // Awake 找到同物体 Animator
        }

        // ============ A) 窗口期切换 root motion ============
        [UnityTest]
        public IEnumerator A_Window_TogglesRootMotion()
        {
            var mc = NewComp(out var anim);
            yield return null; // Awake

            mc.BeginWindow();
            mc.Tick(true);
            Assert.IsFalse(anim.applyRootMotion, "移动时应禁 root motion");
            Assert.IsTrue(mc.IsRootMotionDisabled);

            mc.Tick(false);
            Assert.IsTrue(anim.applyRootMotion, "停止移动应恢复 root motion");
            Assert.IsFalse(mc.IsRootMotionDisabled);

            mc.Tick(true);
            Assert.IsFalse(anim.applyRootMotion, "再次移动再次禁用");

            mc.EndWindow();
            Assert.IsTrue(anim.applyRootMotion, "窗口结束应恢复 root motion");
            Assert.IsFalse(mc.IsWindowOpen);
        }

        // ============ B) 窗口未开 Tick 无效 ============
        [UnityTest]
        public IEnumerator B_Tick_NoOpWhenClosed()
        {
            var mc = NewComp(out var anim);
            yield return null;

            mc.Tick(true); // 未 BeginWindow
            Assert.IsTrue(anim.applyRootMotion, "窗口未开时 Tick 不应改 root motion");
            Assert.IsFalse(mc.IsRootMotionDisabled);
        }

        // ============ C) 无 CharacterController → IsMoving false ============
        [UnityTest]
        public IEnumerator C_IsMoving_FalseWithoutController()
        {
            var mc = NewComp(out _);
            yield return null;
            Assert.IsFalse(mc.IsMoving(), "无 CharacterController 时 IsMoving 应为 false");
        }
    }
}
