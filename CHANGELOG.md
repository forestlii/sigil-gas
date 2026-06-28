# Changelog

All notable changes to Sigil are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/).

本文件记录 Sigil 的所有重要变更。格式遵循 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，
版本号遵循 [语义化版本](https://semver.org/lang/zh-CN/)。

## [Unreleased]

## [0.3.0] - 2026-06-29

Verified on Unity 6000.4.10f1 — **EditMode 21 + PlayMode 86 = 107 automated tests, all green**.

在 Unity 6000.4.10f1 实测 **EditMode 21 + PlayMode 86 = 107 个自动化测试全过**。

### Changed / 变更

- **BREAKING — Movement extracted to a companion package / 移动拆为配套包** `com.likeon.gas.movement`. `MovementSystemComponent`, `CharacterMovementSystemComponent`, the locomotion animation layer (`LocomotionAnimationDriver` / `LocomotionMath` / `LocomotionTypes`), `MovementDefinition` / `MovementSettings` / `MovementTags`, and the sample Animator Controller generator now live there. Namespace is unchanged (`Likeon.GAS`); install the companion package to keep using movement. Rationale: movement is a *consumer* of the GameplayTag state bus, not part of the ability system — keeping the GAS core focused. / 移动是 GAS 状态总线的消费方、非能力系统本身，拆出让核心专注；命名空间不变，装配套包即可继续用。
- The playable demo now uses a minimal built-in `CharacterController` mover, so the core sample stays self-contained (no dependency on the movement package). / 可玩 demo 改用极简 CharacterController 移动，核心示例自给自足、不依赖 movement 包。

## [0.2.0] - 2026-06-29

Verified on Unity 6000.4.10f1 — **EditMode 21 + PlayMode 86 = 107 automated tests, all green**.

在 Unity 6000.4.10f1 实测 **EditMode 21 + PlayMode 86 = 107 个自动化测试全过**。

### Added / 新增

- **GameplayEffect stacking / 效果叠层** — `StackingType` (None / AggregateByTarget / AggregateBySource), `StackLimitCount`, duration-refresh & period-reset policies, and an expiration policy (clear entire stack / remove a single stack & refresh / refresh only). Modifiers and periodic execution scale by stack count; `OnActiveEffectStackChanged` (effect, old, new) + `ActiveGameplayEffect.StackCount` drive ×N UI badges. / 效果叠层：合并方式/上限/时长刷新/周期重置/到期策略，修改量与周期结算按层放大，层变事件 + 层数字段供 ×N 角标。
- **Ability grant/removal observability / 技能授予移除可观测性** — `AbilitySystemComponent.OnAbilityGiven` / `OnAbilityRemoved` events (payload `GameplayAbilitySpec`, fired for loadout & global grants too; removal fires before the instance is destroyed) + read-only `GetGrantedAbilities()`, so a loadout-driven ability bar can auto-populate. / 技能授予/移除事件（含批量授予；移除在销毁前回调）+ 只读已授予枚举，供技能栏自动增删订阅。

## [0.1.0] - 2026-06-29

First public release. Verified on Unity 6000.4.10f1 — **EditMode 21 + PlayMode 76 = 97 automated tests, all green** — with a playable demo. Single-player authoritative; stage-6 networking is the remaining roadmap item.

首个公开版本。在 Unity 6000.4.10f1 实测 **EditMode 21 + PlayMode 76 = 97 个自动化测试全过**，含可玩 Demo。单机权威逻辑，阶段 6 联网为后续路线。

### Added / 新增

- **Core ability system / 核心能力系统** — GameplayTag, AttributeSet (`AS_Health`/`AS_Stamina`/`AS_Mana`/`AS_Combat`/`AS_Poise`), GameplayEffect, GameplayAbility, AbilitySystemComponent, `AbilityLoadout`, `AbilityInteractionRules`.
- **AbilityTask framework / 技能任务框架** — `WaitDelay`, `WaitDelayOneFrame`, `WaitGameplayEvent`, `WaitInputPress`, `WaitTargetData`, `PlayMontageAndWaitForEvent`; auto-cancelled on ability end. / 协程异步任务，技能结束自动取消。
- **Targeting / 目标采集** — `TargetActor` (line/sphere trace), `TargetData`/`TargetDataHandle`, `TargetSource`.
- **Input dispatch / 输入分发** — routing, gating, buffering, control-setup stack; state-driven key polymorphism; optional `Likeon.GAS.UnityInput` binder. / 分发、门控、缓冲、控制集栈；状态驱动按键多态。
- **Melee combat / 近战战斗** — `AttackDefinition`, `AbilityActionSet`, `MeleeAttackTrace`, `DamageExecutionCalculation`, `CombatTeamAgent`, `CombatSystemComponent`.
- **Poise / 削韧** — `AS_Poise` + `PoiseComponent` (break, stagger, regen, recover). / 破防、硬直、恢复、复位。
- **Lock-on targeting / 锁定** — `TargetingSystemComponent` (filtering, selection, cycling, auto-drop, events). / 过滤、选择、切换、自动解锁。
- **Projectiles / 投射物** — `BulletDefinition` / `BulletInstance` / `BulletLauncher` (motion, spread, penetration, bullet chains, `Tick(dt)`).
- **Weapons / 武器** — `IWeapon` + `WeaponComponent` (equip, tag injection, active state, ranged fire). / 装备、标签注入、激活态、远程发射。
- **Hit-reaction pipeline / 受击反应管线** — `CombatFlowComponent` + `AttackResultProcessor`s (death, gameplay events, cues).
- **`GlobalAbilitySystem` / 全局技能效果系统** — apply an ability/effect to all registered ASCs at once; late-registered ASCs auto-receive global items; optional `GlobalAbilitySystemRegistrant`. / 全局施加，后注册者自动补全，含可选自动注册组件。
- **`GamePhaseSubsystem` + `GamePhaseAbility` / 游戏阶段** — nested hierarchical-tag phases (parent/child coexist, siblings exclusive) with start/end observers. / 嵌套层级 tag 阶段（父子共存、兄弟互斥）+ 观察者。
- **`CollisionTrace` / 通用碰撞检测** — generic OverlapSphere hit detection with dedup, state toggle, and filtering (traps / AOE / hazards), distinct from `MeleeAttackTrace`. / 通用碰撞检测（去重+状态+过滤）。
- **`MovementCancellation` / root motion 取消窗口** — Animation-Event-driven window toggling `Animator.applyRootMotion` when the player moves. / 移动取消动画 root motion 窗口。
- **Movement / 移动** — `MovementSystemComponent`, `CharacterMovementSystemComponent`, state mirrored to the ASC; data-driven locomotion layer (in-air state, view-relative aim offset, core-state tags → Animator) + sample layered Animator Controller generator (`Likeon ▸ GAS ▸ Samples`). / 移动系统 + 数据驱动运动层 + 示例分层 Controller 生成器。
- **Observability / 可观测性** — change events for any UI/AI/save consumer to subscribe: `OnAttributeChanged`, `OnTagChanged`, `OnAbilityActivated/Ended`, `OnGameplayEvent`, **`OnActiveEffectAdded`/`OnActiveEffectRemoved`** + read-only `GetActiveGameplayEffects()` / `GetActiveGameplayEffect(handle)` (buff/debuff bars with remaining time), plus combat/poise/targeting/weapon events. / 对外变更事件，任何 UI/AI/存档自行订阅；含激活效果增减事件 + 只读枚举（带剩余时长）。
- **Presentation / 表现** — GameplayCue, surface effects (`SurfaceEffectComponent`/`SurfaceEffectLibrary`), camera blend stack.
- **Editor suite / 编辑器套件** — GameplayTag picker, tag registry & window, `[SerializeReference]` picker, asset inspectors, tag scanner — all under one top-level **Likeon** menu.
- **Playable demo / 可玩 Demo** — `Samples~/PlayableDemo`, importable from the Package Manager.

### Fixed / 修复

- **GamePhase nesting / 阶段嵌套** — corrected a literal condition that would wrongly end a parent phase when starting a child; parent & child now coexist per the documented intent. / 修正会误结束父阶段的逻辑，父子正确共存。

### Known limitations / 已知边界

- No networking yet — single-player authoritative logic. / 暂无联网，单机权威逻辑。
- **No in-package UI framework — by design.** Sigil broadcasts change events (attributes, tags, abilities, active effects, combat, etc.); bind any UI solution (UGUI / UI Toolkit / third-party) to them. / **不含包内 UI 框架——刻意如此**：Sigil 对外广播变更事件，任何 UI 方案自行订阅。
- Locomotion ships a data-driven layer + a sample Animator Controller generator, but no authored animation clips. / 运动含数据驱动层 + 示例 Controller 生成器，但不含成品动画 clip。
- The demo uses placeholder programmer art (capsules). / Demo 为程序员美术（胶囊体）。

[Unreleased]: #unreleased
[0.3.0]: #030---2026-06-29
[0.2.0]: #020---2026-06-29
[0.1.0]: #010---2026-06-29
