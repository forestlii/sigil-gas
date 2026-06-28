# Sigil — Action Combat & Ability Framework for Unity (GAS-style)

> English / 中文 — both languages are included throughout this document.

A GAS-style **action-combat and ability framework** for Unity. A **GameplayTag-driven
state bus** ties together abilities, attributes, effects, input dispatch, melee & ranged
combat, movement and presentation — so input → ability → effect → attribute → feedback
stays decoupled and data-driven.

一套 GAS 风格的 **Unity 动作战斗与能力框架**。以 **GameplayTag 驱动的状态总线**串起
能力 / 属性 / 效果 / 输入分发 / 近战与远程战斗 / 移动 / 表现，让
"输入 → 技能 → 效果 → 属性 → 反馈"保持解耦、数据驱动。

- **Engine / 引擎:** Unity 6 — developed & verified on 6000.4.10f1 / 在 6000.4.10f1 开发并验证
- **Tests / 测试:** EditMode 21 + PlayMode 76 = **97 automated tests, all green / 97 个自动化测试全过**
- **Scope / 范围:** single-player authoritative logic (no networking yet) / 单机权威逻辑（暂无联网）
- **Publisher / 发布者:** Likeon · namespace `Likeon.GAS`

> "GAS-style" describes the architecture (a GameplayTag state bus with abilities, attributes
> and effects). This is an independent Unity implementation; no third-party engine code is included.
>
> "GAS 风格"指的是架构（GameplayTag 状态总线 + 能力/属性/效果）。这是一份独立的 Unity 实现，不含任何第三方引擎代码。

## Install / 安装

Copy the `com.likeon.gas` folder into your project's `Packages/` directory, or use
**Package Manager → Add package from disk…** and select `package.json`.
The only dependency is `com.unity.inputsystem`.

把 `com.likeon.gas` 文件夹放进工程的 `Packages/` 目录，或用
**Package Manager → Add package from disk…** 选中 `package.json`。唯一依赖是 `com.unity.inputsystem`。

## Features / 功能

### Core ability system / 核心能力系统
- **GameplayTag** — hierarchical tags, containers, ref-counted loose tags, tag queries. / 层级标签、容器、引用计数松散标签、标签查询。
- **AttributeSet** — attributes with Pre/Post change hooks; built-in `AS_Health`, `AS_Stamina`, `AS_Mana`, `AS_Combat`, `AS_Poise`. / 属性 + Pre/Post 结算钩子；内置上述属性集。
- **GameplayEffect** — Instant / Duration / Infinite, periodic, modifiers, custom execution calculations, granted tags, application conditions, SetByCaller, **stacking** (aggregate by source/target, stack limit, duration-refresh / period-reset / expiration policies, magnitude scales by stack count). / 瞬时/持续/无限、周期、修改器、自定义执行计算、授予标签、施加条件、SetByCaller、**叠层**（按来源/目标合并、层数上限、刷新/重置/到期策略、修改量按层放大）。
- **GameplayAbility** — activation policy (Parallel / Replaceable / Blocking), cost & cooldown, activation-owned tags, effect containers. / 激活策略（并行/可替换独占/阻断独占）、消耗与冷却、激活期标签、效果容器。
- **AbilitySystemComponent** — the hub: owned tags, attribute sets, active effects, ability granting/activation, exclusivity, interaction rules. / 中枢：拥有标签、属性集、激活效果、技能授予与激活、独占、交互规则。
- **AbilityLoadout** — batch-grant abilities + effects + attribute sets; revoke as one handle. / 批量授予技能+效果+属性集，整批撤销。
- **AbilityInteractionRules** — state-aware block / cancel / activation rules driven by the character's current tags. / 状态感知的 阻挡/取消/激活准入 规则，按角色当前标签驱动。
- **AbilityTask framework** — coroutine-based async tasks: `WaitDelay`, `WaitDelayOneFrame`, `WaitGameplayEvent`, `WaitInputPress`, `WaitTargetData`, `PlayMontageAndWaitForEvent` (play an Animator state while listening for gameplay events); auto-cancelled on ability end. / 协程异步任务（含播动画并等事件），技能结束自动取消。
- **Targeting** — `TargetActor` (line / sphere trace) producing `TargetData`, plus `TargetSource` (self / event-data). / 目标采集器 + 目标数据 + 目标来源。
- **GlobalAbilitySystem** — apply an ability/effect to every registered ASC at once (arena-wide buffs/debuffs, phase abilities); late-registered ASCs auto-receive global items. Optional `GlobalAbilitySystemRegistrant` for auto register. / 全局给所有注册 ASC 施加技能/效果，后注册者自动补全；可选自动注册组件。
- **GamePhaseSubsystem** — nested hierarchical-tag game phases (`GamePhaseAbility`): parent & child phases coexist, siblings are exclusive; start/end observers (exact / partial match). / 嵌套层级 tag 游戏阶段：父子共存、兄弟互斥，开始/结束观察者。

