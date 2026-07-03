// EditMode 测试：调试器只读 API（GetAttributeSets / GetOwnedGameplayTagCounts）与 GASDebuggerSession 订阅层。
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Likeon.GAS;
using Likeon.GAS.Editor;

namespace Likeon.GAS.Tests
{
    public class GASDebuggerEditTests
    {
        private GameObject _go;
        private AbilitySystemComponent _asc;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("DebuggerASC");
            _asc = _go.AddComponent<AbilitySystemComponent>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        [Test]
        public void GetAttributeSets_ReturnsAddedSets()
        {
            Assert.AreEqual(0, _asc.GetAttributeSets().Count, "初始应无属性集");

            var health = new AS_Health();
            _asc.AddAttributeSet(health);
            var sets = _asc.GetAttributeSets();
            Assert.AreEqual(1, sets.Count);
            Assert.AreSame(health, sets[0]);

            _asc.RemoveAttributeSet(health);
            Assert.AreEqual(0, _asc.GetAttributeSets().Count, "移除后应不再枚举到");
        }

        [Test]
        public void GetOwnedGameplayTagCounts_ExplicitTagsWithCounts()
        {
            var child = GameplayTag.RequestTag("Debug.State.A.B");
            var other = GameplayTag.RequestTag("Debug.State.C");
            _asc.AddLooseGameplayTag(child, 2);
            _asc.AddLooseGameplayTag(other);

            var list = new List<KeyValuePair<GameplayTag, int>>();
            _asc.GetOwnedGameplayTagCounts(list);

            Assert.AreEqual(2, list.Count, "只应枚举显式标签（不含自动展开的父标签）");
            int childCount = -1, otherCount = -1;
            foreach (var kv in list)
            {
                if (kv.Key.Equals(child)) childCount = kv.Value;
                if (kv.Key.Equals(other)) otherCount = kv.Value;
            }
            Assert.AreEqual(2, childCount, "多来源叠加的计数应可见");
            Assert.AreEqual(1, otherCount);

            _asc.RemoveLooseGameplayTag(child);
            _asc.GetOwnedGameplayTagCounts(list);
            foreach (var kv in list)
                if (kv.Key.Equals(child)) childCount = kv.Value;
            Assert.AreEqual(1, childCount, "移除一层后计数应递减");
        }

        [Test]
        public void Session_LogsEventsAndTracksAttributeChanges()
        {
            _asc.AddAttributeSet(new AS_Health());
            using var session = new GASDebuggerSession();
            session.SetTarget(_asc);

            // 标签事件
            _asc.AddLooseGameplayTag(GameplayTag.RequestTag("Debug.Session.Tag"));
            // GameplayEvent
            _asc.SendGameplayEvent(GameplayTag.RequestTag("Debug.Session.Event"));
            // 属性变更（带高亮记录）
            var healthAttr = GameplayAttribute.From<AS_Health>("Health");
            _asc.ApplyModToAttributeBase(healthAttr, EAttributeModifierOp.Add, -10f);
            // 激活效果增删
            var ge = ScriptableObject.CreateInstance<GameplayEffect>();
            ge.name = "GE_DebugBuff";
            ge.DurationType = EGameplayEffectDurationType.Infinite;
            var handle = _asc.ApplyGameplayEffectToSelf(ge);
            _asc.RemoveActiveGameplayEffect(handle);

            Assert.IsTrue(session.Log.Exists(e => e.Text.Contains("[Tag] +") && e.Text.Contains("Debug.Session.Tag")), "应记录标签增加");
            Assert.IsTrue(session.Log.Exists(e => e.Text.Contains("[Event]") && e.Text.Contains("Debug.Session.Event")), "应记录 GameplayEvent");
            Assert.IsTrue(session.Log.Exists(e => e.Text.Contains("[Attr] Health")), "应记录属性变更");
            Assert.IsTrue(session.Log.Exists(e => e.Text.Contains("[Effect] Added: GE_DebugBuff")), "应记录效果添加");
            Assert.IsTrue(session.Log.Exists(e => e.Text.Contains("[Effect] Removed: GE_DebugBuff")), "应记录效果移除");
            Assert.IsTrue(session.RecentAttributeChanges.ContainsKey(GASDebuggerSession.AttributeKey(healthAttr)), "属性变更应登记高亮时间戳");

            Object.DestroyImmediate(ge);
        }

        [Test]
        public void Window_CreateAndDestroy_DoesNotThrow()
        {
            // 冒烟：窗口生命周期（OnEnable 订阅 playModeStateChanged / OnDisable 释放会话）不抛异常
            var window = ScriptableObject.CreateInstance<GASDebuggerWindow>();
            Assert.IsNotNull(window);
            Object.DestroyImmediate(window);
        }

        [Test]
        public void Session_SetTargetNull_StopsListening()
        {
            using var session = new GASDebuggerSession();
            session.SetTarget(_asc);
            session.SetTarget(null);

            _asc.AddLooseGameplayTag(GameplayTag.RequestTag("Debug.Session.AfterDetach"));
            Assert.AreEqual(0, session.Log.Count, "退订后不应再记录事件");
        }
    }
}
