# Sigil — Usage Guide

[English](Usage.md) | [简体中文](Usage.zh-CN.md)

**Sigil — Action Combat & Ability Framework for Unity (GAS-style)**　by Likeon

A GAS-style action-combat and ability framework for Unity, built around a GameplayTag-driven
state-bus architecture. The **GameplayTag state bus** decouples and connects
*input → ability → effect → attribute → combat → movement → presentation*.

> This guide is organized as "how to do X". Every code sample maps to a real framework API. The namespace is `Likeon.GAS` throughout.

---

## Contents

1. [Install](#1-install)
2. [Core concepts at a glance](#2-core-concepts-at-a-glance)
3. [5-minute quick start](#3-5-minute-quick-start)
4. [GameplayTags](#4-gameplaytags)
5. [Attributes](#5-attributes)
6. [Gameplay Effects](#6-gameplay-effects)
7. [Abilities](#7-abilities)
8. [AbilitySystemComponent cheat sheet](#8-abilitysystemcomponent-cheat-sheet)
9. [Input dispatch (state-driven key polymorphism)](#9-input-dispatch-state-driven-key-polymorphism)
10. [Melee combat](#10-melee-combat)
11. [Movement / Locomotion (companion package)](#11-movement--locomotion-companion-package)
12. [Presentation (Cue / context effects / camera)](#12-presentation-cue--context-effects--camera)
13. [Editor tools](#13-editor-tools)
14. [Networking](#14-networking)
15. [FAQ](#15-faq)
16. [Advanced systems](#16-advanced-systems)
17. [Binding UI (subscribe to events)](#17-binding-ui-subscribe-to-events)
18. [Common recipes](#18-common-recipes)
19. [Design trade-offs & anti-patterns](#19-design-trade-offs--anti-patterns)
20. [Coming from Unreal GAS?](#20-coming-from-unreal-gas)

---

## 1. Install

**Option A: local package**
Copy the `com.likeon.gas` folder into your project's `Packages/` directory, or Package Manager → `+` → *Add package from disk* → select `package.json`.

**Option B: Git URL**
Package Manager → `+` → *Add package from git URL* → enter the repository address.

**Dependency**: the package declares a dependency on the **Input System** (`com.unity.inputsystem`), installed automatically with Sigil. The core uses it for input binding (see §9.1); the Playable Demo uses it for keyboard/mouse.
> The `Likeon.GAS.Runtime` assembly references the Input System: `InputConfig` maps each `InputTag` to an `InputActionReference`, and `InputSystemComponent` binds those actions automatically when enabled (dispatching to `ReceiveInput`). If you don't use the Input System, you can still drive `ReceiveInput(tag, event, data)` manually from any source.
> ⚠️ To run the Demo you also need **Project Settings ▸ Player ▸ Active Input Handling** set to *Input System Package* (or *Both*), otherwise the new input system can't read the keyboard/mouse.

**Assemblies**: `Likeon.GAS.Runtime` (core), `Likeon.GAS.Editor` (editor tools).

**Companion package (optional)**: movement and locomotion live in the separate package `com.likeon.gas.movement` ([sigil-movement](https://github.com/forestlii/sigil-movement)), which depends on the core and keeps the same namespace. Install it alongside the core if you need movement (see §11).

---

## 2. Core concepts at a glance

```
[input] --InputTag--> [InputSystemComponent] --gating/polymorphic dispatch--> activate
                                                                    │
                                                                    ▼
[GameplayAbility] --(changes attributes via)--> [GameplayEffect] --> [AttributeSet]
       │                                                       ▲
       ├-- hit detection [MeleeAttackTrace] --> [AttackDefinition] -┘
       ├-- drives [MovementSystemComponent] (state written back to ASC tags = the state bus)
       └-- triggers presentation [GameplayCue] / [ContextEffect] / camera mode stack

Running through everything: [GameplayTag] — a character's "current state" is the set of tags the ASC holds
```

Remember one sentence: **almost all "branch by state" logic is querying GameplayTags on the ASC.**

---

## 3. 5-minute quick start

Goal: a character with health that can activate an ability and loses health when damaged.

### 3.1 Add an ASC + attributes to a character

```csharp
using Likeon.GAS;
using UnityEngine;

public class Character : MonoBehaviour
{
    public AbilitySystemComponent ASC { get; private set; }

    void Awake()
    {
        ASC = gameObject.AddComponent<AbilitySystemComponent>();
        ASC.AddAttributeSet(new AS_Health());   // built-in: Health / MaxHealth / IncomingDamage / IncomingHealing
        ASC.AddAttributeSet(new AS_Stamina());

        // Listen for health changes (drives a health-bar UI)
        ASC.OnAttributeChanged += d =>
            Debug.Log($"{d.Attribute} : {d.OldValue} -> {d.NewValue} (by {d.Source?.SourceASC})");
    }
}
```

### 3.2 Write an ability

An ability is a subclass of `GameplayAbility` (a `ScriptableObject`); override `OnActivateAbility`:

```csharp
using Likeon.GAS;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Abilities/Heal")]
public class GA_Heal : GameplayAbility
{
    protected override void OnActivateAbility(GameplayEventData triggerData)
    {
        if (!CommitAbility()) { EndAbility(true); return; } // pay cost + start cooldown (if a CostEffect/CooldownEffect is set)
        Debug.Log("Heal!");
        // …apply a healing effect, play an animation, etc. here…
        EndAbility();
    }
}
```

In the Project, *Create → Game/Abilities/Heal* makes an asset; set its **Ability Tags** (e.g. `Ability.Heal`).

### 3.3 Grant and activate

```csharp
public GA_Heal HealAbility; // drag the asset in

void Start()
{
    var handle = ASC.GiveAbility(HealAbility);   // grant → returns a handle
    ASC.TryActivateAbility(handle);              // activate by handle
    // or: ASC.TryActivateAbilitiesByTag(GameplayTag.RequestTag("Ability.Heal"));
}
```

### 3.4 Deal damage

Damage goes through a **GameplayEffect**. Create a damage GE asset (see [section 6](#6-gameplay-effects)), then:

```csharp
var spec = targetASC.MakeOutgoingSpec(damageEffect)
    .SetSetByCallerMagnitude(GameplayTag.RequestTag("Data.Damage"), 25f);
targetASC.ApplyGameplayEffectSpecToSelf(spec);
// AS_Health maps IncomingDamage to -Health, and OnAttributeChanged fires
```

---

## 4. GameplayTags

Hierarchical dot-separated strings; a child tag matches its parent.

```csharp
var sprint = GameplayTag.RequestTag("Movement.State.Sprint");
sprint.MatchesTag(GameplayTag.RequestTag("Movement.State")); // true (child matches parent)
sprint.MatchesTagExact(...);                                  // exact comparison

// Containers
var c = new GameplayTagContainer();
c.AddTag(sprint);
c.HasTag(GameplayTag.RequestTag("Movement.State")); // true (hierarchical)
c.HasAny(otherContainer); c.HasAll(otherContainer);

// Queries (for "branch by state")
var q = GameplayTagQuery.MakeQuery_MatchAllTags(sprint);
q.Matches(c); // true
// Also MakeQuery_MatchAnyTags / MakeQuery_MatchNoTags / All(...) / Any(...)
```

**"Owned tags" on the ASC** (the character's current state):

```csharp
asc.AddLooseGameplayTag(GameplayTag.RequestTag("State.Stunned"));
asc.HasMatchingGameplayTag(GameplayTag.RequestTag("State"));    // true (hierarchical)
asc.RemoveLooseGameplayTag(GameplayTag.RequestTag("State.Stunned"));
```
Loose tags are ref-counted: when multiple sources add the same tag, one source removing it won't wrongly drop it.

> Don't hand-type strings in the editor — use the [tag picker](#13-editor-tools).

---

## 5. Attributes

Built-in attribute sets: `AS_Health`, `AS_Stamina`, `AS_Mana`, `AS_Combat`. Each attribute is a `GameplayAttributeData` (distinguishing `BaseValue`, the permanent value, from `CurrentValue`, which includes temporary buffs).

```csharp
var health = asc.GetAttributeSet<AS_Health>();
float hp = health.Health.CurrentValue;
GameplayAttribute hpAttr = health.HealthAttribute; // an attribute handle, used by GE modifiers
```

**The damage Meta pipeline**: don't change `Health` directly. Damage is applied to the intermediate attribute `IncomingDamage`; after the calculation `AS_Health` zeroes it out and maps it to `-Health` (auto-clamped to `[0, MaxHealth]`). Healing works the same way via `IncomingHealing`.

> 📌 **How to initialize attributes / configure equipment bonuses**: simple defaults go in the field constructor (e.g. `new GameplayAttributeData(100f)`); data-driven initialization goes through `AbilityLoadout.GrantedEffects` with a **persistent (Infinite) GE**, leveled by the ASC's `attributeInitializeLevel` (curve-table magnitudes look up that level). **Bonuses that will change later — equipment, talents — must be Infinite GEs, not Instant**; see [§19.1](#19-design-trade-offs--anti-patterns) for why.

**Custom attribute set**: derive from `AttributeSet` and register fields in `RegisterAttributes()`:

```csharp
public sealed class AS_Shield : AttributeSet
{
    public readonly GameplayAttributeData Shield = new GameplayAttributeData(0f);
    public GameplayAttribute ShieldAttribute => GetAttribute(nameof(Shield));

    protected override void RegisterAttributes() => Register(nameof(Shield), Shield);

    public override void PreAttributeChange(GameplayAttribute attribute, ref float newValue)
    {
        if (attribute == ShieldAttribute) newValue = Mathf.Max(0f, newValue); // clamp
    }
}
```

### 5.1 Authoring attribute sets in the editor (no hand-written C#)

Unreal's GAS forces attributes into C++. Sigil lets you define them in the editor and generate the C# for you — so a designer can add attributes without a programmer, while code still gets a real, compile-time-safe type.

1. *Create → Sigil → GAS → **Attribute Set Definition***. On the asset, set the class name + namespace and list your attributes: name, default value, optional clamp (min, and/or "clamp to this attribute" e.g. `Health` → `MaxHealth`), and a Meta flag.
2. Click **Generate C#**. This writes two files next to the asset:
   - `<ClassName>.g.cs` — the generated `AttributeSet` subclass (fields, `RegisterAttributes`, typed `…Attribute` handles, clamps in `PreAttributeChange`). **Never edit this** — regenerate from the asset instead.
   - `<ClassName>.cs` — a hand-written partial, generated **once** and never overwritten. Put custom logic here: extra clamps via the `partial void OnPreAttributeChange(…)` hook the generated code calls, and the damage Meta pipeline by overriding `PostGameplayEffectExecute`.

The definition asset is the single source of truth; generation is one-way (asset → C#). The result is a normal type — reference it from code with `From<AS_PlayerStats>("Health")`, add it to `AbilityLoadout.GrantedAttributeSets`, everything works as with a hand-written set. Change an attribute's **values / clamp range** freely; only **adding or removing** attributes needs a regenerate + recompile.

---

## 6. Gameplay Effects

`GameplayEffect` is a ScriptableObject: *Create → Sigil → GAS → Gameplay Effect*.

**Key fields**:
- **DurationType**: `Instant` (changes the base value, e.g. one hit of damage) / `HasDuration` (changes the current value for a time) / `Infinite` (lasts until manually removed)
- **Period**: if >0, it ticks periodically (e.g. poison per second)
- **Modifiers**: each one = a target attribute + an operator (Add/Multiply/Divide/Override) + a magnitude
  - Magnitude source: `ScalableFloat` (base + per-level increment) or `SetByCaller` (passed in at runtime by tag)
- **Executions**: complex calculations (see `GameplayEffectExecutionCalculation`, e.g. damage with mitigation)
- **GrantedTags**: tags placed on the target while it lasts (Duration/Infinite only)
- **ApplicationRequiredTags / BlockedTags**: application conditions
- **OngoingRequiredTags**: must hold while active, otherwise the effect is inhibited
- **GameplayCues**: presentation cue tags triggered on application
- **Stacking** (Duration/Infinite only):
  - **StackingType**: `None` (each application is its own independent instance) / `AggregateByTarget` (merged per target into one instance with a stack count) / `AggregateBySource` (merged per source)
  - **StackLimitCount**: the stack ceiling (>0 to enable, 0 = unlimited); past the cap, re-applying only refreshes instead of adding a stack
  - **StackDurationRefreshPolicy**: whether re-applying refreshes the duration (DoT renewal)
  - **StackPeriodResetPolicy**: whether re-applying resets the period timer
  - **StackExpirationPolicy**: on expiry, clear the whole group / remove one stack & refresh / refresh only
  - Modifiers and periodic execution **scale by stack count** (e.g. poison ×5 = 5× per tick); stack changes fire `OnActiveEffectStackChanged`, and `ActiveGameplayEffect.StackCount` lets the UI show ×N

**Making an instant damage GE that "passes damage via SetByCaller"**: DurationType=Instant, add one modifier — target attribute = `AS_Health.IncomingDamage`, operator = Add, magnitude = SetByCaller(`Data.Damage`). Apply it:

```csharp
asc.ApplyGameplayEffectToSelf(buffEffect, level: 1);   // simple application
var spec = asc.MakeOutgoingSpec(damageEffect)
              .SetSetByCallerMagnitude(GameplayTag.RequestTag("Data.Damage"), 30f);
var handle = asc.ApplyGameplayEffectSpecToSelf(spec);  // application with data
asc.RemoveActiveGameplayEffect(handle);                // remove a duration effect
```

**Custom damage formula** (execution calculation) — derive from `GameplayEffectExecutionCalculation`. The framework ships a sample `DamageExecutionCalculation` (`AS_Combat.Damage − mitigation → IncomingDamage`). Put it as an asset into the GE's *Executions*.

---

## 7. Abilities

### 7.1 Writing an ability

Derive from `GameplayAbility`, override `OnActivateAbility`. Common fields (set in the Inspector):
- **AbilityTags**: identity tags (used for activate-by-tag / relationship matching)
- **ActivationGroup**: `Independent` / `ExclusiveReplaceable` / `ExclusiveBlocking` (exclusivity)
- **ActivationOwnedLooseTags**: tags placed on the character while active (e.g. slide grants `State.Sliding`)
- **ActivationRequiredTags / BlockedTags**: activation gating
- **CostEffect / CooldownEffect**: cost and cooldown (the CooldownEffect's GrantedTags act as the cooldown tags)
- **AdditionalCosts**: modular non-attribute costs (ammo, charges…) — a list of `AbilityCost` ScriptableObjects, each with `OnlyApplyCostOnHit`. `CommitAbility` pays the non-on-hit ones; call `ApplyOnHitCosts()` from your ability once a hit lands
- **EnableTick**: when true, the ASC calls `AbilityTick(deltaTime)` each frame while the ability is active (override it for charge/scan loops; a non-coroutine alternative to AbilityTask)
- **EffectContainerMap**: organizes "effects to apply on hit" by tag

```csharp
[CreateAssetMenu(menuName = "Game/Abilities/Slide")]
public class GA_Slide : GameplayAbility
{
    protected override void OnActivateAbility(GameplayEventData triggerData)
    {
        if (!CommitAbility()) { EndAbility(true); return; }
        // play an animation, change movement params, open a hit window…
        EndAbility();
    }
    // Overridable: CanActivate / CheckCost / CheckCooldown / OnEndAbility(bool wasCancelled)
}
```

> ⚠️ **`CommitAbility()` is not optional.** Cost and Cooldown are only checked and applied inside `CommitAbility()` — skip it in your ability logic and both are **silently bypassed** (`CanActivate` before activation is a pre-check; it doesn't pay anything). Convention: start `OnActivateAbility` with `if (!CommitAbility()) { EndAbility(true); return; }`, and **check the return value** (a failed commit = insufficient resources / on cooldown; cancel-end immediately).

### 7.2 Grant / activate / cancel

```csharp
var h = asc.GiveAbility(slideTemplate, level: 1);
asc.TryActivateAbility(h);
asc.TryActivateAbilitiesByTag(GameplayTag.RequestTag("Ability.Slide"));
asc.ClearAbility(h); // remove
```

### 7.3 Batch grant with AbilityLoadout

An `AbilityLoadout` asset (*Create → Sigil → GAS → Ability Loadout*) bundles "abilities + persistent effects + attribute sets (typed, pick the concrete subclass in the Inspector)" for a single grant:

```csharp
var handles = asc.GrantLoadout(defaultLoadout); // returns GrantedAbilityHandles, revocable as a batch
```

### 7.4 Activation-group exclusivity

- `Independent`: not exclusive
- `ExclusiveReplaceable`: can be interrupted/replaced by another exclusive ability
- `ExclusiveBlocking`: prevents other exclusive abilities from activating

A new exclusive ability automatically interrupts the `ExclusiveReplaceable` group when it activates. Switch an active ability's group at runtime with `asc.ChangeActivationGroup(...)` (guarded by `CanChangeActivationGroup`).

### 7.5 State-aware ability relationships

`AbilityInteractionRules` (*Create → Sigil → GAS → Ability Interaction Rules*) data-drives "block/cancel/activation gating between abilities": each `AbilityTagRule` has an `AbilityTag` (which abilities the rule applies to) + `AbilityTagsToBlock` / `AbilityTagsToCancel` (when this ability activates, block/cancel abilities carrying these tags) + `ActivationRequiredTags` / `ActivationBlockedTags` (activation gating). Its **conditional rules** `ConditionalAbilityTagRules` carry an `ActorTagQuery` (`GameplayTagQuery`) so the group only takes effect while the character's current tags match the query (state-aware). Attach it to the ASC:

```csharp
asc.SetInteractionRules(myRules);   // or set the asc.InteractionRules field directly
```

> Implementation status (single-player build): `AbilityTagsToBlock`, `AbilityTagsToCancel` and the (rule/ability) `ActivationRequiredTags`/`ActivationBlockedTags` are all enforced during activation. While an ability is active, the ability tags it contributes via `AbilityTagsToBlock` prevent any other ability whose `AbilityTags` match those tags from activating; the block is reference-counted across all active sources and lifts when they end (`AbilitySystemComponent.AreAbilityTagsBlocked(...)` exposes the live set). This differs from `AbilityTagsToCancel`, which interrupts already-active abilities rather than gating future activations.

> 📦 **Worked example**: the **Playable Demo**'s `DemoConfig.asset` contains a configured `AbilityInteractionRules` — a channeled *Focus* ability blocks melee (`AbilityTagsToBlock`), and firing ranged cancels Focus (`AbilityTagsToCancel`). Import the sample and open it to inspect/copy.

---

## 8. AbilitySystemComponent cheat sheet

```csharp
// Attributes
asc.AddAttributeSet(new AS_Health());
var h = asc.GetAttributeSet<AS_Health>();
asc.ApplyModToAttributeBase(h.HealthAttribute, EAttributeModifierOp.Add, +10f); // change the base value directly

// Tags
asc.AddLooseGameplayTag(tag); asc.RemoveLooseGameplayTag(tag);
asc.HasMatchingGameplayTag(tag);
var owned = new GameplayTagContainer(); asc.GetOwnedGameplayTags(owned);

// Abilities
var handle = asc.GiveAbility(template); asc.TryActivateAbility(handle);
asc.TryActivateAbilitiesByTag(tag);
asc.ClearAbility(handle);
IReadOnlyCollection<GameplayAbilitySpec> granted = asc.GetGrantedAbilities(); // list an ability bar

// Effects
var ge = asc.ApplyGameplayEffectToSelf(effect, level); // returns an active handle (instant effects have no valid handle)
asc.ApplyGameplayEffectSpecToSelf(spec);
asc.RemoveActiveGameplayEffect(ge);
asc.GetCooldownRemainingForTags(cooldownTags, out float remain, out float dur);

// Read-only enumeration of active effects (list buffs/debuffs, read remaining time)
IReadOnlyList<ActiveGameplayEffect> actives = asc.GetActiveGameplayEffects();
ActiveGameplayEffect one = asc.GetActiveGameplayEffect(ge); // by handle; read one.TimeRemaining

// Events
asc.OnAttributeChanged += d => { /* d.Attribute / d.OldValue / d.NewValue / d.Source */ };
asc.OnAbilityActivated += ability => { };
asc.OnAbilityEnded += (ability, cancelled) => { };
asc.OnTagChanged += (tag, present) => { };
asc.OnActiveEffectAdded += active => { };   // when a duration/infinite effect is registered (instant doesn't fire)
asc.OnActiveEffectRemoved += active => { }; // on expiry / explicit removal / being replaced
asc.OnActiveEffectStackChanged += (active, oldN, newN) => { }; // stack count changed (refresh ×N badge)
asc.OnAbilityGiven += spec => { };          // ability granted (incl. loadout/global batches)
asc.OnAbilityRemoved += spec => { };        // ability removed (fired before destruction; spec still readable)

// Gameplay events / presentation
asc.SendGameplayEvent(eventTag, data);
asc.ExecuteGameplayCue(cueTag, cueParams);
```

---

## 9. Input dispatch (state-driven key polymorphism)

This is the framework's signature feature: **the same key triggers different abilities depending on the character's state** (e.g. the X key: slide while sprinting, otherwise crouch).

### 9.1 The component

`InputSystemComponent` (on the character). It's not bound to a specific input back end; it only knows `ReceiveInput(inputTag, triggerEvent, data)`:

```csharp
inputSys.ReceiveInput(
    GameplayTag.RequestTag("InputTag.Crouch"),
    InputTriggerEvent.Started,
    InputActionData.Empty);
```

With Unity's Input System, configure the `InputConfig` asset's `InputActionMappings` (each row: `InputTag` → an `InputActionReference`) and assign that `InputConfig` to the `InputSystemComponent`. On enable it subscribes each action's started/performed/canceled and dispatches `ReceiveInput` automatically — no extra component needed.

#### End to end: from a key to an ability

"key → tag → ability" is a **two-stage mapping**, configured in two places (decoupled by the InputTag in the middle):

1. **Key → InputTag**: in the `InputConfig` asset's `InputActionMappings`, add a row = an `InputActionReference` (an action from your `.inputactions` asset, e.g. *Crouch*) → an `InputTag` (e.g. `InputTag.Crouch`). Assign that `InputConfig` to the character's `InputSystemComponent`; it auto-binds on enable.
2. **InputTag → ability**: in the `InputControlSetup`'s `inputProcessors`, add an `InputProcessor_ActivateAbilityByTag` with `InputTags = InputTag.Crouch` and `AbilityTag = Ability.Crouch` (matched against the ability asset's `AbilityTags`, internally via `TryActivateAbilitiesByTag`).
3. **Hook it up**: put that `InputControlSetup` into the `InputSystemComponent`'s `inputControlSetups`.

At runtime: press the key → `InputSystemComponent` dispatches `InputTag.Crouch` → the current control setup filters the processors listening for it → activates `Ability.Crouch`. For "one key, many abilities", see the slide/crouch table in 9.2 below.

### 9.2 Control setups and polymorphism

`InputControlSetup` (*Create → Sigil → GAS → Input Control Setup*) holds:
- **Checkers (InputChecker)** (gating; all must pass to proceed; `InputChecker_TagRelationship` passes/blocks by state)
- **Processors (InputProcessor)** (the polymorphic dispatch targets)
- **ExecutionType**: `FirstOnly` (only the first one that passes runs — the key to polymorphism) / `MatchAll`

**Configuring "X = slide while sprinting / otherwise crouch"**: in one InputControlSetup, set `ExecutionType = FirstOnly` and add two `InputProcessor_ActivateAbilityByTag` listening on the same InputTag:

| Order | Processor | StateQuery | AbilityTag |
|---|---|---|---|
| [0] slide (first) | InputProcessor_ActivateAbilityByTag | `MatchAllTags(Movement.State.Sprint)` | `Ability.Slide` |
| [1] crouch (second) | InputProcessor_ActivateAbilityByTag | (empty) | `Ability.Crouch` |

Pressing the key while sprinting → [0]'s state condition passes → slide, and `FirstOnly` returns; not sprinting → [0] fails → [1] crouch.

**Same pattern, weapon-gated (one melee key → a different ability per weapon)** — the `StateQuery` just checks a weapon tag instead of a movement tag. A `WeaponComponent` injects `Weapon.Sword` / `Weapon.Axe` onto the ASC when equipped, so:

| Order | InputTags | StateQuery | AbilityTag |
|---|---|---|---|
| [0] | `InputTag.Melee` | `MatchAllTags(Weapon.Sword)` | `Ability.MeleeAttack` (light slash) |
| [1] | `InputTag.Melee` | `MatchAllTags(Weapon.Axe)` | `Ability.HeavyAttack` (heavy hit) |

Equip the sword → only [0] passes → light slash; switch to the axe → only [1] passes → heavy hit. That's "weapon → different abilities", configured purely as data.

> 📦 **Want to see a complete, working configuration?** Import the **Playable Demo** sample and open its `DemoConfig.asset` — it has fully-wired `InputControlSetup`s (this exact melee polymorphism + ranged + focus, plus a vehicle scheme) you can inspect, copy, and tweak in the Inspector.

#### Swapping the whole control scheme (vehicle / aiming / UI)

`InputSystemComponent` keeps a **stack of control setups**; the active one is always the top:

```csharp
inputSys.PushInputSetup(vehicleSetup);  // entering a vehicle: push the vehicle scheme, effective immediately
inputSys.PopInputSetup();               // exiting: pop, automatically back to the previous scheme
```

**No manual "reload config" is needed** — `ReceiveInput` always dispatches to the top `GetCurrentInputSetup()`, so pushing a new `InputControlSetup` swaps the entire scheme (gating checkers + processors), and popping restores the previous one. Typical use: push a vehicle setup on entering a vehicle (so the same physical `InputTag.Brake` now maps to a brake ability instead of a character ability), or push a UI-only setup that lets only UI inputs through (usually with `EnableInputBuffer` off). `PopInputSetup` is a no-op when only one setup remains (the base scheme can't be popped away).

> Where does this "sprint state" come from? The [movement system](#11-movement--locomotion-companion-package) writes `Movement.State.Sprint` onto the ASC's tags — that's the "state bus".

### 9.3 Input buffering (combo pre-input)

Define a buffer window in `InputConfig`; an attack animation uses an Animation Event to call `inputSys.OpenInputBufferWindow(tag)` to open the window. Inputs blocked during the window are stored in the buffer and replayed on `CloseInputBufferWindow`.

---

## 10. Melee combat

### 10.1 Attack definition

`AttackDefinition` (*Create → Sigil → Combat → Attack Definition*): what to apply on hit.
- **TargetEffect**: the main effect applied on hit (a damage GE)
- **SetByCallerMagnitudes**: the values passed to the effect (e.g. `Data.Damage = 20`)
- **TargetEffectContainer**: extra batch effects
- **TargetGameplayCues**: hit presentation
- **KnockbackDistance / HitStallingDuration**: knockback / hit-stop

### 10.2 The hit-detection component

`MeleeAttackTrace` (on the character): sphere-cast hit detection using bone sockets (Transforms attached to bones). Configure `Entries` (each = one AttackDefinition + a set of detection points).

**The hit window is driven by Animation Events** — call these at the start and end of the hit-frame range in the attack animation:

```csharp
// Called from Animation Events (int parameter = index into Entries)
public void BeginAttackTrace(int index);
public void EndAttackTrace();
```

The hit flow (automatic): sphere-cast → affiliation filtering (`CombatTeamAgent`, only hostiles) → each target is hit only once per window → apply the AttackDefinition's effects → produce an `AttackResult` registered on the target's `CombatSystemComponent` → trigger cue + hit-stop.

### 10.3 Affiliation

`CombatTeamAgent` (TeamId). Same = ally, different = enemy, -1 = neutral. Decided by `CombatTeamAgent.IsHostile(source, target)`.

### 10.4 Damage formula

By default SetByCaller goes straight to `IncomingDamage`; for mitigation, use `DamageExecutionCalculation` (reads `AS_Combat.Damage` − the target's mitigation → IncomingDamage) and put it into the damage GE's Executions.

### 10.5 Weapons and abilities (different abilities per weapon)

⚠️ **A common misconception first**: `WeaponComponent` **does not hold or grant ability assets** — it has no "ability list" field. On equip it only injects its `weaponTags` (e.g. `Weapon.Sword`) as loose tags onto the owner's ASC (`applyWeaponTagsToOwner`), and removes them on unequip. So "switch weapon → switch abilities" is done **via tags**, in one of two ways:

**Path A (built-in, tag-gating, recommended, no code)**: grant all abilities to the ASC once, and on each weapon's abilities set `ActivationRequiredTags = [Weapon.Sword]` in the ability asset. Equip the sword → `Weapon.Sword` tag present → only the sword's abilities can activate; switch to a saber → the sword tag is removed and the saber tag injected → it automatically switches to the saber's abilities. The same attack key (`InputTag.Attack`) thus activates different abilities under different weapons (combine with the 9.2 polymorphism: give each weapon a higher-priority `InputProcessor_ActivateAbilityByTag` whose `StateQuery` requires that weapon's tag).

**Path B (grant/revoke on equip, write your own glue)**: if you want a weapon to **carry** its own ability assets (granted on equip, revoked on unequip), there's no built-in field — subscribe to `WeaponComponent.OnEquipped` / `OnUnequipped` and call it yourself:

```csharp
weapon.OnEquipped   += owner => _handles = ownerASC.GrantLoadout(swordLoadout); // an AbilityLoadout asset
weapon.OnUnequipped += ()    => _handles.RevokeFrom(ownerASC);                  // GrantedAbilityHandles.RevokeFrom
```

> Which to pick: use Path A to "gate already-granted abilities by the current weapon"; use Path B to "add/remove abilities dynamically with the weapon (the ability bar changes too)". They can be combined.

**Other `WeaponComponent` / `IWeapon` bits:**

- **`SourceObject`** (`Object`, get/set) — the equipment instance or data asset a weapon is backed by. The weapon itself only injects tags; `SourceObject` is the back-reference for equipment / loot / data-table systems to trace a weapon to its source. Purely yours to read; the framework doesn't interpret it.
- **Targeting toggle** — `SetTargeting(bool)` / `ToggleTargeting()` / `IsTargeting` + `OnTargetingChanged`, a weapon-level aim/fire flag **distinct from the lock-on system** (`TargetingSystemComponent`). Reset to `false` on unequip. Hook `OnTargetingChanged` to swap a crosshair, FOV, or aim animation.
- **Multiple trace segments** — besides the primary `MeleeTrace` (+ its entry index), add extra `(MeleeAttackTrace, entryIndex)` pairs to `AdditionalTraces`. `SetWeaponActive(true/false)` opens/closes the primary **and** all additional segments together (dual-blade / main+secondary hitboxes). `RefreshTraceInstances()` re-points every segment's source at the current owner ASC (called automatically on `Equip`).

---

## 11. Movement / Locomotion — companion package

> 📦 **Movement and locomotion have been split into a separate companion package** `com.likeon.gas.movement` (GitHub: [sigil-movement](https://github.com/forestlii/sigil-movement)) and are **not in the core package**.
>
> Why: movement is a *consumer* of the GameplayTag state bus, not part of the ability system itself — splitting it out keeps the GAS core focused. The namespace is unchanged (`Likeon.GAS`); just install it alongside the core. The core can also be paired with your own movement solution.

The companion package provides: `MovementSystemComponent` / `CharacterMovementSystemComponent` (a tag-driven CharacterController movement state machine whose state can mirror onto the ASC, driving [input polymorphism](#9-input-dispatch-state-driven-key-polymorphism) like "sprint → slide"), `LocomotionAnimationDriver` + `LocomotionMath` (a locomotion animation data layer → Animator parameters), `MovementDefinition` / `MovementSettings` / `MovementTags`, and a sample layered Animator Controller generator.

👉 **See the companion package's usage guide for full details**: `com.likeon.gas.movement/Documentation~/Usage.md`.

---

## 12. Presentation (Cue / context effects / camera)

### 12.1 GameplayCue (tag-driven presentation)

Cues fire automatically when an effect is applied (the GE's **GameplayCues** field): Instant→`Executed`, Duration/Infinite→`OnActive` on application, `Removed` on removal.

Write a cue handler: `GameplayCueNotify_Static` (*Create → Sigil → GAS → Gameplay Cue Notify (Static)*), configure particles + audio + the CueTag it responds to (hierarchical match). Register it:

```csharp
GameplayCueManager.Instance.RegisterCueNotify(myCueNotify);
asc.ExecuteGameplayCue(GameplayTag.RequestTag("GameplayCue.Hit"),
    GameplayCueParameters.At(hitPoint, hitNormal));
// You can also subscribe to GameplayCueManager.Instance.OnGameplayCue and handle it yourself
```

### 12.2 Context effects (footsteps/hits by surface)

Attach `SurfaceType` to world objects (tagged `SurfaceType.Grass`, etc.). Put a `ContextEffectComponent` on the character, configured with a `ContextEffectsLibrary` (effect tag + context tag → audio/particles).

```csharp
// Footsteps: played from a ground raycast hit, auto-selecting audio by surface
ctxFx.PlayContextEffectFromHit(GameplayTag.RequestTag("ContextEffect.Footstep"), groundHit);
ctxFx.PlayContextEffect(effectTag, location, surfaceTag); // or give the surface directly
```

### 12.3 Camera mode stack

`CameraSystemComponent` drives the Unity Camera. Default is third-person (`CameraMode_ThirdPerson`, with SphereCast occlusion avoidance).

```csharp
var cam = gameObject.AddComponent<CameraSystemComponent>();
var mode = new CameraMode_ThirdPerson { ArmLength = 5f, PivotOffset = new Vector3(0,1.6f,0) };
cam.Configure(Camera.main, playerTransform, mode);
mode.AddLookInput(mouseDeltaX, -mouseDeltaY); // mouse look

// Entering aim: push a new mode (blended in over BlendTime), pop on exit
cam.PushCameraMode(aimMode);
cam.PopCameraMode(aimMode);
```

---

## 13. Editor tools

- **GameplayTag picker**: every `GameplayTag` field becomes a hierarchical dropdown + search, with a `+` beside it to add a tag in one click.
- **GameplayTagContainer multi-select**: lists the contained tags (removable) + a deduplicating dropdown to add.
- **Tag registry `GameplayTagsSettings`** + the top-level menu window **`Sigil ▸ GAS ▸ Gameplay Tags`**: add/remove tags centrally (the source of the dropdown candidates); the window also has a "scan the project for tags" button. The plugin's editor entry points are all under the **Likeon** menu, not in Project Settings.
- **Tag scanning**: menu `Sigil ▸ GAS ▸ Scan Project for Gameplay Tags` scans `RequestTag("...")` literals in the project and adds them to the registry in one click.
- **Enhanced Inspectors**: GameplayEffect / GameplayAbility / AttackDefinition / AbilityLoadout carry summaries and configuration-validation hints. `AbilityLoadout`'s attribute-set list and `InputControlSetup`'s checker/processor lists both add `[SerializeReference]` entries via a subclass dropdown.
- **Attribute-set codegen**: `AttributeSetDefinition` asset + its **Generate C#** button author attribute sets without hand-writing C# — see [§5.1](#51-authoring-attribute-sets-in-the-editor-no-hand-written-c).
- **Gameplay tag constants**: menu `Sigil ▸ GAS ▸ Generate Gameplay Tag Constants` turns the tag registry into a nested static class (`Game.GameplayTags.Movement.State.Sprint`, with `Self` for tags that are also parents), so code references tags type-safely instead of via `RequestTag("…")` — auto-synced from the registry rather than a hand-maintained constants file.

### 13.1 Runtime debugger — GAS Debugger

Menu **`Sigil ▸ GAS ▸ GAS Debugger`**. Answers "why won't this ability activate / did that buff actually land" questions while the game runs in Play Mode:

- **Picking a target**: select any GameObject in the Hierarchy/Scene — the `AbilitySystemComponent` is resolved on it or its parents (selecting a character's weapon bone still finds the host), or pick any ASC in the scene from the toolbar dropdown (not just the player). `Lock` pins the current target while you click around; `Ping` highlights it in the Hierarchy.
- **Attributes**: Base / Current per attribute, grouped by attribute set. Rows that just changed flash yellow; Current is highlighted when it differs from Base (temporary modifiers in effect).
- **Owned Tags**: currently owned tags with counts (e.g. `State.Sprinting ×2` — two sources applied it).
- **Abilities**: granted abilities with Active/Blocked state, activation group, and a cooldown progress bar.
- **Active Effects**: per live effect — remaining-duration progress bar, stacks ×N, period countdown, granted tags; effects inhibited by their ongoing tag requirements are dimmed and marked `(inhibited)`.
- **Event Log**: while the window is open it records the ASC's event stream — ability activation / **failure (with the reason enum)** / end, grants/removals, effect add/remove/stack changes, attribute changes (with the source ability/instigator), gameplay events, and tag flips. For "I pressed the button and nothing happened", read the Failed line.

Editor-only (lives in the `Likeon.GAS.Editor` assembly) — zero overhead in builds.

---

## 14. Networking

The current version is **single-player authoritative logic**. Network replication / prediction is **not implemented** and is planned for a later stage. All "state-changing" entry points are concentrated in `AbilitySystemComponent`, making it easy to add an authority check uniformly when the time comes.

> The effort estimate, work breakdown, and the two open strategic questions (whether to do client-side prediction, and re-evaluating the network library NGO) are archived in the repo's `MD/design/阶段6-联网-工作量评估.md` (a development doc, not part of the released package).

---

## 15. FAQ

> 💡 **Open the debugger first**: `Sigil ▸ GAS ▸ GAS Debugger` ([§13.1](#131-runtime-debugger--gas-debugger)) with the misbehaving character selected — it pinpoints most of the issues below at a glance.

**Q: My ability won't activate.**
**Check the `Failed` line in the debugger's Event Log — the failure reason enum tells you directly** (missing RequiredTags / BlockedTags / cost not met / on cooldown / activation-group blocked / blocked by another ability). Manual checklist: ① are the AbilityTags set (needed for activate-by-tag); ② are ActivationRequiredTags/BlockedTags blocked by the current state (see the Owned Tags panel); ③ can the CostEffect not be paid; ④ is it blocked by cooldown or activation-group exclusivity (see the Abilities panel's cooldown bar / Blocked badge).

**Q: Damage doesn't take effect.**
Watch `IncomingDamage`/`Health` in the debugger's Attributes panel (changed rows flash) while landing a hit — you'll see which layer the pipeline breaks at. Confirm the damage GE changes `AS_Health.IncomingDamage` (not Health directly), the target ASC has `AS_Health`, and the SetByCaller tag matches the one configured in the GE.

**Q: "Slide while sprinting" doesn't trigger.**
Confirm the movement system mirrors `Movement.State.Sprint` onto the ASC (automatic when an ASC is on the same object; otherwise call `move.SetGameplayTagsProvider(asc)`), and that the slide processor is ordered before crouch with ExecutionType=FirstOnly.

**Q: My melee trace doesn't hit.**
Confirm: ① Animation Events call `BeginAttackTrace`/`EndAttackTrace`; ② the socket Transforms are configured in Entries; ③ the target has a Collider + ASC; ④ both sides' `CombatTeamAgent` are hostile (different TeamId).

**Q: The tag dropdown is empty.**
Open the *Sigil ▸ GAS ▸ Gameplay Tags* window to add tags, or run *Sigil ▸ GAS ▸ Scan Project for Gameplay Tags* once (the button is also in the window).

---

## 16. Advanced systems

### 16.1 AbilityTask (coroutine async)

Use AbilityTasks inside an ability when you need to "wait a while / wait for an event / play an animation while waiting for a hit" (coroutine wrappers, auto-cancelled on ability end to prevent dangling coroutines):

```csharp
protected override void OnActivateAbility(GameplayEventData triggerData)
{
    // Play an Animator state and wait for it to finish, while listening for matching hit events
    var t = AbilityTask_PlayMontageAndWaitForEvent.PlayMontageAndWaitForEvent(
        this, animator, "Attack01", duration: 0.8f, eventTags: hitTags);
    t.OnEventReceived += (tag, data) => { /* hit confirmed: deal damage / combo */ };
    t.OnCompleted     += () => EndAbility();
    t.OnCancelled     += () => EndAbility(true);
    t.Activate();
}
```

Other tasks: `AbilityTask_WaitDelay` (delay), `WaitDelayOneFrame` (one frame), `AbilityTask_WaitGameplayEvent` (wait for an event), `AbilityTask_WaitInputPress` (wait for input, tag-gateable), `AbilityTask_WaitTargetData` (drives a TargetActor to collect targets).

> The `animator` of `PlayMontageAndWaitForEvent` can be left empty — it then drives its 5 callbacks (OnCompleted / OnBlendOut / OnInterrupted / OnCancelled / OnEventReceived) purely by `duration`, which is handy for pure logic/tests.

### 16.2 GlobalAbilitySystem (global abilities/effects)

Apply an ability/effect to **all registered ASCs** at once (arena-wide buffs/debuffs, environmental effects, phase abilities):

```csharp
// A character with a GlobalAbilitySystemRegistrant component registers automatically (or call GlobalAbilitySystem.Instance.RegisterASC manually)
GlobalAbilitySystem.Instance.ApplyEffectToAll(poisonAuraEffect);  // arena-wide poison
GlobalAbilitySystem.Instance.ApplyAbilityToAll(emoteAbility);     // everyone gains an ability
GlobalAbilitySystem.Instance.RemoveEffectFromAll(poisonAuraEffect);
```

A late-registered ASC automatically receives all globally-applied items; re-applying the same item is idempotent.

### 16.3 GamePhaseSubsystem (game phases)

Manage nested game phases with hierarchical GameplayTags — **parent and child phases coexist, sibling phases are exclusive**:

```csharp
// A phase is a GamePhaseAbility asset (Create → Sigil → GAS → Game Phase Ability) with a GamePhaseTag
GamePhaseSubsystem.Instance.StartPhase(gameAsc, playingPhase);    // Game.Playing
GamePhaseSubsystem.Instance.StartPhase(gameAsc, warmUpPhase);     // Game.Playing.WarmUp (coexists with the parent)
GamePhaseSubsystem.Instance.StartPhase(gameAsc, postGamePhase);   // ends the sibling WarmUp, keeps the parent Game.Playing

GamePhaseSubsystem.Instance.WhenPhaseStartsOrIsActive(
    GameplayTag.RequestTag("Game.Playing"), EPhaseTagMatchType.PartialMatch,
    tag => { /* when entering any sub-phase of Playing */ });
bool inPlaying = GamePhaseSubsystem.Instance.IsPhaseActive(GameplayTag.RequestTag("Game.Playing"));
```

### 16.4 CollisionTrace (generic collision detection)

Generic non-melee collision hits (traps, AOE zones, environmental damage areas). Unlike `MeleeAttackTrace` (which binds an AttackDefinition and deals damage), CollisionTrace only produces hit events — you decide the damage:

```csharp
var trace = obj.AddComponent<CollisionTrace>();
trace.SetSockets(point1, point2);          // detection points
trace.Radius = 0.5f;
trace.HitFilter = go => IsEnemy(go);        // optional filter (affiliation, etc.)
trace.OnHit += col => ApplyDamage(col.gameObject);
trace.ToggleTraceState(true);               // enable (each target hit only once per activation)
```

### 16.5 MovementCancellation

For attack animations with root-motion displacement, let the player "cancel" the displacement by moving (an action-game feel). Animation Events call `BeginWindow`/`EndWindow` at the window's start and end; if the player moves during the window, `Animator.applyRootMotion` is turned off:

```csharp
var mc = character.AddComponent<MovementCancellation>(); // auto-finds Animator + CharacterController
// Animation Events call: start of the displacement segment → mc.BeginWindow(); end of the segment → mc.EndWindow()
```

---

## 17. Binding UI (subscribe to events)

Sigil is **logic-only and ships no UI framework** — it broadcasts "things that change" as events, and you subscribe and render with any UI solution (UGUI / UI Toolkit / third-party). Drawing the HUD is the host's job; Sigil's job is to expose the data. Common mappings:

| What the UI draws | Subscribe to |
|---|---|
| Health/mana/stamina bars | `asc.OnAttributeChanged(AttributeChangeData)` — `.Attribute/.OldValue/.NewValue/.Source` |
| Status icons (stun / buff present) | `asc.OnTagChanged(tag, present)` |
| Buff/debuff icon bars (with countdown + ×N stacks) | `asc.OnActiveEffectAdded/Removed/StackChanged` + poll `asc.GetActiveGameplayEffects()` reading `TimeRemaining` / `StackCount` |
| Ability cooldown fills | poll `asc.GetCooldownRemainingForTags(...)` |
| Ability bar (loadout-driven add/remove) | `asc.OnAbilityGiven / OnAbilityRemoved` + `asc.GetGrantedAbilities()` |
| Ability activation feedback | `asc.OnAbilityActivated / OnAbilityEnded` |
| Damage numbers | `combat.OnDealtDamage / OnAttackResultReceived` |
| Poise bar | `poise.OnPoiseBroken / OnPoiseRecovered` |
| Lock-on marker | `targeting.OnTargetLockOn / OnTargetLockOff` |
| Weapon icon | `weapon.OnEquipped / OnUnequipped / OnWeaponActiveStateChanged / OnTargetingChanged` |

Buff icon-bar example (subscribe to add/remove + refresh remaining time each frame):

```csharp
void OnEnable()  { asc.OnActiveEffectAdded += Add; asc.OnActiveEffectRemoved += Remove; }
void OnDisable() { asc.OnActiveEffectAdded -= Add; asc.OnActiveEffectRemoved -= Remove; }

void Add(ActiveGameplayEffect e)    { /* instantiate an icon, remember e */ }
void Remove(ActiveGameplayEffect e) { /* destroy the matching icon */ }

void Update() // refresh countdowns
{
    foreach (var e in asc.GetActiveGameplayEffects())
        UpdateIcon(e, e.TimeRemaining); // Infinite is positive infinity; hide the countdown yourself
}
```

> Note: instant effects produce no active instance, so they **don't fire** `OnActiveEffectAdded` and aren't in the enumeration — their impact shows via `OnAttributeChanged` (e.g. health loss).

---

## 18. Common recipes

> Gameplay patterns assembled from parts already introduced above — each recipe shows how to *combine* fields/APIs, not new machinery.

### 18.1 Immunity (block effect application)

Set the damage GE's **ApplicationBlockedTags** = `Status.Immune`. While the target owns that tag, application **fails outright** (it's not applied-then-negated). Grant immunity itself via a Duration GE with `GrantedTags = [Status.Immune]` — the tag is applied for the duration and removed automatically on expiry.

```csharp
asc.ApplyGameplayEffectToSelf(immunityBuff);   // 5s immunity (Duration GE, GrantedTags=Status.Immune)
// any GE whose ApplicationBlockedTags contains Status.Immune now fails to apply
```

### 18.2 Silence / stun (block ability activation)

The mirror image, blocking abilities: set the ability's **ActivationBlockedTags** = `Status.Silenced`. The silence debuff is a Duration GE (`GrantedTags = [Status.Silenced]`). While it lasts, activation is rejected (the debugger's Event Log shows `Failed: ... (ActivationBlockedTags)`); it recovers automatically on expiry. To also *interrupt* an ability mid-cast the moment silence lands, combine with `AbilityTagsToCancel` from §7.5.

### 18.3 Passive / reactive abilities (procs)

"Counter when hit / jump when spooked": a permanently-active ability listening for events —

```csharp
[CreateAssetMenu(menuName = "Game/Abilities/CounterAttack")]
public class GA_Counter : GameplayAbility
{
    // On the asset, tick ActivateOnGranted = true (activates when granted; stays listening)
    protected override void OnActivateAbility(GameplayEventData triggerData)
    {
        var t = AbilityTask_WaitGameplayEvent.WaitGameplayEvent(
            this, GameplayTag.RequestTag("Event.Combat.WasHit")); // onlyTriggerOnce defaults to false = keep listening
        t.OnEventReceived += (tag, data) => { /* data.Instigator = who hit me → counter */ };
        t.Activate();
        // no EndAbility — it stays resident
    }
}
```

The event is sent by the attacker (or a hit callback):

```csharp
victimASC.SendGameplayEvent(GameplayTag.RequestTag("Event.Combat.WasHit"),
    new GameplayEventData { Instigator = attacker, EventMagnitude = damage });
```

### 18.4 Charged abilities (hold to charge, release to fire)

Activate the ability on key **press**, then wait for **release** inside it (`InputTriggerEvent.Canceled`) and scale damage by the held time:

```csharp
protected override void OnActivateAbility(GameplayEventData triggerData)
{
    if (!CommitAbility()) { EndAbility(true); return; }
    var t = AbilityTask_WaitInputPress.WaitInputPress(
        this, GameplayTag.RequestTag("InputTag.ChargedAttack"),
        triggerEvent: InputTriggerEvent.Canceled);           // wait for release
    t.OnPress += heldSeconds =>
    {
        float charge = Mathf.Clamp01(heldSeconds / maxChargeSeconds);
        var spec = ASC.MakeOutgoingSpec(damageEffect)
            .SetSetByCallerMagnitude(GameplayTag.RequestTag("Data.Damage"), baseDamage * (1f + charge));
        // …gather targets, then ApplyGameplayEffectSpecToSelf on them…
        EndAbility();
    };
    t.Activate();
}
```

For a per-frame charge bar, combine with `EnableTick` + `AbilityTick(dt)` (§7.1).

### 18.5 Multiple damage types (fire/cold resistances)

**Don't** create one Damage attribute per element. Instead (the Fortnite-style pattern): express the damage *type* as a **tag**, and add per-type resistance attributes:

1. Tag the damage GE's **AssetTags** with `Damage.Type.Fire` (or inject at runtime via `spec.AddDynamicAssetTags(...)` — the weapon-enchant case).
2. Add `FireResistance` / `ColdResistance` to a custom attribute set.
3. Write an ExecutionCalculation that picks the matching resistance by the spec's type tags:

```csharp
public override void Execute(GameplayEffectSpec spec, AbilitySystemComponent src,
    AbilitySystemComponent tgt, List<GameplayExecutionOutput> outputs)
{
    float damage = spec.GetSetByCallerMagnitude(DamageTag, 0f);
    var resist = tgt.GetAttributeSet<AS_Resistances>();
    foreach (var t in spec.GetAllAssetTags())                       // static AssetTags + dynamic injections
        if (t.MatchesTag(GameplayTag.RequestTag("Damage.Type.Fire")) && resist != null)
            damage -= resist.FireResistance.CurrentValue;
    // …clamp and write into IncomingDamage (see the built-in DamageExecutionCalculation)…
}
```

Caveat: "total resistance" is meaningless in this model (30% fire + 30% cold ≠ 60% overall) — display resistances per type in UI.

### 18.6 Floating damage numbers

Two routes (the Playable Demo uses the latter): ① subscribe to `combat.OnDealtDamage / OnAttackResultReceived` (table in §17); ② go through cues — where the `AS_Health` damage pipeline settles, `ExecuteGameplayCue(GameplayCue.Damage, params)` with the damage in `GameplayCueParameters.Magnitude`, and a cue handler/subscriber spawns the floating text at the hit point.

---

## 19. Design trade-offs & anti-patterns

### 19.1 Instant vs Duration vs Infinite — how to choose

The criterion: **does this modification need to be *remembered* — revertible or adjustable later?**

| Scenario | Use | Why |
|---|---|---|
| One-shot damage / heal / resource spend | **Instant** | Changes BaseValue, fire-and-forget |
| Timed buff/debuff (5s speed boost) | **Duration** | Changes CurrentValue, auto-reverts on expiry |
| Equipment bonus / talent / aura | **Infinite** | Lasts until explicitly removed — **unequips/respecs revert cleanly** |
| DoT/HoT (damage per second) | Duration/Infinite + **Period** | Each tick settles with Instant semantics onto BaseValue |

**Why equipment/talents must not be Instant — with numbers**: MaxHealth base 100, talent tier 1 = +10%.
- Instant: BaseValue permanently becomes 110; tier 2 (design intent "+20% total") multiplies again → **121, wrong** — and refunding the talent can't restore precisely.
- Infinite modifier: BaseValue stays 100; tier 1 applies ×1.1 → Current 110; tier 2 swaps in ×1.2 → **120, correct**; removing the effect cleanly returns to 100.

### 19.2 SetByCaller vs Meta Attribute vs Execution — how to choose

All three "feed runtime-computed numbers into settlement", with different jobs:

| Mechanism | What it is | When to use |
|---|---|---|
| **SetByCaller** (§6) | Stuff a float into the spec by tag at apply time | A single simple value (this hit's damage, knockback strength) — lightest, prefer it |
| **Meta Attribute** (§5) | An intermediate attribute (IncomingDamage) consumed and zeroed by the attribute set after settlement | **Multiple sources funneling into one result** settled in one place (basic attacks, DoTs, thorns all write IncomingDamage; clamping/shield-break logic lives once) |
| **Execution** (§6) | A custom calculation class with multi-attribute inputs/outputs | The formula must **read several attributes on both sides** (attacker Damage, defender mitigation, blocking state) — heaviest and most powerful |

They compose: an Execution reads the SetByCaller base value + both sides' attributes → writes the result to a Meta Attribute (the built-in `DamageExecutionCalculation` is exactly this chain).

### 19.3 Anti-patterns (don't do these)

- ❌ **Skipping `CommitAbility()` in an ability** → Cost/Cooldown silently bypassed (see the warning in §7.1).
- ❌ **Multiple `AbilitySystemComponent`s on one GameObject** → `GetComponent` resolution, movement tag mirroring, and debugger target resolution all assume a single ASC; behavior is undefined. One character = one ASC; split *attributes* across multiple AttributeSets, not multiple ASCs.
- ❌ **Mutating attributes around the pipeline** (`health.Health.CurrentValue = 50`) → skips PreAttributeChange clamping, fires no events, and the next aggregation recalculation overwrites it. The right paths: a GE, or `ApplyModToAttributeBase` from code.
- ❌ **Driving gameplay logic off tag counts** (triggering a mechanic when `GetOwnedGameplayTagCounts` reads ×3) → counts are **multi-source reference bookkeeping** (for the debugger/UI); source add/remove ordering isn't guaranteed. Gameplay checks ask presence only: `HasMatchingGameplayTag`.
- ❌ **An ability listening for its own "stop" input to toggle itself** → `ReceiveInput` broadcasts the event *before* processor dispatch, so one key press "ends the ability, then the processor immediately re-activates it". For toggle abilities use the movement package's `InputProcessor_ToggleAbilityByTag` (the toggle decision lives in the processor).
- ❌ **Importing a Sample before the packages finish compiling** → `[SerializeReference]` assets (loadout attribute sets) get corrupted when their types can't be resolved yet. Wait for package resolve + compilation, then import.

---

## 20. Coming from Unreal GAS?

Most concepts carry over **by name and meaning**; this table lists the mappings and the differences:

| UE GAS | Sigil equivalent | Notes |
|---|---|---|
| `UAbilitySystemComponent` | `AbilitySystemComponent` (MonoBehaviour) | Lives on the character GameObject |
| `IAbilitySystemInterface` | `GetComponent` / `GetComponentInParent<AbilitySystemComponent>()` | Unity idiom; no interface needed |
| OwnerActor / AvatarActor split | **Unified** (the ASC's GameObject is both) | Single-player simplification; revisit for networking |
| `AttributeSet` + `ATTRIBUTE_ACCESSORS` | `AttributeSet` subclass + `Register()` | `PreAttributeChange` / `PostGameplayEffectExecute` hooks: same names, same semantics |
| Meta Attributes (Damage) | Same (`AS_Health.IncomingDamage`) | Identical pipeline |
| GE Instant / HasDuration / Infinite / Period | Same names, same semantics | — |
| `ModifierMagnitudeCalculation` (MMC) | **Not provided** | Cover with an Execution or SetByCaller |
| `ExecutionCalculation` | `GameplayEffectExecutionCalculation` | A ScriptableObject asset |
| SetByCaller / Stacking / GrantedTags / application tag requirements | Same names, same semantics | Stacking includes AggregateByTarget/BySource |
| `FGameplayEffectContext` subclass + `AllocGameplayEffectContext` | Subclass `GameplayEffectContext` in C#, pass it when constructing a `GameplayEffectSpec` | No global alloc hook needed; the convenience path `MakeOutgoingSpec` uses the base class |
| Instancing Policy (3 modes) | Always **InstancedPerActor** (granting clones) | UE itself deprecated NonInstanced as of 5.5 |
| AbilityTriggers (event-tag-triggered abilities) | `ActivateOnGranted` + `AbilityTask_WaitGameplayEvent` | The resident-listener pattern, see §18.3 |
| Input ID enum binding | `InputTag` + `InputConfig` + `InputProcessor` polymorphism | Closer to Lyra's Enhanced Input direction (§9) |
| `CommitAbility` / Cost GE / Cooldown GE | Same names, same semantics | Cooldown remaining: `GetCooldownRemainingForTags` |
| `GameplayCueNotify_Static` | Same (ScriptableObject) | **The stateful `_Actor` form is not provided** — manage persistent effect instances yourself (Add/Remove event semantics exist) |
| Gameplay Debugger (Shift+') | `Sigil ▸ GAS ▸ GAS Debugger` (§13.1) | Inspect any selected GameObject |
| Replication / Prediction / NetExecutionPolicy | **Not implemented** (single-player authoritative) | Planned as a later phase (§14) |

---

*This document is updated alongside the framework version. For issues and version history, see the project repository.*