### Input dispatch / 输入分发
- **InputSystemComponent** — back-end-agnostic routing, gating, buffering, push/pop control-setup stack. / 与后端解耦的分发、门控、缓冲、控制集栈。
- **InputChecker / InputProcessor / InputControlSetup** (FirstOnly / MatchAll). / 门控 / 多态分发 / 控制集。
- **State-driven key polymorphism** — multiple processors on one input tag; `FirstOnly` picks the first whose state query passes (e.g. *sprint → slide, else crouch* on one key). / 状态驱动按键多态：同键不同技能。
- Input buffer windows for combos; optional `Likeon.GAS.UnityInput` binder. / 连招输入缓冲窗口；可选 Unity Input System 适配器。

### Combat / 战斗
- **Melee** — `AttackDefinition`, `MeleeAttackTrace` (animation-event hit windows + socket sphere-cast), `DamageExecutionCalculation`, `CombatTeamAgent`, `CombatSystemComponent`. / 近战：攻击定义、判定帧球扫、伤害执行计算、阵营过滤、攻击结果/受击反应/顿帧。
- **Combo selection** — `AbilityActionSet` picks actions by source/target state (layered tag queries). / 连段：按状态选动作。
- **Poise / stagger** — `AS_Poise` + `PoiseComponent`: break, stagger, regen, recover. / 削韧/硬直：破防→硬直→恢复→复位。
- **Lock-on** — `TargetingSystemComponent`: overlap collection → affiliation/dead/tag/view-cone/line-of-sight filtering → best/closest selection, left/right cycling, auto-drop, lock events. / 锁定：搜集→过滤链→选择→左右切换→失效自动解锁。
- **Projectiles** — `BulletDefinition` / `BulletInstance` / `BulletLauncher`: self-integrated motion, spread, swept-sphere hits, character/map penetration, bullet chains; `Tick(dt)` decouples flight from Unity time. / 投射物：自走、散射、穿透、子弹链。
- **Weapons** — `IWeapon` + `WeaponComponent`: equip/unequip, weapon-tag injection for ability gating, active-state toggling, ranged `FireProjectile`. / 武器：装备/卸下、标签注入、激活态、枪口发射。
- **Hit-reaction pipeline** — `CombatFlowComponent` + `AttackResultProcessor`s (death, tag-filtered gameplay events, gameplay cues). / 受击反应管线：死亡/触发事件/命中 cue。
- **CollisionTrace** — generic `OverlapSphere` hit detection with per-activation dedup, on/off state, and filtering — for traps, AOE zones, environmental hazards (distinct from `MeleeAttackTrace`). / 通用碰撞检测：去重 + 状态开关 + 过滤，用于陷阱/AOE 区域/环境伤害。
- **MovementCancellation** — Animation-Event-driven window that toggles `Animator.applyRootMotion` when the player moves, so attack root-motion can be cancelled by movement. / root motion 取消窗口：移动时取消动画位移。

