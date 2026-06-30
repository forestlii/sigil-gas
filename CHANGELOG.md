# Changelog

[English](CHANGELOG.md) | [简体中文](CHANGELOG.zh-CN.md)

All notable changes to Sigil are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

- **Modular additional ability costs** (`AbilityCost`). Abilities can now carry a list of pluggable, non-attribute costs (ammo, charges, custom resources) beyond the single attribute-based `CostEffect`. Each `AbilityCost` is a `ScriptableObject` with `CheckCost` / `ApplyCost` and an `OnlyApplyCostOnHit` flag; activation requires *all* costs to be affordable, `CommitAbility` pays the non-on-hit ones, and `GameplayAbility.ApplyOnHitCosts()` pays the on-hit ones once a hit is confirmed. Costs are cloned per granted ability instance (mirrors UE `Instanced` costs), so charge state never leaks between characters.
- **Per-frame ability tick** (`GameplayAbility.AbilityTick(float)` gated by `EnableTick`). The ASC drives `AbilityTick` each frame for active abilities that opt in — an alternative to coroutine `AbilityTask`s for charge/scan loops.

### Changed

- **BREAKING — activation-group rename.** The enum `EAbilityActivationPolicy` → `EAbilityActivationGroup` and its values `Parallel` / `Replaceable` / `Blocking` → `Independent` / `ExclusiveReplaceable` / `ExclusiveBlocking`; the field `GameplayAbility.ActivationPolicy` → `ActivationGroup`; ASC methods `IsActivationPolicyBlocked` / `RegisterAbilityPolicy` / `UnregisterAbilityPolicy` / `CancelAbilitiesWithPolicy` → `…ActivationGroup`. This aligns the *naming* with the underlying concept (the methods were already called `ChangeActivationGroup`). Existing ability assets keep their values via `[FormerlySerializedAs]` and the enum's int order is unchanged.
- **BREAKING — `OnAttributeChanged` now carries the source.** The event signature changed from `Action<GameplayAttribute, float, float>` to `Action<AttributeChangeData>`, where `AttributeChangeData` carries `Attribute` / `OldValue` / `NewValue` / `Source` (a `GameplayEffectContext` — who/what caused the change; null when there is no single source, e.g. removal or inhibition). Damage/healing paths thread the attacker's context through, so a floating-damage-number UI can now tell *who hit whom*.

### Fixed

- **`AbilityInteractionRules.AbilityTagsToBlock` is now enforced.** Previously the field was collected but never read, so "block other abilities from activating while this one is active" had no effect (only `AbilityTagsToCancel` worked). An active ability now contributes its block tags to a reference-counted set on the ASC; any ability whose `AbilityTags` match is denied activation until every blocking source ends. New API: `AbilitySystemComponent.AreAbilityTagsBlocked(...)` and `AbilityInteractionRules.AddBaseRule(...)` / `AddConditionalRules(...)` for building rules in code. Covered by 6 new PlayMode tests (`AbilityBlockTagsPlayTests`). Test totals: **EditMode 21 + PlayMode 95 = 116**.

### Changed

- **Playable Demo upgraded to a feature showcase**. The bundled sample now puts several combat lines in one runtime-built scene — melee, **ranged projectiles**, **lock-on switching between 3 enemies**, **poise/stagger**, and **stacking buffs** — with a self-explanatory on-screen HUD rendered purely from the framework's observability events. No framework code changed; the demo simply exercises features that were already implemented and tested. New demo scripts: `DemoRanged` / `DemoRangedAbility` / `DemoHUD`.
- **Demo now also showcases the input/ability wiring**: input dispatch (keys → `InputTag` → `InputProcessor_ActivateAbilityByTag` → ability, no direct `TryActivate`), context switching (`Push/PopInputSetup` for a vehicle scheme where the melee key becomes a horn), ability block/cancel via an `AbilityInteractionRules` asset (a channeled Focus blocks melee; ranged cancels Focus), and weapon → different abilities (`WeaponComponent` injects `Weapon.Sword`/`Weapon.Axe`; the melee key polymorphs to light/heavy). New demo script `DemoFocusAbility`. Still no framework changes.
- **Demo is now data-driven** — all config (input control setups, ability-interaction rules, abilities, attacks, bullet, effects) lives in a designer-editable `DemoConfig` asset (with the sub-assets nested inside it). `GASDemo` reads it from a `Config` field (wired in the demo scene); leave it empty and it falls back to building the same defaults in code (so a bare `AddComponent` / the headless smoke test still works). Menu **Likeon ▸ GAS ▸ Generate Demo Config Assets** regenerates the asset + wires the scene. New: `DemoConfig`, `DemoConfigBuilder`.
- Demo smoke tests expanded from 1 to **11** (melee / lock-on / ranged / buff-stacking + input-dispatch / weapon-switch / focus-block / ranged-cancel / vehicle-horn + config-integrity / assigned-config-build), all green. They now ship **inside the sample** at `Samples~/PlayableDemo/Tests/`, so importing the Playable Demo brings runnable tests. Test totals: **EditMode 21 + PlayMode 102 = 123**.
- Tests moved into the in-package `Tests/` folder (PlayMode + EditMode) so they ship with the package and can be run via `"testables"`.

## [0.3.0] - 2026-06-29

Verified on Unity 6000.4.10f1 — **EditMode 21 + PlayMode 86 = 107 automated tests, all green**.

### Changed

