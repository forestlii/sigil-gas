# Sigil — Action Combat & Ability Framework for Unity (GAS-style)

[English](README.md) | [简体中文](README.zh-CN.md)

A GAS-style **ability-system framework** for Unity. A **GameplayTag-driven
state bus** ties together abilities, attributes, effects, input dispatch and presentation —
so input → ability → effect → attribute → feedback stays decoupled and data-driven.
**Melee/ranged combat and movement live in optional companion packages** ([`com.likeon.gas.combat`](https://github.com/forestlii/sigil-combat)
and [`com.likeon.gas.movement`](https://github.com/forestlii/sigil-movement)); the core stays a pure ability system.

- **Engine:** Unity 6 — developed & verified on 6000.4.10f1
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
- **AttributeSet** — attributes with Pre/Post change hooks. No built-in `AS_*`: define your attribute sets in the Inspector and **generate** the C# with the attribute-set codegen tool (*Sigil ▸ GAS ▸ …*), or hand-write `AttributeSet` subclasses.
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

### Combat — companion package
Melee/ranged combat lives in a **separate companion package**, [`com.likeon.gas.combat`](https://github.com/forestlii/sigil-combat)
(attack definitions, melee/collision hit detection, combat flow pipeline, poise & poise-break,
lock-on targeting, weapons, bullets, and a damage execution calculation — all attribute-name based).
Kept out of the core on purpose: combat is a *domain* built on the ability system, not part of the
core mechanics — so you can pair the GAS core with your own combat, or with this package. It sits
**beside** the movement companion; the two are independent and don't depend on each other.

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
- GameplayTag picker (hierarchical dropdown + search + add), tag registry & a `Sigil ▸ GAS ▸ Gameplay Tags` window, `[SerializeReference]` subclass pickers, asset inspectors, a project tag scanner, and the **attribute-set / tag-constants codegen** tools — all under one top-level **Sigil** menu.

### Playable demo — in the combat companion
The flagship **Playable Demo** (melee → damage → cue, ranged projectiles, lock-on, poise/stagger,
stacking buffs) is combat-centric, so it now ships with the [`com.likeon.gas.combat`](https://github.com/forestlii/sigil-combat)
companion package (import it there and open `PlayableDemo.unity`). The core package stays demo-free.

## Configuration (data-driven)

Sigil is **data-driven**: you configure behaviour by authoring **ScriptableObject assets** in the Inspector — no code — so designers can tune it. The main config assets (all under *Create → Sigil → GAS → …*):

- **Input dispatch & mutual exclusion** — `InputControlSetup`: a list of `InputProcessor`s mapping an `InputTag` to an ability, with `ExecutionType = FirstOnly` for "one key, one ability" polymorphism (the first processor whose `StateQuery` passes wins). Bind physical keys → `InputTag`s in the `InputConfig` asset (`InputActionMappings`); `InputSystemComponent` auto-binds them. Push/pop setups to swap whole schemes (vehicle / UI).
- **Ability mutual exclusion** — `AbilityInteractionRules`: data-driven block / cancel / activation-gating between abilities (state-aware via tag queries). Plus each `GameplayAbility` has its own `ActivationGroup` / `ActivationRequiredTags` / `ActivationBlockedTags`.
- **Abilities / effects** — `GameplayAbility`, `GameplayEffect` (incl. stacking), `AbilityLoadout`. (Combat assets like `AttackDefinition` / `BulletDefinition` live in the combat companion package.)

→ Step-by-step configuration (incl. "one key → different ability per weapon") is in the [usage guide](Documentation~/Usage.md) §9 (input) and §7.5 (ability rules).
→ **A complete worked example ships with the Playable Demo** (in the [combat companion](https://github.com/forestlii/sigil-combat)): import it and open `DemoConfig.asset` to inspect/copy fully-wired input setups, interaction rules, abilities and effects.

### Editor cheat sheet

Authored **in the Editor, no code** — *Create → Sigil → GAS → …*: **Gameplay Effect**, **Ability Loadout**, **Ability Interaction Rules**, **Input Config**, **Input Control Setup**, **Curve Table**, **Gameplay Tags Settings**, **Attribute Set Definition** (→ codegen), **Gameplay Cue Notify (Static)**, **Surface Effect Library**, **Target Source**, **Game Phase Ability**. Code-backed types (`GameplayAbility` / `GameplayCueNotify` / `GameplayEffectExecutionCalculation`) get one-click empty-subclass templates under *Assets → Create → Sigil*. Tools under *Sigil ▸ GAS*: **Gameplay Tags** registry, **GAS Debugger**, **Generate Gameplay Tag Constants**, **Scan Project for Gameplay Tags**. Attribute sets are generated from an **Attribute Set Definition** asset (no C++/C# needed). Combat / movement assets ship with their companion packages.

→ Full table (with descriptions) in the [usage guide](Documentation~/Usage.md) §21.

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
// AS_Health / AS_Stamina here are YOUR attribute sets (generated by the codegen tool or
// hand-written) — the core ships none built in.
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

Single-player core is **complete and tested** (EditMode + PlayMode suites all green, run together
with the combat & movement companion packages) — covering the ability system, input dispatch,
game phases, global abilities, and presentation.

- **Combat** — in the companion package [`com.likeon.gas.combat`](https://github.com/forestlii/sigil-combat).
- **Movement / locomotion** — in the companion package [`com.likeon.gas.movement`](https://github.com/forestlii/sigil-movement).
- **UI** — **intentionally out of scope.** Sigil is logic-only; subscribe to its change events
  (see *Observability* above) from any UI solution.
- **Networking** (replication / prediction) — planned for a later stage.

## License

[MIT](LICENSE.md) — free for any use including commercial, just keep the copyright notice.
