// PlayMode 测试：GamePhaseSubsystem（嵌套游戏阶段：父子共存、兄弟互斥）。
//  A) StartPhase → IsPhaseActive；B) 兄弟互斥；C) 父子共存（修正逻辑核心）；
//  D) PartialMatch 观察者；E) onEnded 回调（被兄弟取代时）。
// 单例 → SetUp/TearDown Clear()。
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Likeon.GAS;

namespace Likeon.GAS.PlayTests
{
    public class GamePhasePlayTests
    {
        private static GameplayTag Tag(string s) => GameplayTag.RequestTag(s);
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private readonly List<Object> _assets = new List<Object>();

        [SetUp]
        public void Reset() => GamePhaseSubsystem.Instance.Clear();

        [TearDown]
        public void Cleanup()
        {
            GamePhaseSubsystem.Instance.Clear();
            foreach (var go in _spawned) if (go != null) Object.Destroy(go);
            foreach (var a in _assets) if (a != null) Object.Destroy(a);
            _spawned.Clear(); _assets.Clear();
        }

        private AbilitySystemComponent NewGameAsc()
        {
            var go = new GameObject("GameManager"); _spawned.Add(go);
            return go.AddComponent<AbilitySystemComponent>();
        }

        private GamePhaseAbility NewPhase(string phaseTag)
        {
            var a = ScriptableObject.CreateInstance<GamePhaseAbility>(); _assets.Add(a);
            a.GamePhaseTag = Tag(phaseTag);
            return a;
        }

        // ============ A) 启动阶段 ============
        [UnityTest]
        public IEnumerator A_StartPhase_IsActive()
        {
            var asc = NewGameAsc();
            GamePhaseSubsystem.Instance.StartPhase(asc, NewPhase("Game.Playing"));
            Assert.IsTrue(GamePhaseSubsystem.Instance.IsPhaseActive(Tag("Game.Playing")), "启动后阶段应活跃");
            yield return null;
        }

        // ============ B) 兄弟互斥 ============
        [UnityTest]
        public IEnumerator B_SiblingPhases_AreExclusive()
        {
            var asc = NewGameAsc();
            GamePhaseSubsystem.Instance.StartPhase(asc, NewPhase("Game.Menu"));
            GamePhaseSubsystem.Instance.StartPhase(asc, NewPhase("Game.Playing"));
            Assert.IsFalse(GamePhaseSubsystem.Instance.IsPhaseActive(Tag("Game.Menu")), "兄弟阶段应被结束");
            Assert.IsTrue(GamePhaseSubsystem.Instance.IsPhaseActive(Tag("Game.Playing")), "新阶段应活跃");
            yield return null;
        }

        // ============ C) 父子共存（修正逻辑核心）============
        [UnityTest]
        public IEnumerator C_ParentChild_Coexist()
        {
            var asc = NewGameAsc();
            GamePhaseSubsystem.Instance.StartPhase(asc, NewPhase("Game.Playing"));
            GamePhaseSubsystem.Instance.StartPhase(asc, NewPhase("Game.Playing.WarmUp"));
            Assert.IsTrue(GamePhaseSubsystem.Instance.IsPhaseActive(Tag("Game.Playing")), "父阶段应保留（修正逻辑）");
            Assert.IsTrue(GamePhaseSubsystem.Instance.IsPhaseActive(Tag("Game.Playing.WarmUp")), "子阶段应活跃");

            // 再启动另一个子阶段 → 结束兄弟 WarmUp，但保留父 Playing
            GamePhaseSubsystem.Instance.StartPhase(asc, NewPhase("Game.Playing.PostGame"));
            Assert.IsTrue(GamePhaseSubsystem.Instance.IsPhaseActive(Tag("Game.Playing")), "父阶段仍保留");
            Assert.IsFalse(GamePhaseSubsystem.Instance.IsPhaseActive(Tag("Game.Playing.WarmUp")), "兄弟子阶段应结束");
            Assert.IsTrue(GamePhaseSubsystem.Instance.IsPhaseActive(Tag("Game.Playing.PostGame")), "新子阶段活跃");
            yield return null;
        }

        // ============ D) PartialMatch 观察者 ============
        [UnityTest]
        public IEnumerator D_PartialMatch_Observer()
        {
            var asc = NewGameAsc();
            int fired = 0;
            GamePhaseSubsystem.Instance.WhenPhaseStartsOrIsActive(Tag("Game.Playing"), EPhaseTagMatchType.PartialMatch, _ => fired++);
            GamePhaseSubsystem.Instance.StartPhase(asc, NewPhase("Game.Playing.WarmUp")); // 子级，PartialMatch 命中
            Assert.AreEqual(1, fired, "PartialMatch 观察者应被子阶段触发");
            yield return null;
        }

        // ============ E) onEnded 回调 ============
        [UnityTest]
        public IEnumerator E_OnEnded_CalledWhenReplaced()
        {
            var asc = NewGameAsc();
            int ended = 0;
            GamePhaseSubsystem.Instance.StartPhase(asc, NewPhase("Game.Menu"), _ => ended++);
            Assert.AreEqual(0, ended, "刚启动未结束");
            GamePhaseSubsystem.Instance.StartPhase(asc, NewPhase("Game.Playing")); // 取代 Game.Menu
            Assert.AreEqual(1, ended, "被兄弟取代时 onEnded 应回调");
            yield return null;
        }
    }
}
