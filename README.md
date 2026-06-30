# Sigil — Action Combat & Ability Framework for Unity (GAS-style)

[English](README.md) | [简体中文](README.zh-CN.md)

A GAS-style **action-combat and ability framework** for Unity. A **GameplayTag-driven
state bus** ties together abilities, attributes, effects, input dispatch, melee & ranged
combat, and presentation — so input → ability → effect → attribute → feedback stays
decoupled and data-driven. (Movement lives in an optional companion package; see below.)

- **Engine:** Unity 6 — developed & verified on 6000.4.10f1
- **Tests:** EditMode 21 + PlayMode 86 = **107 automated tests, all green**
- **Scope:** single-player authoritative logic (no networking yet)
- **Publisher:** Likeon · namespace `Likeon.GAS`

> "GAS-style" describes the architecture (a GameplayTag state bus with abilities, attributes
> and effects). This is an independent Unity implementation; no third-party engine code is included.

## Install

Copy the `com.likeon.gas` folder into your project's `Packages/` directory, or use
**Package Manager → Add package from disk…** and select `package.json`.
The only dependency is `com.unity.inputsystem`.

### Running tests

The package ships with EditMode + PlayMode tests under `Tests/`. To run them, add the
package to `"testables"` in your project's `Packages/manifest.json`, then open
**Window → General → Test Runner**:

```json
"testables": [ "com.likeon.gas" ]
```

## Features

### Core ability system
- **GameplayTag** — hierarchical tags, containers, ref-counted loose tags, tag queries.
- **AttributeSet** — attributes with Pre/Post change hooks; built-in `AS_Health`, `AS_Stamina`, `AS_Mana`, `AS_Combat`, `AS_Poise`.
- **GameplayEffect** — Instant / Duration / Infinite, periodic, modifiers, custom execution calculations, granted tags, application conditions, SetByCaller, **stacking** (aggregate by source/target, stack limit, duration-refresh / period-reset / expiration policies, magnitude scales by stack count).
- **GameplayAbility** — activation group (Independent / ExclusiveReplaceable / ExclusiveBlocking), cost & cooldown (incl. modular `AbilityCost`), optional per-frame `AbilityTick`, activation-owned tags, effect containers.
- **AbilitySystemComponent** — the hub: owned tags, attribute sets, active effects, ability granting/activation, exclusivity, interaction rules.
- **AbilityLoadout** — batch-grant abilities + effects + attribute sets; revoke as one handle.
- **AbilityInteractionRules** — state-aware block / cancel / activation rules driven by the character's current tags.
- **AbilityTask framework** — coroutine-based async tasks: `WaitDelay`, `WaitDelayOneFrame`, `WaitGameplayEvent`, `WaitInputPress`, `WaitTargetData`, `PlayMontageAndWaitForEvent` (play an Animator state while listening for gameplay events); auto-cancelled on ability end.
- **Targeting** — `TargetActor` (line / sphere trace) producing `TargetData`, plus `TargetSource` (self / event-data).
- **GlobalAbilitySystem** — apply an ability/effect to every registered ASC at once (arena-wide buffs/debuffs, phase abilities); late-registered ASCs auto-receive global items. Optional `GlobalAbilitySystemRegistrant` for auto register.
- **GamePhaseSubsystem** — nested hierarchical-tag game phases (`GamePhaseAbility`): parent & child phases coexist, siblings are exclusive; start/end observers (exact / partial match).

### Input dispatch
- **InputSystemComponent** — back-end-agnostic routing, gating, buffering, push/pop control-setup stack.
- **InputChecker / InputProcessor / InputControlSetup** (FirstOnly / MatchAll).
- **State-driven key polymorphism** — multiple processors on one input tag; `FirstOnly` picks the first whose state query passes (e.g. *sprint → slide, else crouch* on one key).
- Input buffer windows for combos; optional `Likeon.GAS.UnityInput` binder.

