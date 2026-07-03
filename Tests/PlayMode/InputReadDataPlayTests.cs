// PlayMode 测试：InputSystemComponent.ReadData 对复合 2D 轴（WASD 2DVector）保留方向信息。
// 回归背景：曾按 ctx.control.valueType（触发键=float）判型，导致 Vector2 轴被兜底成恒定 (1,0)，
// WASD 四个键行为相同（movement demo"只会往前走"）。判型应使用 ctx.valueType（动作值类型）。
// 基于官方 InputTestFixture：替换输入运行时，让模拟按键在无焦点的 batchmode 下也能被处理。
using NUnit.Framework;
using UnityEngine.InputSystem;
using Likeon.GAS;

namespace Likeon.GAS.PlayTests
{
    public class InputReadDataPlayTests : InputTestFixture
    {
        private Keyboard _keyboard;
        private InputAction _move;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _move = new InputAction("Move", InputActionType.Value);
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
        }

        public override void TearDown()
        {
            _move.Disable();
            _move.Dispose();
            base.TearDown();
        }

        [Test]
        public void WasdComposite_ReadData_PreservesDirection()
        {
            InputActionData last = InputActionData.Empty;
            _move.performed += ctx => last = InputSystemComponent.ReadData(ctx);
            _move.Enable();

            Press(_keyboard.wKey);
            Assert.AreEqual(1f, last.Value.y, 1e-3f, "W 应读出 +y");
            Assert.AreEqual(0f, last.Value.x, 1e-3f, "W 不应有 x 分量");
            Release(_keyboard.wKey);

            Press(_keyboard.sKey);
            Assert.AreEqual(-1f, last.Value.y, 1e-3f, "S 应读出 -y");
            Release(_keyboard.sKey);

            Press(_keyboard.aKey);
            Assert.AreEqual(-1f, last.Value.x, 1e-3f, "A 应读出 -x");
            Assert.AreEqual(0f, last.Value.y, 1e-3f, "A 不应有 y 分量");
            Release(_keyboard.aKey);

            Press(_keyboard.dKey);
            Assert.AreEqual(1f, last.Value.x, 1e-3f, "D 应读出 +x");
            Release(_keyboard.dKey);
        }

        [Test]
        public void ButtonAction_ReadData_StillReadsScalar()
        {
            // 按钮/标量路径不受判型修复影响：x=按压值
            var jump = new InputAction("Jump", InputActionType.Button, "<Keyboard>/space");
            InputActionData last = InputActionData.Empty;
            jump.performed += ctx => last = InputSystemComponent.ReadData(ctx);
            jump.Enable();

            Press(_keyboard.spaceKey);
            Assert.Greater(last.Value.x, 0.5f, "按钮按下应读出 x>0.5");
            Assert.IsTrue(last.IsPressed);

            jump.Disable();
            jump.Dispose();
        }
    }
}