### Movement / 移动
- **MovementSystemComponent** — movement-set/state machine, definition stack, rotation modes, input direction. / 移动状态机、定义栈、旋转模式。
- **CharacterMovementSystemComponent** — actual movement on a `CharacterController`. / 基于 CharacterController 的实际移动。
- **State bus to the ability system** — movement state mirrors onto the ASC as loose tags. / 移动状态镜像到 ASC。
- **Locomotion animation driver** — `LocomotionAnimationDriver` + `LocomotionMath`: computes speed, local-velocity yaw, 4-/8-way direction (dead-zone hysteresis), lean, in-air state (jump apex / falling time / just-landed / ground prediction), view-relative aim offset, and core-state tags — writing them all to Animator parameters. A menu generator (`Likeon ▸ GAS ▸ Samples`) builds a matching layered Animator Controller (8-way blend tree + jump/fall + upper-body aim-offset layer). / 运动动画驱动：速度/偏航/四八向/倾身/空中态(顶点·下落·着地预测)/视角 AimOffset/核心状态标签 → Animator 参数；附菜单生成器一键造对齐的分层 Controller。

### Presentation / 表现
- **GameplayCue** — tag-driven VFX/SFX, routed by tag hierarchy via `GameplayCueManager`. / 标签驱动表现，层级路由。
- **Surface effects** — `SurfaceEffectComponent` resolves a surface from a hit and plays audio & particles from a `SurfaceEffectLibrary`. / 表面效果：按命中表面播音画。
- **Camera blend stack** — third-person behaviors blended by an AnimationCurve-driven weight, with SphereCast collision pull-in. / 相机混合栈：第三人称 + 曲线混合 + 碰撞拉近。

### Observability — binding your own UI / 可观测性 — 接你自己的 UI
Sigil is **logic-only and UI-agnostic**: it broadcasts change events and lets *any* UI
solution (UGUI / UI Toolkit / third-party) subscribe and render. Drawing the HUD is the
host project's job — Sigil's job is to expose the data. Key events:

Sigil **只做逻辑、不绑 UI**：它对外广播变更事件，任何 UI 方案自行订阅渲染。画 HUD 是宿主的事，
Sigil 负责把数据暴露出来。主要事件：

- `AbilitySystemComponent`: `OnAttributeChanged` (health/mana/stamina bars), `OnTagChanged` (status icons), `OnAbilityActivated` / `OnAbilityEnded`, **`OnAbilityGiven` / `OnAbilityRemoved`** + read-only `GetGrantedAbilities()` for a loadout-driven ability bar, `OnGameplayEvent`, **`OnActiveEffectAdded` / `OnActiveEffectRemoved` / `OnActiveEffectStackChanged`** + read-only `GetActiveGameplayEffects()` / `GetActiveGameplayEffect(handle)` for buff/debuff bars with remaining time and ×N stack badges, and `GetCooldownRemainingForTags(...)` for cooldown fills. / 属性条、状态图标、技能激活、技能授予移除（含只读枚举）、激活效果增减/层变（含只读枚举读剩余时长与层数）、冷却查询。
- `CombatSystemComponent`: `OnDealtDamage` / `OnAttackResultReceived` (damage numbers). / 伤害飘字。
- `PoiseComponent`: `OnPoiseBroken` / `OnPoiseRecovered`. `TargetingSystemComponent`: `OnTargetLockOn` / `OnTargetLockOff`. `WeaponComponent`: `OnEquipped` / `OnUnequipped` / `OnWeaponActiveStateChanged`. / 削韧、锁定、武器事件。

### Editor tools / 编辑器工具
- GameplayTag picker (hierarchical dropdown + search + add), tag registry & a `Likeon ▸ GAS ▸ Gameplay Tags` window, `[SerializeReference]` subclass pickers, asset inspectors, a project tag scanner — all under one top-level **Likeon** menu. / 标签选择器、标签注册表与管理窗口、子类选择器、资产 Inspector、标签扫描器，统一收在顶部 Likeon 菜单下。

### Playable demo / 可玩 Demo
Import via **Package Manager → Sigil → Samples → *Playable Demo***, open `GASDemo.unity`, press Play:
WASD move · Shift sprint · mouse look · Space / left-click attack. Builds the full
input → ability → hit → damage → cue loop at runtime (placeholder programmer art).

在 **Package Manager → Sigil → Samples → *Playable Demo*** 导入，打开 `GASDemo.unity` 按 Play：
WASD 移动 / Shift 冲刺 / 鼠标看 / 空格或左键攻击，演示输入→技能→命中→扣血→cue 全闭环（程序员美术胶囊体）。

## Quick start / 快速上手

