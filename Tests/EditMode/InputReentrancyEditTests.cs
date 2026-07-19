// EditMode 测试：输入系统的重入安全（C6/C7）——阶段3。
//  C6) 缓冲触发（FireBufferedInput）分发中处理器开下一段连招窗口 → 该新窗口不被收尾关掉（连招链不断）；
//      且缓冲路径关窗只派发一次 (tag,false)（不重复）。
//  C7①) 处理器 HandleInput 里同步重入 HandleInput → 不抛 InvalidOperationException（借还池，非共享 scratch）。
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Likeon.GAS;

namespace Likeon.GAS.Tests
{
    // 处理时同步再分发一次内层输入 —— 制造 HandleInput 重入。
    public sealed class ReentrantDispatchProcessor : InputProcessor
    {
        public GameplayTag InnerTag;
        private bool _did;
        protected override void HandleInputStarted(InputSystemComponent ic, InputActionData data, GameplayTag inputTag)
        {
            if (_did) return;
            _did = true;
            ic.ProcessInput(InputActionData.Empty, InnerTag, InputTriggerEvent.Started);
        }
    }

    // 计数处理器。
    public sealed class CountingInputProcessor : InputProcessor
    {
        public int Count;
        protected override void HandleInputStarted(InputSystemComponent ic, InputActionData data, GameplayTag inputTag) => Count++;
    }

    // 处理时开一个缓冲窗口 —— 制造"分发过程中开下一段连招窗口"。
    public sealed class OpenWindowProcessor : InputProcessor
    {
        public GameplayTag WindowToOpen;
        protected override void HandleInputStarted(InputSystemComponent ic, InputActionData data, GameplayTag inputTag)
            => ic.OpenInputBufferWindow(WindowToOpen);
    }

    public class InputReentrancyEditTests
    {
        private static GameplayTag T(string s) => GameplayTag.RequestTag(s);
        private readonly List<Object> _objs = new List<Object>();

        [TearDown]
        public void Cleanup()
        {
            foreach (var o in _objs) if (o != null) Object.DestroyImmediate(o);
            _objs.Clear();
        }

        private InputSystemComponent NewIC()
        {
            var go = new GameObject("IC"); _objs.Add(go);
            return go.AddComponent<InputSystemComponent>();
        }

        private static T Proc<T>(string inputTag) where T : InputProcessor, new()
        {
            var p = new T();
            p.InputTags.AddTag(GameplayTag.RequestTag(inputTag));
            p.TriggerEvents = new List<InputTriggerEvent> { InputTriggerEvent.Started };
            return p;
        }

        private InputConfig ConfigWithWindows(params string[] windowThenAllowed)
        {
            // 入参成对：(窗口 tag, 该窗口允许的输入 tag)。
            var cfg = ScriptableObject.CreateInstance<InputConfig>(); _objs.Add(cfg);
            for (int i = 0; i + 1 < windowThenAllowed.Length; i += 2)
            {
                cfg.InputBufferDefinitions.Add(new InputBufferWindow
                {
                    Tag = T(windowThenAllowed[i]),
                    BufferType = EInputBufferType.LastInput,
                    AllowedInputs = new List<AllowedInput>
                    {
                        new AllowedInput { InputTag = T(windowThenAllowed[i + 1]), TriggerEvents = new List<InputTriggerEvent> { InputTriggerEvent.Started } }
                    }
                });
            }
            return cfg;
        }

        private static void SetConfig(InputSystemComponent ic, InputConfig cfg)
            => typeof(InputSystemComponent).GetField("inputConfig", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(ic, cfg);

        private InputControlSetup NewSetup(EInputProcessorExecutionType exec = EInputProcessorExecutionType.MatchAll)
        {
            var s = ScriptableObject.CreateInstance<InputControlSetup>(); _objs.Add(s);
            s.ExecutionType = exec;
            return s;
        }

        // ============ C6-a：缓冲路径关窗只派发一次 (tag,false)（去重复）============
        [Test]
        public void C6_CloseBufferWindowViaFire_DispatchesCloseEventOnce()
        {
            var ic = NewIC();
            SetConfig(ic, ConfigWithWindows("Window.A", "Input.X"));

            int closeA = 0;
            ic.OnInputBufferWindowStateChanged += (tag, open) => { if (!open && tag.Equals(T("Window.A"))) closeA++; };

            ic.OpenInputBufferWindow(T("Window.A"));
            Assert.IsTrue(ic.TrySaveInput(InputActionData.Empty, T("Input.X"), InputTriggerEvent.Started), "输入应存入 Window.A");

            ic.CloseInputBufferWindow(T("Window.A")); // 窗口有有效缓冲 → 走 FireBufferedInput

            Assert.AreEqual(1, closeA, "缓冲触发路径关窗只应派发一次 (Window.A,false)（修复前会派发两次）");
        }

        // ============ C6-b：分发中新开的连招窗口不被收尾关掉 ============
        [Test]
        public void C6_WindowOpenedDuringFire_SurvivesClose()
        {
            var ic = NewIC();
            SetConfig(ic, ConfigWithWindows("Window.A", "Input.X", "Window.B", "Input.Y"));

            var setup = NewSetup();
            var opener = Proc<OpenWindowProcessor>("Input.X");
            opener.WindowToOpen = T("Window.B"); // 触发 Input.X 时开 Window.B
            setup.AddProcessor(opener);
            ic.PushInputSetup(setup);

            var open = new HashSet<GameplayTag>();
            ic.OnInputBufferWindowStateChanged += (tag, isOpen) => { if (isOpen) open.Add(tag); else open.Remove(tag); };

            ic.OpenInputBufferWindow(T("Window.A"));
            Assert.IsTrue(ic.TrySaveInput(InputActionData.Empty, T("Input.X"), InputTriggerEvent.Started));

            ic.CloseInputBufferWindow(T("Window.A")); // FireBufferedInput → 分发 Input.X → opener 开 Window.B

            Assert.IsFalse(open.Contains(T("Window.A")), "Window.A 应已关闭");
            Assert.IsTrue(open.Contains(T("Window.B")), "分发中新开的 Window.B 不应被收尾一起关掉（修复前会被关）");
        }

        // ============ C7①：处理器里重入 HandleInput 不抛异常（借还池）============
        [Test]
        public void C7_ReentrantHandleInput_DoesNotThrow_AndBothProcessorsRun()
        {
            var ic = NewIC();
            var setup = NewSetup(EInputProcessorExecutionType.MatchAll);

            var reentrant = Proc<ReentrantDispatchProcessor>("Input.Outer");
            reentrant.InnerTag = T("Input.Inner");
            var countOuter = Proc<CountingInputProcessor>("Input.Outer");
            var countInner = Proc<CountingInputProcessor>("Input.Inner");

            // 顺序：重入处理器排前 → 它重入后，外层 foreach 还要继续到 countOuter，
            // 共享 scratch 被内层 Clear 会让外层 MoveNext 抛 InvalidOperationException。
            setup.AddProcessor(reentrant);
            setup.AddProcessor(countOuter);
            setup.AddProcessor(countInner);
            ic.PushInputSetup(setup);

            Assert.DoesNotThrow(() => ic.ProcessInput(InputActionData.Empty, T("Input.Outer"), InputTriggerEvent.Started),
                "处理器里重入分发不应抛异常（借还池而非共享 scratch）");
            Assert.AreEqual(1, countOuter.Count, "外层 Input.Outer 的 countOuter 应正常执行一次（重入未打断外层遍历）");
            Assert.AreEqual(1, countInner.Count, "内层重入分发的 Input.Inner 应执行一次");
        }
    }
}
