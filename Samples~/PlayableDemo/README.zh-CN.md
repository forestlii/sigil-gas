# Sigil · 可玩功能展示场

[English](README.md) | [简体中文](README.zh-CN.md)

一个**运行时构建**的可玩 demo，把 Sigil 已实现的多条战斗线串成一个场景给你直接玩：
**近战命中 → 扣血 → GameplayCue（火花 + 受击闪红）、远程子弹、锁定切换、削韧破防、buff 叠层**，
外加第三人称相机、敌人血条、体力消耗 + 回复、自解释 HUD。

## 怎么玩

1. 在 Package Manager 里选中 **Sigil**，点 **Samples** 标签下 *Playable Demo* 的 **Import**。
2. 打开导入到 `Assets/Samples/.../PlayableDemo/PlayableDemo.unity` 的场景。
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
| **1 / 2** | 切换武器：剑 / 斧（近战键变轻击 / 重击）|
| **G** | 专注（持续；近战被挡，远程取消）|
| **V** | 切换载具模式（近战键变鸣笛）|

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
| **输入分发**（键 → tag → 技能） | 按键喂 `InputSystemComponent.ReceiveInput(InputTag, …)`；一套 `InputProcessor_ActivateAbilityByTag` 的 `InputControlSetup` 把 InputTag → 技能——没有任何地方直接 `TryActivate` |
| **武器 → 不同技能** | 1/2 装备 剑 / 斧（`WeaponComponent` 注入 `Weapon.Sword` / `Weapon.Axe`）；同一个近战键据此多态成轻击或重击（FirstOnly 处理器按武器标签门控）|
| **技能 block / cancel** | 持续型专注（G）挂 `State.Focusing`；一个 `AbilityInteractionRules` 资产在专注期间 **block** 近战、并让远程 **cancel** 专注——HUD 显示"专注中—近战被挡" |
| **上下文切换（载具）** | V 键 `PushInputSetup` 一套载具键位；同一个近战键改广播鸣笛 `GameplayEvent` 而非攻击。`PopInputSetup` 恢复战斗 |
| 可观测性 | HUD 全靠订阅框架对外事件/只读枚举渲染——即"GAS 广播数据、UI 自接"的活演示 |

## 它怎么搭起来的

- `DemoConfig.cs`（ScriptableObject）汇总**全部策划可调配置**——输入控制集（键→tag→技能 + FirstOnly"互斥"）、`AbilityInteractionRules`（block/cancel）、技能、攻击、子弹、效果——都是资产引用。demo 场景把一个 `DemoConfig.asset` 接到 `PlayableDemo.Config` 上，策划在 Inspector 改这些 `.asset` 即可、不碰代码。（`Config` 留空时 `PlayableDemo` 会用 `DemoConfig.CreateDefault()` 在代码里建同一套默认值作回退。）用菜单 **Sigil ▸ GAS ▸ Generate Demo Config Assets**（`DemoConfigBuilder`）可重新生成资产并接好场景。
- `PlayableDemo.cs`（MonoBehaviour）在 `Awake` 里**程序化构建**地面、相机、玩家、3 个敌人——不依赖外部美术资产（程序员美术：胶囊体）。它还接好 `InputSystemComponent` + 战斗/载具两套 `InputControlSetup`、两把 `WeaponComponent`（剑/斧）、各技能，以及一个 `AbilityInteractionRules` 资产（专注 block 近战、远程 cancel 专注）。
- `DemoPlayerController.cs`：读键鼠，喂给 `InputSystemComponent.ReceiveInput(InputTag, …)`（走输入分发——没有任何地方直接 `TryActivate`），外加武器切换(1/2)、专注(G)、载具切换(V)、锁定、叠 buff。不含战斗逻辑本身。
- `DemoMeleeAbility.cs`：近战技能——按 `TraceEntryIndex` 决定轻击/重击；用 `AbilityTask_WaitDelay` 自管判定窗口。
- `DemoFocusAbility.cs`：持续型"专注"技能，激活期间挂 `State.Focusing`（供 block/cancel 演示）。
- `DemoRangedAbility.cs` / `DemoRanged.cs`：远程技能（扣体力）→ 从枪口用 `BulletLauncher` 发射；给逻辑子弹挂可视小球。
- `DemoHealthBar.cs` / `DemoHUD.cs`：世界空间血条、屏幕 HUD（含武器/载具/专注/鸣笛提示）。
- `Editor/DemoSceneBuilder.cs`：菜单 **Sigil ▸ GAS ▸ Build Demo Scene** 可一键重新生成场景。

## 程序集

- `Likeon.GAS.Sample.PlayableDemo`（运行时，命名空间 `PlayableDemo`；引用 `Likeon.GAS.Runtime` + `Unity.InputSystem`）
- `Likeon.GAS.Sample.PlayableDemo.Editor`（编辑器，场景生成器）

## 说明

- 这是**功能演示**，美术为占位胶囊体；正式项目请替换为自己的模型/动画/特效。
- demo 用到的标签（`Ability.MeleeAttack` / `Ability.HeavyAttack` / `Ability.RangedAttack` / `Ability.Focus` / `Data.Damage` / `Data.PoiseDamage` / `GameplayCue.Hit` / `State.Staggered` / `State.Focusing` / `Weapon.Sword` / `Weapon.Axe` / `InputTag.Melee` / `InputTag.Ranged` / `InputTag.Focus` / `Event.Honk` / `SurfaceType.Stone`）在运行时通过 `RequestTag` 注册，无需预先在注册表里添加。
- demo 只是把核心包里**已实现并测试过**的功能调用出来给人看，不含任何额外战斗逻辑。
