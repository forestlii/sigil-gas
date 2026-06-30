# Sigil — Unity 动作战斗与能力框架（GAS 风格）

[English](README.md) | [简体中文](README.zh-CN.md)

一套 GAS 风格的 **Unity 动作战斗与能力框架**。以 **GameplayTag 驱动的状态总线**串起
能力 / 属性 / 效果 / 输入分发 / 近战与远程战斗 / 表现，让
"输入 → 技能 → 效果 → 属性 → 反馈"保持解耦、数据驱动。（移动在可选配套包里，见下文。）

- **引擎：** Unity 6 — 在 6000.4.10f1 开发并验证
- **测试：** EditMode 21 + PlayMode 86 = **107 个自动化测试全过**
- **范围：** 单机权威逻辑（暂无联网）
- **发布者：** Likeon · 命名空间 `Likeon.GAS`

> "GAS 风格"指的是架构（GameplayTag 状态总线 + 能力/属性/效果）。这是一份独立的 Unity 实现，不含任何第三方引擎代码。

## 安装

把 `com.likeon.gas` 文件夹放进工程的 `Packages/` 目录，或用
**Package Manager → Add package from disk…** 选中 `package.json`。唯一依赖是 `com.unity.inputsystem`。

### 运行测试

包内 `Tests/` 自带 EditMode + PlayMode 测试。在工程 `Packages/manifest.json` 的
`"testables"` 里加上本包，再打开 **Window → General → Test Runner** 即可运行：

```json
"testables": [ "com.likeon.gas" ]
```

## 功能

### 核心能力系统
- **GameplayTag** — 层级标签、容器、引用计数松散标签、标签查询。
- **AttributeSet** — 属性 + Pre/Post 结算钩子；内置 `AS_Health`、`AS_Stamina`、`AS_Mana`、`AS_Combat`、`AS_Poise`。
- **GameplayEffect** — 瞬时/持续/无限、周期、修改器、自定义执行计算、授予标签、施加条件、SetByCaller、**叠层**（按来源/目标合并、层数上限、刷新/重置/到期策略、修改量按层放大）。
- **GameplayAbility** — 激活策略（并行/可替换独占/阻断独占）、消耗与冷却、激活期标签、效果容器。
- **AbilitySystemComponent** — 中枢：拥有标签、属性集、激活效果、技能授予与激活、独占、交互规则。
- **AbilityLoadout** — 批量授予技能+效果+属性集，整批撤销。
- **AbilityInteractionRules** — 状态感知的 阻挡/取消/激活准入 规则，按角色当前标签驱动。
- **AbilityTask 框架** — 协程异步任务：`WaitDelay`、`WaitDelayOneFrame`、`WaitGameplayEvent`、`WaitInputPress`、`WaitTargetData`、`PlayMontageAndWaitForEvent`（播 Animator 状态同时监听 gameplay 事件）；技能结束自动取消。
- **目标采集** — `TargetActor`（线/球 trace）产出 `TargetData`，外加 `TargetSource`（自身 / 事件数据）。
- **GlobalAbilitySystem** — 一次性给所有注册 ASC 施加技能/效果（全场 buff/debuff、阶段技能）；后注册的 ASC 自动补全。可选 `GlobalAbilitySystemRegistrant` 自动注册。
- **GamePhaseSubsystem** — 嵌套层级 tag 游戏阶段（`GamePhaseAbility`）：父子阶段共存、兄弟互斥；开始/结束观察者（精确/部分匹配）。

### 输入分发
- **InputSystemComponent** — 与后端解耦的分发、门控、缓冲、push/pop 控制集栈。
- **InputChecker / InputProcessor / InputControlSetup**（FirstOnly / MatchAll）。
- **状态驱动按键多态** — 同一输入标签挂多个处理器；`FirstOnly` 取第一个状态查询通过的（如一键 *冲刺中→滑铲，否则→下蹲*）。
- 连招输入缓冲窗口；可选 `Likeon.GAS.UnityInput` 适配器。