### Combat
- **Melee** — `AttackDefinition`, `MeleeAttackTrace` (animation-event hit windows + socket sphere-cast), `DamageExecutionCalculation`, `CombatTeamAgent`, `CombatSystemComponent`.
- **Combo selection** — `AbilityActionSet` picks actions by source/target state (layered tag queries).
- **Poise / stagger** — `AS_Poise` + `PoiseComponent`: break, stagger, regen, recover.
- **Lock-on** — `TargetingSystemComponent`: overlap collection → affiliation/dead/tag/view-cone/line-of-sight filtering → best/closest selection, left/right cycling, auto-drop, lock events.
- **Projectiles** — `BulletDefinition` / `BulletInstance` / `BulletLauncher`: self-integrated motion, spread, swept-sphere hits, character/map penetration, bullet chains; `Tick(dt)` decouples flight from Unity time.
- **Weapons** — `IWeapon` + `WeaponComponent`: equip/unequip, weapon-tag injection for ability gating, active-state toggling, a `SourceObject` (the equipment instance / data asset a weapon is backed by), a weapon-level targeting toggle (`SetTargeting` / `ToggleTargeting`, distinct from lock-on), multiple simultaneous collision-trace segments (`AdditionalTraces`), and ranged `FireProjectile`.
- **Hit-reaction pipeline** — `CombatFlowComponent` + `AttackResultProcessor`s (death, tag-filtered gameplay events, gameplay cues).
- **CollisionTrace** — generic `OverlapSphere` hit detection with per-activation dedup, on/off state, and filtering — for traps, AOE zones, environmental hazards (distinct from `MeleeAttackTrace`).
- **MovementCancellation** — Animation-Event-driven window that toggles `Animator.applyRootMotion` when the player moves, so attack root-motion can be cancelled by movement.

