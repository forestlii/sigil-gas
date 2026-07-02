# Changelog

[English](CHANGELOG.md) | [简体中文](CHANGELOG.zh-CN.md)

All notable changes to Sigil are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/).

## [Unreleased]

## [0.4.1] - 2026-07-02

### Fixed

- **`GameplayTagQuery` configured in the Inspector now actually evaluates.** Emptiness was tracked by a serialized `isEmpty` flag that only the code-side factory methods (`MakeQuery_*` / `All` / `Any`) ever cleared. A query authored in the Inspector (default constructor) kept `isEmpty = true` and was silently treated as "no condition → always pass", so its `tags` and nested `expressions` were ignored — affecting any tag query on a data asset, e.g. `InputProcessor.StateQuery` on an `InputControlSetup`. Emptiness is now derived from content at runtime (tag-type queries inspect `tags`, expression-type queries inspect `expressions`). Factory-constructed queries and existing assets are unaffected.

## [0.4.0] - 2026-07-01

### Added

- **Modular additional ability costs** (`AbilityCost`). Abilities can now carry a list of pluggable, non-attribute costs (ammo, charges, custom resources) beyond the single attribute-based `CostEffect`. Each `AbilityCost` is a `ScriptableObject` with `CheckCost` / `ApplyCost` and an `OnlyApplyCostOnHit` flag; activation requires *all* costs to be affordable, `CommitAbility` pays the non-on-hit ones, and `GameplayAbility.ApplyOnHitCosts()` pays the on-hit ones once a hit is confirmed. Costs are cloned per granted ability instance (mirrors UE `Instanced` costs), so charge state never leaks between characters.
- **Per-frame ability tick** (`GameplayAbility.AbilityTick(float)` gated by `EnableTick`). The ASC drives `AbilityTick` each frame for active abilities that opt in — an alternative to coroutine `AbilityTask`s for charge/scan loops.
- **Activate-on-granted (passive / aura abilities)** — `GameplayAbility.ActivateOnGranted`. When set, the ability tries to activate the moment it's granted (mirrors UE `TryActivateAbilityOnSpawn`), still subject to `CanActivate`. Works through any grant path (`GiveAbility` / `GrantLoadout` / `GlobalAbilitySystem`).
- **Ability action library** (`AbilityActionLibrary`) — a ScriptableObject that bundles `AbilityActionSet`s by ability tag and picks the right attack action for a tag + source/target state (`SelectBestAbilityActions`), mirroring UE `GCS_AbilityActionSetSettings`. Wired into `CombatSystemComponent` via `ActionLibrary` / `QueryAbilityActions(...)` / `PlayAbilityActionByTag(...)`. (Previously the `AbilityActionSet` data shell existed but had no container and no consumer.)
- **Combat settings** (`CombatSettings`) — a ScriptableObject with a static `Active` accessor, mirroring UE `GCS_CombatSystemSettings`: `MainMeshLookupTag` and `DisableAffiliationCheck` (a debug toggle that lets attacks ignore team affiliation; wired into `CombatTeamAgent.IsHostile`, default off).
- **Combat interface** (`ICombatInterface`) — the contract a combat-capable character implements (combat target, `QueryAbilityActions`, current weapon, block input, rotation/movement-set/movement-state tags, death lifecycle, movement input direction), mirroring UE `GCS_CombatInterface` with Unity types. Helper `CombatInterface.Get(go)` resolves it from a GameObject hierarchy.