### 战斗
- **近战** — `AttackDefinition`、`MeleeAttackTrace`（动画事件判定帧 + 挂点球扫）、`DamageExecutionCalculation`、`CombatTeamAgent`、`CombatSystemComponent`。
- **连段选择** — `AbilityActionSet` 按来源/目标状态选动作（分层标签查询）。
- **削韧/硬直** — `AS_Poise` + `PoiseComponent`：破防、硬直、恢复、复位。
- **锁定** — `TargetingSystemComponent`：overlap 搜集 → 阵营/死亡/标签/视角锥/视线 过滤 → 最佳/最近 选择，左右切换，自动解锁，锁定事件。
- **投射物** — `BulletDefinition` / `BulletInstance` / `BulletLauncher`：自走积分、散射、球扫命中、角色/地图穿透、子弹链；`Tick(dt)` 让飞行与 Unity 时间解耦。
- **武器** — `IWeapon` + `WeaponComponent`：装备/卸下、注入武器标签供技能门控、激活态切换、远程 `FireProjectile`。
- **受击反应管线** — `CombatFlowComponent` + 一组 `AttackResultProcessor`（死亡、标签过滤的 gameplay 事件、gameplay cue）。
- **CollisionTrace** — 通用 `OverlapSphere` 命中检测，含单次激活去重、开关状态、过滤——用于陷阱、AOE 区域、环境伤害（与 `MeleeAttackTrace` 分工）。
- **MovementCancellation** — 动画事件驱动的窗口，玩家移动时切换 `Animator.applyRootMotion`，让攻击位移可被移动取消。