### Movement — companion package
Movement & locomotion live in a **separate companion package**, [`com.likeon.gas.movement`](https://github.com/forestlii/sigil-movement)
(GameplayTag-driven movement state machine on a `CharacterController` + data-driven locomotion
animation layer + sample Animator Controller generator). Kept out of the core on purpose:
movement is a *consumer* of the state bus, not the ability system — so you can pair the GAS
core with your own movement, or with this package.

### Presentation
- **GameplayCue** — tag-driven VFX/SFX, routed by tag hierarchy via `GameplayCueManager`.
- **Surface effects** — `SurfaceEffectComponent` resolves a surface from a hit and plays audio & particles from a `SurfaceEffectLibrary`.
- **Camera blend stack** — third-person behaviors blended by an AnimationCurve-driven weight, with SphereCast collision pull-in.

### Observability — binding your own UI
Sigil is **logic-only and UI-agnostic**: it broadcasts change events and lets *any* UI
solution (UGUI / UI Toolkit / third-party) subscribe and render. Drawing the HUD is the
host project's job — Sigil's job is to expose the data. Key events:

- `AbilitySystemComponent`: `OnAttributeChanged` (health/mana/stamina bars), `OnTagChanged` (status icons), `OnAbilityActivated` / `OnAbilityEnded`, **`OnAbilityGiven` / `OnAbilityRemoved`** + read-only `GetGrantedAbilities()` for a loadout-driven ability bar, `OnGameplayEvent`, **`OnActiveEffectAdded` / `OnActiveEffectRemoved` / `OnActiveEffectStackChanged`** + read-only `GetActiveGameplayEffects()` / `GetActiveGameplayEffect(handle)` for buff/debuff bars with remaining time and ×N stack badges, and `GetCooldownRemainingForTags(...)` for cooldown fills.
- `CombatSystemComponent`: `OnDealtDamage` / `OnAttackResultReceived` (damage numbers).
- `PoiseComponent`: `OnPoiseBroken` / `OnPoiseRecovered`. `TargetingSystemComponent`: `OnTargetLockOn` / `OnTargetLockOff`. `WeaponComponent`: `OnEquipped` / `OnUnequipped` / `OnWeaponActiveStateChanged` / `OnTargetingChanged`.

### Editor tools
- GameplayTag picker (hierarchical dropdown + search + add), tag registry & a `Likeon ▸ GAS ▸ Gameplay Tags` window, `[SerializeReference]` subclass pickers, asset inspectors, a project tag scanner — all under one top-level **Likeon** menu.

### Playable demo
Import via **Package Manager → Sigil → Samples → *Playable Demo***, open `GASDemo.unity`, press Play.
A **feature showcase** shipped as **player/enemy prefabs + a wired scene** (`DemoPlayer` / `DemoEnemy`
under `Resources/`, with attributes & abilities supplied by data-driven `AbilityLoadout` assets via
`initialLoadouts`); `GASDemo` is thin orchestration (camera / HUD / dynamic feedback) and falls back to
building everything at runtime if dropped on an empty GameObject. Re-bake it from *Likeon ▸ GAS ▸ Demo ▸
Build All*. It puts several combat lines in one scene (placeholder programmer art):
**melee → damage → cue, ranged projectiles, lock-on switching between 3 enemies, poise/stagger, and
stacking buffs**, with a self-explanatory on-screen HUD that renders purely from the framework's
observability events. Controls: WASD move · Shift sprint · mouse look · Space/LMB melee · RMB/F ranged ·
Tab lock-on · Q/E switch target · R stack a buff.

## Configuration (data-driven)

Sigil is **data-driven**: you configure behaviour by authoring **ScriptableObject assets** in the Inspector — no code — so designers can tune it. The main config assets (all under *Create → Likeon → GAS → …*):

- **Input dispatch & mutual exclusion** — `InputControlSetup`: a list of `InputProcessor`s mapping an `InputTag` to an ability, with `ExecutionType = FirstOnly` for "one key, one ability" polymorphism (the first processor whose `StateQuery` passes wins). Bind physical keys → `InputTag`s in the `InputConfig` asset (`InputActionMappings`); `InputSystemComponent` auto-binds them. Push/pop setups to swap whole schemes (vehicle / UI).
- **Ability mutual exclusion** — `AbilityInteractionRules`: data-driven block / cancel / activation-gating between abilities (state-aware via tag queries). Plus each `GameplayAbility` has its own `ActivationGroup` / `ActivationRequiredTags` / `ActivationBlockedTags`.
- **Abilities / effects / attacks** — `GameplayAbility`, `GameplayEffect` (incl. stacking), `AttackDefinition`, `BulletDefinition`, `AbilityLoadout`.

→ Step-by-step configuration (incl. "one key → different ability per weapon") is in the [usage guide](Documentation~/Usage.md) §9 (input) and §7.5 (ability rules).
→ **A complete worked example ships with the Playable Demo**: import it and open `DemoConfig.asset` to inspect/copy fully-wired input setups, interaction rules, abilities and effects.

## Quick start

```csharp
using Likeon.GAS;
using UnityEngine;

// Author an ability: derive from GameplayAbility, override OnActivateAbility.
[CreateAssetMenu(menuName = "Game/Abilities/Slide")]
public class GA_Slide : GameplayAbility
{
    protected override void OnActivateAbility(GameplayEventData triggerData)
    {
        if (!CommitAbility()) { EndAbility(true); return; } // pay cost + cooldown

        var wait = AbilityTask_WaitDelay.WaitDelay(this, 0.3f);
        wait.OnFinish += () => EndAbility();
        wait.Activate();
    }
}
```

```csharp
// Set up an ability system on a character.
var asc = gameObject.AddComponent<AbilitySystemComponent>();
asc.AddAttributeSet(new AS_Health());
asc.AddAttributeSet(new AS_Stamina());

asc.GrantLoadout(defaultLoadout);                 // batch grant
var handle = asc.GiveAbility(slideAbility);        // single grant

asc.OnAttributeChanged += d => Debug.Log($"{d.Attribute} : {d.OldValue} -> {d.NewValue} (by {d.Source?.SourceASC})");

asc.AddLooseGameplayTag(GameplayTag.RequestTag("Movement.State.Sprint"));
asc.TryActivateAbility(handle);
```

```csharp
// Apply a damage effect (Instant GE → AS_Health.IncomingDamage; value via SetByCaller).
var spec = targetASC.MakeOutgoingSpec(damageEffect);
spec.SetSetByCallerMagnitude(GameplayTag.RequestTag("Data.Damage"), 25f);
targetASC.ApplyGameplayEffectSpecToSelf(spec);
```

### State-driven key polymorphism (one key = crouch or slide)

```csharp
// In an InputControlSetup asset, add two processors on the same InputTag, ExecutionType = FirstOnly:
//
//  [0] InputProcessor_ActivateAbilityByTag  (slide, first)
//        InputTags  = InputTag.Crouch
//        StateQuery = MatchAllTags(Movement.State.Sprint)   // only while sprinting
//        AbilityTag = Ability.Slide
//  [1] InputProcessor_ActivateAbilityByTag  (crouch, second)
//        InputTags  = InputTag.Crouch
//        StateQuery = (empty)                               // unconditional
//        AbilityTag = Ability.Crouch
//
// Sprinting → [0] passes → slide, FirstOnly returns. Otherwise → [1] → crouch.

inputSystemComponent.ReceiveInput(
    GameplayTag.RequestTag("InputTag.Crouch"),
    InputTriggerEvent.Started,
    InputActionData.Empty);
```

## Status & roadmap

Single-player core is **complete and tested** (107 automated tests, run together with the
movement companion package) — covering the ability system, input dispatch, melee & ranged
combat, game phases, global abilities, generic collision tracing, and presentation.

- **Movement / locomotion** — in the companion package [`com.likeon.gas.movement`](https://github.com/forestlii/sigil-movement).
- **UI** — **intentionally out of scope.** Sigil is logic-only; subscribe to its change events
  (see *Observability* above) from any UI solution.
- **Networking** (replication / prediction) — planned for a later stage.

## License

[MIT](LICENSE.md) — free for any use including commercial, just keep the copyright notice.
