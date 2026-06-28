# Changelog

All notable changes to Sigil are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/).

本文件记录 Sigil 的所有重要变更。格式遵循 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，
版本号遵循 [语义化版本](https://semver.org/lang/zh-CN/)。

## [Unreleased]

Verified on Unity 6000.4.10f1 — **EditMode 21 + PlayMode 71 = 92 automated tests, all green**.
Stage-A single-player combat is now complete (only stage-6 networking remains).

在 Unity 6000.4.10f1 实测 **EditMode 21 + PlayMode 71 = 92 个自动化测试全过**。A 类单机战斗至此全部完结（仅余阶段 6 联网）。

### Added / 新增

- **`PlayMontageAndWaitForEvent` ability task / 播放动画并等事件任务** — plays an Animator state (CrossFade + duration timing) while listening for matching gameplay events; callbacks OnCompleted/OnBlendOut/OnInterrupted/OnCancelled/OnEventReceived. / 播 Animator 状态同时监听事件，5 回调全覆盖。
- **`GlobalAbilitySystem` / 全局技能效果系统** — apply an ability/effect to all registered ASCs at once; late-registered ASCs auto-receive global items; optional `GlobalAbilitySystemRegistrant`. / 全局施加，后注册者自动补全，含可选自动注册组件。
- **`GamePhaseSubsystem` + `GamePhaseAbility` / 游戏阶段** — nested hierarchical-tag phases (parent/child coexist, siblings exclusive) with start/end observers. / 嵌套层级 tag 阶段（父子共存、兄弟互斥）+ 观察者。
- **`CollisionTrace` / 通用碰撞检测** — generic OverlapSphere hit detection with dedup, state toggle, and filtering (traps / AOE / hazards), distinct from `MeleeAttackTrace`. / 通用碰撞检测（去重+状态+过滤）。
- **`MovementCancellation` / root motion 取消窗口** — Animation-Event-driven window toggling `Animator.applyRootMotion` when the player moves. / 移动取消动画 root motion 窗口。
- **Locomotion data layer expansion / 运动动画数据层扩展** — in-air state (jump apex / falling time / just-landed / ground prediction), view-relative aim offset, core-state tags → Animator. / 空中态、视角 AimOffset、核心状态标签。
- **Sample layered Animator Controller generator / 示例分层 Controller 生成器** — `Likeon ▸ GAS ▸ Samples` builds a controller matching the locomotion driver (8-way blend tree + jump/fall + upper-body aim-offset layer). / 菜单一键生成对齐 driver 的分层 Controller。

### Fixed / 修复

- **GamePhase nesting / 阶段嵌套** — corrected the literal condition (ported from UE) that would wrongly end a parent phase when starting a child; parent & child now coexist per the documented intent. / 修正会误结束父阶段的逻辑，父子正确共存。

## [0.1.0] - 2026-06-28

First release. Verified on Unity 6000.4.10f1 — **EditMode 9 + PlayMode 42 = 51 automated tests, all green** — with a playable demo.

首个版本。在 Unity 6000.4.10f1 实测 **EditMode 9 + PlayMode 42 = 51 个自动化测试全过**，含可玩 Demo。

### Added / 新增

- **Core ability system / 核心能力系统** — GameplayTag, AttributeSet (`AS_Health`/`AS_Stamina`/`AS_Mana`/`AS_Combat`/`AS_Poise`), GameplayEffect, GameplayAbility, AbilitySystemComponent, `AbilityLoadout`, `AbilityInteractionRules`.
- **AbilityTask framework / 技能任务框架** — `WaitDelay`, `WaitDelayOneFrame`, `WaitGameplayEvent`, `WaitInputPress`, `WaitTargetData`; auto-cancelled on ability end. / 协程异步任务，技能结束自动取消。
- **Targeting / 目标采集** — `TargetActor` (line/sphere trace), `TargetData`/`TargetDataHandle`, `TargetSource`.
- **Input dispatch / 输入分发** — routing, gating, buffering, control-setup stack; state-driven key polymorphism; optional `Likeon.GAS.UnityInput` binder. / 分发、门控、缓冲、控制集栈；状态驱动按键多态。
- **Melee combat / 近战战斗** — `AttackDefinition`, `AbilityActionSet`, `MeleeAttackTrace`, `DamageExecutionCalculation`, `CombatTeamAgent`, `CombatSystemComponent`.
- **Poise / 削韧** — `AS_Poise` + `PoiseComponent` (break, stagger, regen, recover). / 破防、硬直、恢复、复位。
- **Lock-on targeting / 锁定** — `TargetingSystemComponent` (filtering, selection, cycling, auto-drop, events). / 过滤、选择、切换、自动解锁。
- **Projectiles / 投射物** — `BulletDefinition` / `BulletInstance` / `BulletLauncher` (motion, spread, penetration, bullet chains, `Tick(dt)`).
- **Weapons / 武器** — `IWeapon` + `WeaponComponent` (equip, tag injection, active state, ranged fire). / 装备、标签注入、激活态、远程发射。
- **Hit-reaction pipeline / 受击反应管线** — `CombatFlowComponent` + `AttackResultProcessor`s (death, gameplay events, cues).
- **Movement / 移动** — `MovementSystemComponent`, `CharacterMovementSystemComponent`, state mirrored to the ASC.
- **Presentation / 表现** — GameplayCue, surface effects (`SurfaceEffectComponent`/`SurfaceEffectLibrary`), camera blend stack.
- **Editor suite / 编辑器套件** — GameplayTag picker, tag registry & window, `[SerializeReference]` picker, asset inspectors, tag scanner — all under one top-level **Likeon** menu.
- **Playable demo / 可玩 Demo** — `Samples~/PlayableDemo`, importable from the Package Manager.

### Known limitations / 已知边界

- No networking yet — single-player authoritative logic. / 暂无联网，单机权威逻辑。
- Not included: a locomotion animation graph and an in-package UI framework. / 未含：运动动画框架、包内 UI 框架。
- The demo uses placeholder programmer art (capsules). / Demo 为程序员美术（胶囊体）。

[Unreleased]: #unreleased
[0.1.0]: #010---2026-06-28