### 移动 — 配套包
移动与运动动画在**独立配套包** [`com.likeon.gas.movement`](https://github.com/forestlii/sigil-movement)
（标签驱动的 `CharacterController` 移动状态机 + 数据驱动运动动画层 + 示例 Animator Controller 生成器）。
刻意不放核心：移动是状态总线的*消费方*、非能力系统——所以核心可配你自己的移动，也可搭这个包。

### 表现
- **GameplayCue** — 标签驱动 VFX/SFX，经 `GameplayCueManager` 按标签层级路由。
- **表面效果** — `SurfaceEffectComponent` 从命中解析表面，从 `SurfaceEffectLibrary` 播放音效与粒子。
- **相机混合栈** — 第三人称行为按 AnimationCurve 权重混合，含 SphereCast 碰撞拉近。

### 可观测性 — 接你自己的 UI
Sigil **只做逻辑、不绑 UI**：它对外广播变更事件，让*任意* UI 方案（UGUI / UI Toolkit / 第三方）订阅并渲染。
画 HUD 是宿主工程的事——Sigil 的职责是把数据暴露出来。主要事件：

- `AbilitySystemComponent`：`OnAttributeChanged`（血/蓝/耐力条）、`OnTagChanged`（状态图标）、`OnAbilityActivated` / `OnAbilityEnded`、**`OnAbilityGiven` / `OnAbilityRemoved`** + 只读 `GetGrantedAbilities()`（供 loadout 驱动的技能栏）、`OnGameplayEvent`、**`OnActiveEffectAdded` / `OnActiveEffectRemoved` / `OnActiveEffectStackChanged`** + 只读 `GetActiveGameplayEffects()` / `GetActiveGameplayEffect(handle)`（buff/debuff 条，带剩余时间与 ×N 层数角标），以及 `GetCooldownRemainingForTags(...)`（冷却填充）。
- `CombatSystemComponent`：`OnDealtDamage` / `OnAttackResultReceived`（伤害飘字）。
- `PoiseComponent`：`OnPoiseBroken` / `OnPoiseRecovered`。`TargetingSystemComponent`：`OnTargetLockOn` / `OnTargetLockOff`。`WeaponComponent`：`OnEquipped` / `OnUnequipped` / `OnWeaponActiveStateChanged`。

### 编辑器工具
- GameplayTag 选择器（层级下拉 + 搜索 + 新增）、标签注册表与 `Likeon ▸ GAS ▸ Gameplay Tags` 窗口、`[SerializeReference]` 子类选择器、资产 Inspector、工程标签扫描器——统一收在顶部 **Likeon** 菜单下。

### 可玩 Demo
在 **Package Manager → Sigil → Samples → *Playable Demo*** 导入，打开 `GASDemo.unity` 按 Play。
一个运行时构建的**功能展示场**，把多条战斗线放进同一场景（程序员美术胶囊体）：
**近战 → 扣血 → cue、远程子弹、3 敌人间锁定切换、削韧破防、buff 叠层**，并带一个全靠订阅框架可观测性事件
渲染的自解释 HUD。操作：WASD 移动 · Shift 冲刺 · 鼠标看 · 空格/左键 近战 · 右键/F 远程 ·
Tab 锁定 · Q/E 切目标 · R 叠 buff。

## 配置（数据驱动）

Sigil 是**数据驱动**的：行为靠在 Inspector 里编辑 **ScriptableObject 资产**来配——不写代码，策划即可调。主要配置资产（都在 *Create → Likeon → GAS → …*）：

- **输入分发 & 互斥** — `InputControlSetup`：一组 `InputProcessor` 把 `InputTag` 映射到技能，`ExecutionType = FirstOnly` 实现"一键一技能"多态（第一个 `StateQuery` 通过的处理器胜出）。在 `InputConfig` 资产（`InputActionMappings`）里把物理键 → `InputTag`，`InputSystemComponent` 自动绑定。push/pop 控制集可整套切换键位（载具 / UI）。
- **技能互斥** — `AbilityInteractionRules`：数据驱动的技能间 block / cancel / 激活准入（按标签查询、状态感知）。另外每个 `GameplayAbility` 自身有 `ActivationGroup` / `ActivationRequiredTags` / `ActivationBlockedTags`。
- **技能 / 效果 / 攻击** — `GameplayAbility`、`GameplayEffect`（含叠层）、`AttackDefinition`、`BulletDefinition`、`AbilityLoadout`。

→ 分步配置（含"一个键 → 不同武器不同技能"）见[使用文档](Documentation~/Usage.zh-CN.md) §9（输入）与 §7.5（技能规则）。
→ **Playable Demo 自带一套完整范例**：导入它、打开 `DemoConfig.asset`，即可看/抄接好线的输入控制集、交互规则、技能与效果。

## 快速上手

```csharp
using Likeon.GAS;
using UnityEngine;

// 自定义技能：继承 GameplayAbility，重写 OnActivateAbility。
[CreateAssetMenu(menuName = "Game/Abilities/Slide")]
public class GA_Slide : GameplayAbility
{
    protected override void OnActivateAbility(GameplayEventData triggerData)
    {
        if (!CommitAbility()) { EndAbility(true); return; } // 扣耗 + 冷却

        var wait = AbilityTask_WaitDelay.WaitDelay(this, 0.3f);
        wait.OnFinish += () => EndAbility();
        wait.Activate();
    }
}
```

```csharp
// 在角色上初始化 ASC。
var asc = gameObject.AddComponent<AbilitySystemComponent>();
asc.AddAttributeSet(new AS_Health());
asc.AddAttributeSet(new AS_Stamina());

asc.GrantLoadout(defaultLoadout);                 // 批量授予
var handle = asc.GiveAbility(slideAbility);        // 单独授予

asc.OnAttributeChanged += d => Debug.Log($"{d.Attribute} : {d.OldValue} -> {d.NewValue}（来源 {d.Source?.SourceASC}）");

asc.AddLooseGameplayTag(GameplayTag.RequestTag("Movement.State.Sprint"));
asc.TryActivateAbility(handle);
```

```csharp
// 施加伤害效果（瞬时 GE → AS_Health.IncomingDamage；数值用 SetByCaller 传入）。
var spec = targetASC.MakeOutgoingSpec(damageEffect);
spec.SetSetByCallerMagnitude(GameplayTag.RequestTag("Data.Damage"), 25f);
targetASC.ApplyGameplayEffectSpecToSelf(spec);
```

### 状态驱动按键多态（一键 = 下蹲或滑铲）

```csharp
// 在一个 InputControlSetup 资产里配两个处理器，监听同一 InputTag，ExecutionType = FirstOnly：
//
//  [0] InputProcessor_ActivateAbilityByTag  (滑铲, 第一个)
//        InputTags  = InputTag.Crouch
//        StateQuery = MatchAllTags(Movement.State.Sprint)   // 仅冲刺时通过
//        AbilityTag = Ability.Slide
//  [1] InputProcessor_ActivateAbilityByTag  (下蹲, 第二个)
//        InputTags  = InputTag.Crouch
//        StateQuery = (空)                                  // 无条件
//        AbilityTag = Ability.Crouch
//
// 冲刺中 → [0] 通过 → 滑铲，FirstOnly 返回；否则 → [1] → 下蹲。

inputSystemComponent.ReceiveInput(
    GameplayTag.RequestTag("InputTag.Crouch"),
    InputTriggerEvent.Started,
    InputActionData.Empty);
```

## 状态与路线

单机核心**已完成并测试**（107 个自动化测试，与 movement 配套包一起跑）——覆盖能力系统、输入分发、
近战与远程战斗、游戏阶段、全局技能、通用碰撞检测、表现层。

- **移动 / 运动** — 在配套包 [`com.likeon.gas.movement`](https://github.com/forestlii/sigil-movement)。
- **UI** — **刻意不在范围内。** Sigil 只做逻辑；从任意 UI 方案订阅它的变更事件（见上文 *可观测性*）接入。
- **联网**（复制 / 预测）— 后续阶段规划。

## 许可

[MIT](LICENSE.md) — 免费用于任何用途（含商用），保留版权声明即可。