- **BREAKING — Movement extracted to a companion package** `com.likeon.gas.movement`. `MovementSystemComponent`, `CharacterMovementSystemComponent`, the locomotion animation layer (`LocomotionAnimationDriver` / `LocomotionMath` / `LocomotionTypes`), `MovementDefinition` / `MovementSettings` / `MovementTags`, and the sample Animator Controller generator now live there. Namespace is unchanged (`Likeon.GAS`); install the companion package to keep using movement. Rationale: movement is a *consumer* of the GameplayTag state bus, not part of the ability system — keeping the GAS core focused.
- The playable demo now uses a minimal built-in `CharacterController` mover, so the core sample stays self-contained (no dependency on the movement package).

## [0.2.0] - 2026-06-29

Verified on Unity 6000.4.10f1 — **EditMode 21 + PlayMode 86 = 107 automated tests, all green**.

### Added

- **GameplayEffect stacking** — `StackingType` (None / AggregateByTarget / AggregateBySource), `StackLimitCount`, duration-refresh & period-reset policies, and an expiration policy (clear entire stack / remove a single stack & refresh / refresh only). Modifiers and periodic execution scale by stack count; `OnActiveEffectStackChanged` (effect, old, new) + `ActiveGameplayEffect.StackCount` drive ×N UI badges.
- **Ability grant/removal observability** — `AbilitySystemComponent.OnAbilityGiven` / `OnAbilityRemoved` events (payload `GameplayAbilitySpec`, fired for loadout & global grants too; removal fires before the instance is destroyed) + read-only `GetGrantedAbilities()`, so a loadout-driven ability bar can auto-populate.

## [0.1.0] - 2026-06-29

First public release. Verified on Unity 6000.4.10f1 — **EditMode 21 + PlayMode 76 = 97 automated tests, all green** — with a playable demo. Single-player authoritative; stage-6 networking is the remaining roadmap item.

### Added

- **Core ability system** — GameplayTag, AttributeSet (`AS_Health`/`AS_Stamina`/`AS_Mana`/`AS_Combat`/`AS_Poise`), GameplayEffect, GameplayAbility, AbilitySystemComponent, `AbilityLoadout`, `AbilityInteractionRules`.
- **AbilityTask framework** — `WaitDelay`, `WaitDelayOneFrame`, `WaitGameplayEvent`, `WaitInputPress`, `WaitTargetData`, `PlayMontageAndWaitForEvent`; auto-cancelled on ability end.
- **Targeting** — `TargetActor` (line/sphere trace), `TargetData`/`TargetDataHandle`, `TargetSource`.
- **Input dispatch** — routing, gating, buffering, control-setup stack; state-driven key polymorphism; optional `Likeon.GAS.UnityInput` binder.
- **Melee combat** — `AttackDefinition`, `AbilityActionSet`, `MeleeAttackTrace`, `DamageExecutionCalculation`, `CombatTeamAgent`, `CombatSystemComponent`.
- **Poise** — `AS_Poise` + `PoiseComponent` (break, stagger, regen, recover).
- **Lock-on targeting** — `TargetingSystemComponent` (filtering, selection, cycling, auto-drop, events).
- **Projectiles** — `BulletDefinition` / `BulletInstance` / `BulletLauncher` (motion, spread, penetration, bullet chains, `Tick(dt)`).
- **Weapons** — `IWeapon` + `WeaponComponent` (equip, tag injection, active state, ranged fire).
- **Hit-reaction pipeline** — `CombatFlowComponent` + `AttackResultProcessor`s (death, gameplay events, cues).
- **`GlobalAbilitySystem`** — apply an ability/effect to all registered ASCs at once; late-registered ASCs auto-receive global items; optional `GlobalAbilitySystemRegistrant`.
- **`GamePhaseSubsystem` + `GamePhaseAbility`** — nested hierarchical-tag phases (parent/child coexist, siblings exclusive) with start/end observers.
- **`CollisionTrace`** — generic OverlapSphere hit detection with dedup, state toggle, and filtering (traps / AOE / hazards), distinct from `MeleeAttackTrace`.
- **`MovementCancellation`** — Animation-Event-driven window toggling `Animator.applyRootMotion` when the player moves.
- **Movement** — `MovementSystemComponent`, `CharacterMovementSystemComponent`, state mirrored to the ASC; data-driven locomotion layer (in-air state, view-relative aim offset, core-state tags → Animator) + sample layered Animator Controller generator (`Likeon ▸ GAS ▸ Samples`).
- **Observability** — change events for any UI/AI/save consumer to subscribe: `OnAttributeChanged`, `OnTagChanged`, `OnAbilityActivated/Ended`, `OnGameplayEvent`, **`OnActiveEffectAdded`/`OnActiveEffectRemoved`** + read-only `GetActiveGameplayEffects()` / `GetActiveGameplayEffect(handle)` (buff/debuff bars with remaining time), plus combat/poise/targeting/weapon events.
- **Presentation** — GameplayCue, surface effects (`SurfaceEffectComponent`/`SurfaceEffectLibrary`), camera blend stack.
- **Editor suite** — GameplayTag picker, tag registry & window, `[SerializeReference]` picker, asset inspectors, tag scanner — all under one top-level **Likeon** menu.
- **Playable demo** — `Samples~/PlayableDemo`, importable from the Package Manager.

### Fixed

- **GamePhase nesting** — corrected a literal condition that would wrongly end a parent phase when starting a child; parent & child now coexist per the documented intent.

### Known limitations

- No networking yet — single-player authoritative logic.
- **No in-package UI framework — by design.** Sigil broadcasts change events (attributes, tags, abilities, active effects, combat, etc.); bind any UI solution (UGUI / UI Toolkit / third-party) to them.
- Locomotion ships a data-driven layer + a sample Animator Controller generator, but no authored animation clips.
- The demo uses placeholder programmer art (capsules).

[Unreleased]: #unreleased
[0.3.0]: #030---2026-06-29
[0.2.0]: #020---2026-06-29
[0.1.0]: #010---2026-06-29
