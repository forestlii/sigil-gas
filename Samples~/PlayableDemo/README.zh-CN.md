# Sigil · 可玩功能展示场

[English](README.md) | [简体中文](README.zh-CN.md)

一个**运行时构建**的可玩 demo，把 Sigil 已实现的多条战斗线串成一个场景给你直接玩：
**近战命中 → 扣血 → GameplayCue（火花 + 受击闪红）、远程子弹、锁定切换、削韧破防、buff 叠层**，
外加第三人称相机、敌人血条、体力消耗 + 回复、自解释 HUD。

## 怎么玩

1. 在 Package Manager 里选中 **Sigil**，点 **Samples** 标签下 *Playable Demo* 的 **Import**。
2. 打开导入到 `Assets/Samples/.../PlayableDemo/GASDemo.unity` 的场景。
3. 按 **Play**。

操作（同步显示在左上角 HUD）：

| 键 | 动作 |
|---|---|
| **WASD** | 移动 |
| **Shift** | 冲刺 |
| **鼠标** | 看 |
| **空格 / 左键** | 近战 |
| **右键 / F** | 远程子弹 |
| **Tab** | 锁定 / 解锁 |
| **Q / E** | 左 / 右切换锁定目标 |
| **R** | 叠加 Power buff（演示 stacking）|

> ⚠️ 工程的 *Active Input Handling* 需为 **Input System Package**（或 Both）。本 demo 走新输入系统
> （`Keyboard/Mouse.current`），纯旧 Input 会报错。

## 演示了哪些功能

| 功能 | 怎么看 |
|---|---|
| 近战 + Cue | 空格/左键挥砍，命中敌人闪红 + 火花 |
| 远程子弹 | 右键/F 发射子弹（`BulletLauncher`）；锁定时自动朝目标 |
| 锁定 | Tab 锁定最前方敌人，Q/E 在 3 个敌人间左右切换；HUD 显示当前目标 |
| 削韧破防 | 持续攻击削减目标削韧条；归零 → 敌人变黄破防（HUD 显示削韧条 + ★破防）|
| buff 叠层 | 连按 R 叠加 Power（+MaxHealth/层）；HUD 显示 `×N` 层数 + 倒计时 |
| 可观测性 | HUD 全靠订阅框架对外事件/只读枚举渲染——即"GAS 广播数据、UI 自接"的活演示 |

## 它怎么搭起来的

- `GASDemo.cs`（MonoBehaviour）在 `Awake` 里**程序化构建**地面、相机、玩家、3 个敌人并接好线——不依赖外部美术资产（程序员美术：胶囊体）。
- `DemoPlayerController.cs`：读键鼠输入，调用框架已实现的功能（技能激活 / 锁定切换 / 施加 buff），不含战斗逻辑本身。
- `DemoRangedAbility.cs` / `DemoRanged.cs`：远程技能（扣体力）→ 从枪口用 `BulletLauncher` 发射；给逻辑子弹挂可视小球。
- `DemoMeleeAbility.cs` / `DemoHealthBar.cs` / `DemoHUD.cs`：近战技能、世界空间血条、屏幕 HUD。
- `Editor/DemoSceneBuilder.cs`：菜单 **Likeon ▸ GAS ▸ Build Demo Scene** 可一键重新生成场景。

## 程序集

- `Likeon.GAS.Sample.PlayableDemo`（运行时，命名空间 `GASDemo`；引用 `Likeon.GAS.Runtime` + `Unity.InputSystem`）
- `Likeon.GAS.Sample.PlayableDemo.Editor`（编辑器，场景生成器）

## 说明

- 这是**功能演示**，美术为占位胶囊体；正式项目请替换为自己的模型/动画/特效。
- demo 用到的标签（`Ability.MeleeAttack` / `Ability.RangedAttack` / `Data.Damage` / `Data.PoiseDamage` / `GameplayCue.Hit` / `State.Staggered` / `SurfaceType.Stone`）在运行时通过 `RequestTag` 注册，无需预先在注册表里添加。
- demo 只是把核心包里**已实现并测试过**的功能调用出来给人看，不含任何额外战斗逻辑。
