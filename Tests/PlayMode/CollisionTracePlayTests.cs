// PlayMode 测试：CollisionTrace（通用碰撞检测 + 去重 + 状态）。
//  A) 激活命中一次且去重；B) HitFilter 过滤；C) 状态切换回调 + 未激活不命中；D) 重新激活清去重可再命中。
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Likeon.GAS;

namespace Likeon.GAS.PlayTests
{
    public class CollisionTracePlayTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void Cleanup()
        {
            foreach (var go in _spawned) if (go != null) Object.Destroy(go);
            _spawned.Clear();
        }

        private CollisionTrace NewTrace()
        {
            var go = new GameObject("Trace"); _spawned.Add(go);
            go.transform.position = Vector3.zero;
            var t = go.AddComponent<CollisionTrace>();
            t.SetSockets(go.transform);
            t.Radius = 0.5f;
            return t;
        }

        private GameObject NewTarget(Vector3 pos)
        {
            var go = new GameObject("Target"); _spawned.Add(go);
            go.transform.position = pos;
            go.AddComponent<SphereCollider>().radius = 0.5f;
            return go;
        }

        // ============ A) 命中一次 + 去重 ============
        [UnityTest]
        public IEnumerator A_Hit_OnceWithDedup()
        {
            var trace = NewTrace();
            NewTarget(Vector3.zero);
            int hits = 0; trace.OnHit += _ => hits++;
            yield return new WaitForFixedUpdate(); // 物理同步 collider

            trace.ToggleTraceState(true);
            trace.ForceTrace();
            trace.ForceTrace(); // 同一激活再检测 → 去重
            Assert.AreEqual(1, hits, "应命中一次且去重");
        }

        // ============ B) HitFilter 过滤 ============
        [UnityTest]
        public IEnumerator B_HitFilter_Excludes()
        {
            var trace = NewTrace();
            NewTarget(Vector3.zero);
            int hits = 0; trace.OnHit += _ => hits++;
            trace.HitFilter = _ => false; // 全部过滤
            yield return new WaitForFixedUpdate();

            trace.ToggleTraceState(true);
            trace.ForceTrace();
            Assert.AreEqual(0, hits, "HitFilter 返回 false 应忽略命中");
        }

        // ============ C) 状态切换 + 未激活不命中 ============
        [UnityTest]
        public IEnumerator C_StateChange_AndInactiveNoHit()
        {
            var trace = NewTrace();
            NewTarget(Vector3.zero);
            int hits = 0; trace.OnHit += _ => hits++;
            int stateChanges = 0; bool lastState = false;
            trace.OnTraceStateChanged += s => { stateChanges++; lastState = s; };
            yield return new WaitForFixedUpdate();

            trace.ForceTrace(); // 未激活
            Assert.AreEqual(0, hits, "未激活时 ForceTrace 不命中");

            trace.ToggleTraceState(true);
            Assert.AreEqual(1, stateChanges); Assert.IsTrue(lastState, "激活状态回调 true");
            trace.ToggleTraceState(false);
            Assert.AreEqual(2, stateChanges); Assert.IsFalse(lastState, "关闭状态回调 false");
        }

        // ============ D) 重新激活清去重 → 可再命中 ============
        [UnityTest]
        public IEnumerator D_Reactivate_ClearsDedup()
        {
            var trace = NewTrace();
            NewTarget(Vector3.zero);
            int hits = 0; trace.OnHit += _ => hits++;
            yield return new WaitForFixedUpdate();

            trace.ToggleTraceState(true);
            trace.ForceTrace();
            trace.ToggleTraceState(false);
            trace.ToggleTraceState(true); // 重新激活清去重
            trace.ForceTrace();
            Assert.AreEqual(2, hits, "重新激活后应可再次命中同一目标");
        }
    }
}