- **Dynamic tags on grant** — `AbilityLoadout.GrantedAbility.DynamicTags` and a `GiveAbility(template, level, dynamicTags)` overload append tags to the granted ability instance (template untouched), mirroring UE `FGGA_AbilitySet_GameplayAbility.DynamicTags` (e.g. tag a grant with `Slot.Primary`).
- **`AbilityTask.ExternalConfirm(endTask)`** — mirrors UE `UAbilityTask::ExternalConfirm`; base ends the task when asked, `AbilityTask_WaitTargetData` overrides it to confirm targeting.
- **`Custom` targeting confirmation** — added to `EGameplayTargetingConfirmation` (waits for external/custom confirmation, like UserConfirmed but driven by custom logic).
- **Component-level `OnPostGameplayEffectExecute`** — `AbilitySystemComponent` now broadcasts after any gameplay-effect execute (mirrors UE `GGA_AttributeSystemComponent`), so damage-stat / AI-perception listeners don't have to override per-AttributeSet hooks.
- **Weapon source object, targeting toggle, and multi-segment traces** (`IWeapon` / `WeaponComponent`). A weapon can now carry a `SourceObject` (the equipment instance / data asset it's backed by — for equipment, loot, or data-table systems to trace a weapon back to its source), expose a weapon-level targeting toggle (`SetTargeting` / `ToggleTargeting` / `IsTargeting` + `OnTargetingChanged`, distinct from the lock-on system), and drive several simultaneous collision traces through `AdditionalTraces` (+ `RefreshTraceInstances()`) instead of just one trace + index — for dual-blade or main/secondary hit segments. The existing single `MeleeTrace` keeps working as the primary segment.

### Changed

- **BREAKING — activation-group rename.** The enum `EAbilityActivationPolicy` → `EAbilityActivationGroup` and its values `Parallel` / `Replaceable` / `Blocking` → `Independent` / `ExclusiveReplaceable` / `ExclusiveBlocking`; the field `GameplayAbility.ActivationPolicy` → `ActivationGroup`; ASC methods `IsActivationPolicyBlocked` / `RegisterAbilityPolicy` / `UnregisterAbilityPolicy` / `CancelAbilitiesWithPolicy` → `…ActivationGroup`. This aligns the *naming* with the underlying concept (the methods were already called `ChangeActivationGroup`). Existing ability assets keep their values via `[FormerlySerializedAs]` and the enum's int order is unchanged.
- **BREAKING — typed attribute sets in `AbilityLoadout`.** `GrantedAttributeSetTypes` (`List<string>` of type names, resolved by reflection) is replaced by `[SerializeReference] List<AttributeSet> GrantedAttributeSets` — pick the concrete set in the Inspector; granting creates a fresh instance per ASC by the chosen type. No more silent breakage on class rename and no type-name strings. Existing loadout assets that used the string list must re-select their attribute sets.
- **BREAKING — `OnAttributeChanged` now carries the source.** The event signature changed from `Action<GameplayAttribute, float, float>` to `Action<AttributeChangeData>`, where `AttributeChangeData` carries `Attribute` / `OldValue` / `NewValue` / `Source` (a `GameplayEffectContext` — who/what caused the change; null when there is no single source, e.g. removal or inhibition). Damage/healing paths thread the attacker's context through, so a floating-damage-number UI can now tell *who hit whom*.

### Fixed

- **Attribute sets in `AbilityLoadout` now persist to disk.** `AttributeSet` and its subclasses lacked `[Serializable]`, so the `[SerializeReference] List<AttributeSet> GrantedAttributeSets` (above) was silently dropped whenever a loadout was saved as an asset or baked into a prefab — the in-memory API worked but the actual designer workflow (assign in the Inspector, save, instantiate) lost every attribute set. Marking `AttributeSet` / `AS_Health` / `AS_Stamina` / `AS_Poise` / `AS_Mana` / `AS_Combat` `[Serializable]` makes the chosen types serialize; `GrantLoadout` then recreates a fresh set per ASC by type. Guarded by a new EditMode test.
- **Attack type tags now reach the damage effect (dead-config fixed).** `AttackDefinition.AttackTags` (Melee/Ranged, Slash/Strike, etc.) were documented as "added as dynamic asset tags to the gameplay effect spec" but were never actually injected, so a target could not react based on *what kind of attack* hit it. Added `GameplayEffectSpec.DynamicAssetTags` (+ `AddDynamicAssetTags` / `GetAllAssetTags`); both attack-application paths (`AttackApplication` and `MeleeAttackTrace`, plus effect containers) now inject `AttackTags` into the spec, the attack tags are folded into `AttackResult.AggregatedSourceTags` (so hit-reaction processors can query attack type, e.g. heavy → stagger), and `RemoveEffectsWithTags` now honors dynamic asset tags.

- **`AbilityInteractionRules.AbilityTagsToBlock` is now enforced.** Previously the field was collected but never read, so "block other abilities from activating while this one is active" had no effect (only `AbilityTagsToCancel` worked). An active ability now contributes its block tags to a reference-counted set on the ASC; any ability whose `AbilityTags` match is denied activation until every blocking source ends. New API: `AbilitySystemComponent.AreAbilityTagsBlocked(...)` and `AbilityInteractionRules.AddBaseRule(...)` / `AddConditionalRules(...)` for building rules in code. Covered by 6 new PlayMode tests (`AbilityBlockTagsPlayTests`). Test totals: **EditMode 21 + PlayMode 95 = 116**.

### Changed

- **Playable Demo upgraded to a feature showcase**. The bundled sample now puts several combat lines in one runtime-built scene — melee, **ranged projectiles**, **lock-on switching between 3 enemies**, **poise/stagger**, and **stacking buffs** — with a self-explanatory on-screen HUD rendered purely from the framework's observability events. No framework code changed; the demo simply exercises features that were already implemented and tested. New demo scripts: `DemoRanged` / `DemoRangedAbility` / `DemoHUD`.
- **Playable Demo ships as a prefab + scene** (designer workflow), not just runtime `AddComponent`. An editor generator (`DemoPrefabBuilder`, menu *Likeon ▸ GAS ▸ Demo ▸ Build All*) bakes `DemoPlayer` / `DemoEnemy` prefabs (under `Resources/`) and a wired `GASDemo.unity` scene; player/enemy attributes + abilities come from `PlayerLoadout` / `EnemyLoadout` assets via `AbilitySystemComponent.initialLoadouts` (no code `AddAttributeSet` / `GiveAbility`). Player/enemy construction is shared between the prefab generator and the runtime fallback through `DemoActorBuilder`. `GASDemo` is now thin orchestration: in prefab mode it only wires the cross-boundary bits a prefab can't hold (camera `ViewSource` / third-person camera / HUD) plus dynamic event subscriptions; with no scene instances it falls back to building everything at runtime (so the demo still "just runs" when added to an empty GameObject, and the headless smoke tests keep working). Reference fields on `DemoPlayerController` / `DemoRanged` are now visible in the Inspector. New smoke tests `M` / `N` / `O` cover prefab instantiation and the adopt path.
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