```csharp
using Likeon.GAS;
using UnityEngine;

// Author an ability: derive from GameplayAbility, override OnActivateAbility.
// 自定义技能：继承 GameplayAbility，重写 OnActivateAbility。
[CreateAssetMenu(menuName = "Game/Abilities/Slide")]
public class GA_Slide : GameplayAbility
{
    protected override void OnActivateAbility(GameplayEventData triggerData)
    {
        if (!CommitAbility()) { EndAbility(true); return; } // pay cost + cooldown / 扣耗 + 冷却

        var wait = AbilityTask_WaitDelay.WaitDelay(this, 0.3f);
        wait.OnFinish += () => EndAbility();
        wait.Activate();
    }
}
```

```csharp
// Set up an ability system on a character. / 在角色上初始化 ASC。
var asc = gameObject.AddComponent<AbilitySystemComponent>();
asc.AddAttributeSet(new AS_Health());
asc.AddAttributeSet(new AS_Stamina());

asc.GrantLoadout(defaultLoadout);                 // batch grant / 批量授予
var handle = asc.GiveAbility(slideAbility);        // single grant / 单独授予

asc.OnAttributeChanged += (attr, oldV, newV) => Debug.Log($"{attr} : {oldV} -> {newV}");

asc.AddLooseGameplayTag(GameplayTag.RequestTag("Movement.State.Sprint"));
asc.TryActivateAbility(handle);
```

```csharp
// Apply a damage effect (Instant GE → AS_Health.IncomingDamage; value via SetByCaller).
// 施加伤害效果（瞬时 GE → AS_Health.IncomingDamage；数值用 SetByCaller 传入）。
var spec = targetASC.MakeOutgoingSpec(damageEffect);
spec.SetSetByCallerMagnitude(GameplayTag.RequestTag("Data.Damage"), 25f);
targetASC.ApplyGameplayEffectSpecToSelf(spec);
```

### State-driven key polymorphism / 状态驱动按键多态（一键 = 下蹲或滑铲）

```csharp
// In an InputControlSetup asset, add two processors on the same InputTag, ExecutionType = FirstOnly:
// 在一个 InputControlSetup 资产里配两个处理器，监听同一 InputTag，ExecutionType = FirstOnly：
//
//  [0] InputProcessor_ActivateAbilityByTag  (slide / 滑铲, first)
//        InputTags  = InputTag.Crouch
//        StateQuery = MatchAllTags(Movement.State.Sprint)   // only while sprinting / 仅冲刺时通过
//        AbilityTag = Ability.Slide
//  [1] InputProcessor_ActivateAbilityByTag  (crouch / 下蹲, second)
//        InputTags  = InputTag.Crouch
//        StateQuery = (empty / 空)                          // unconditional / 无条件
//        AbilityTag = Ability.Crouch
//
// Sprinting → [0] passes → slide, FirstOnly returns. Otherwise → [1] → crouch.
// 冲刺中 → [0] 通过 → 滑铲，FirstOnly 返回；否则 → [1] → 下蹲。

inputSystemComponent.ReceiveInput(
    GameplayTag.RequestTag("InputTag.Crouch"),
    InputTriggerEvent.Started,
    InputActionData.Empty);
```

## Status & roadmap / 状态与路线

Single-player core is **complete and tested** (97 automated tests) — including game phases,
global abilities, generic collision tracing, and a locomotion animation **data layer** (with a
sample layered Controller generator).

- **Networking** (replication / prediction) — planned for a later stage. / 联网（复制/预测）—后续阶段。
- **UI** — **intentionally out of scope.** Sigil is logic-only; subscribe to its change events
  (see *Observability* above) from any UI solution. / **刻意不在范围内**：Sigil 只做逻辑，UI 自行订阅事件接入。
- **Locomotion** drives Animator parameters; final animation clips & feel are the host project's. / 运动层驱动 Animator 参数，成品动画与手感由宿主提供。

单机核心**已完成并测试**（97 个自动化测试）——含游戏阶段、全局技能、通用碰撞检测、运动动画**数据层**（附示例分层 Controller 生成器）。

## License / 许可

[MIT](LICENSE.md) — free for any use including commercial, just keep the copyright notice.
/ [MIT 许可证](LICENSE.md) — 免费用于任何用途（含商用），保留版权声明即可。
