# Sigil · Playable Demo（可玩示例）

一个**运行时构建**的可玩 demo，演示 Sigil 的完整闭环：
**输入 → GAS 技能 → 近战命中 → 扣血 → GameplayCue（火花 + 受击闪红）**，外加第三人称相机、敌人血条、体力消耗 + 回复。

A runtime-built playable demo showcasing the full loop: input → ability → melee hit → damage → GameplayCue, plus a third-person camera, enemy health bar and stamina.

## 怎么玩 How to play

1. 在 Package Manager 里选中 **Sigil**，点 **Samples** 标签下 *Playable Demo* 的 **Import**。
2. 打开导入到 `Assets/Samples/.../PlayableDemo/GASDemo.unity` 的场景。
3. 按 **Play**。

操作 Controls：
- **WASD** 移动 / **Shift** 冲刺 / **鼠标** 看 / **空格 或 鼠标左键** 攻击

> ⚠️ 工程的 *Active Input Handling* 需为 **Input System Package**（或 Both）。本 demo 走新输入系统（`Keyboard/Mouse.current`），纯旧 Input 会报错。

## 它怎么搭起来的 How it's built

- `GASDemo.cs`（MonoBehaviour）在 `Awake` 里**程序化构建**地面、相机、玩家、敌人并接好线——不依赖外部美术资产（程序员美术：胶囊体）。
- `DemoPlayerController.cs` / `DemoMeleeAbility.cs` / `DemoHealthBar.cs`：输入驱动、近战技能、血条。
- `Editor/DemoSceneBuilder.cs`：菜单 **Likeon ▸ GAS ▸ Build Demo Scene** 可一键重新生成场景。

## 程序集 Assemblies

- `GASDemo`（运行时，引用 `Likeon.GAS.Runtime` + `Unity.InputSystem`）
- `GASDemo.Editor`（编辑器，场景生成器）

## 说明 Notes

- 这是**功能演示**，美术为占位胶囊体；正式项目请替换为自己的模型/动画。
- demo 用到的标签（`Ability.MeleeAttack` / `Data.Damage` / `GameplayCue.Hit` / `Movement.Set.Default` / `SurfaceType.Stone`）在运行时通过 `RequestTag` 注册，无需预先在注册表里添加。
