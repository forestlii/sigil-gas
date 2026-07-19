# Changelog

[English](CHANGELOG.md) | [简体中文](CHANGELOG.zh-CN.md)

All notable changes to Sigil are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Fixed

- **Periodic effects no longer double-count their modifiers (DoT / regen values were wrong).** `RecalculateCurrentValue` now skips periodic effects when aggregating CurrentValue: a periodic effect applies its modifiers to BaseValue every period as an Instant-style execution, so also aggregating them as duration modifiers on CurrentValue counted the same magnitude twice — a −10 HP/s DoT dropped 20 on its first tick and read one tick low for its whole lifetime. Added a `Period <= 0` guard on the periodic loop to prevent an infinite loop if an asset's `Period` is edited to 0 at runtime.
- **`GameplayTagQuery` no longer throws on null sub-expressions.** An expression-type query whose `expressions` list contained null elements (the default when a `[SerializeReference]` element is added in the Inspector but no concrete type is chosen yet) threw a `NullReferenceException` on evaluation. The three expression loops now skip null elements and `IsEmpty` treats an all-null list as empty.
- **The ability system component now releases its granted-ability clones on destroy.** Granted abilities (and their cloned `AdditionalCosts`) are `HideAndDontSave` clones that survive scene unload; without an `OnDestroy` they leaked a batch of ScriptableObjects every time an ability-bearing character was spawned and destroyed. `OnDestroy` now destroys them (play-mode only, to avoid edit-mode `Destroy` errors) and unhooks the owned-tag trigger subscription.
- **Inhibited effects now drop their granted tags and cues, not just their attribute modifiers.** When an effect became inhibited (its `OngoingRequiredTags` stopped being met) only the attribute modifiers were rolled back; the `GrantedTags` and persistent cues stayed on. A root effect that was inhibited freed the attribute but left `State.Rooted` on, so every tag-driven system still saw the target as rooted. Inhibition now removes/restores granted tags and cues in lockstep (with a matching guard in effect removal so counts aren't double-decremented).
- **Adding a second attribute set of the same type is now rejected with a warning.** `AddAttributeSet` only blocked the same instance, not another instance of the same type; two loadouts granting the same attribute-set type produced two instances and attribute resolution silently hit only the first, so the second set's reads/writes silently no-op'd. Same-type additions are now refused with a `LogWarning`.
- **Modifiers targeting an unregistered attribute set now warn (editor-only) instead of being silently dropped.** When a GE modifier referenced an attribute set that was not added (e.g. loadout A's init GE references a set only loadout B adds), the modifier was `continue`d away with no diagnostic. `ExecuteEffectSpec` / `RecalculateAffectedAttributes` now emit an editor-only `LogWarning` naming the effect and attribute.
- **The four runtime singletons now reset on entering Play Mode.** `GlobalAbilitySystem`, `GameplayCueManager`, `GameplayTagManager` and `GamePhaseSubsystem` used `static _instance ??= new()` with no reset hook, so with Domain Reload disabled (the recommended fast-enter-playmode option) they carried destroyed objects and stale state across sessions. Each now clears `_instance` via `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`.
- **`GlobalAbilitySystem` now re-applies global effects at their original level to late-registering ASCs.** `ApplyEffectToAll(effect, level)` recorded only the effect, not the level, so an ASC that registered afterwards received the effect at level 1 regardless — curve-table magnitudes came out wrong. The level is now stored per effect and used when back-filling.
- **`GamePhaseSubsystem` no longer loses a phase's `onEnded` callback when a sibling's end handler starts another phase.** `StartPhase` passed the callback through a shared `_pendingOnEnded` field; a nested `StartPhase` during sibling teardown overwrote it, so the outer phase's callback was dropped. `OnBeginPhase` now consumes the pending callback into a local up front. `StartPhase` also `ClearAbility`s the granted phase ability if activation fails, instead of leaking it.
- **`GameplayCueManager` lifecycle fixes.** `Clear()` now destroys the live actor-cue instances instead of only clearing the dictionary (they used to leak as orphan GameObjects); an `OnActive` whose stored instance was destroyed externally now rebuilds it instead of being silently swallowed by a stale fake-null entry; and `UnregisterCueNotify` cascades — destroying that notify's active instances and clearing its pool bucket.
- **The tag registry now rejects malformed tag names.** `GameplayTagsSettings.AddTag` only checked for whitespace, so names with empty segments (`A..B`, `.A`, `A.`), quotes or other stray characters could enter the registry and break the constants generator. It now validates the dotted-segment format (letters/digits/`_`/`-` per segment) via a new `IsValidTagName`.
- **Effect settlement is now reentrancy-safe (deferred queue).** Applying or removing an effect from inside a settlement / attribute-changed hook (a normal pattern — reflect damage, execute-on-kill chains, "take damage → dispel shield") used to run immediately: it could shift `_activeEffects` mid-tick so an effect was skipped for that frame, keep processing an already-removed effect, or recurse `ExecuteEffectSpec` without bound until a StackOverflow crashed the process. The ASC now runs effect settlement inside a scope; `ApplyGameplayEffectSpecToSelf` / `RemoveActiveGameplayEffect` called while a scope is active enqueue the request and flush it once the outermost scope exits (mirrors Unreal's `FScopedAbilityListLock` + deferred removal, with a 256-op-per-flush cap that logs an error if a hook self-feeds infinitely). Steady-state (non-reentrant) calls behave exactly as before; a reentrant Apply returns an Invalid handle and a reentrant Remove returns true ("accepted"). *(Reentrancy stage 1 of 3 — ability activation (C1) and input (C6/C7) still to come; see `MD/design/重入安全-延迟队列方案.md`.)*
- **The code generators no longer emit uncompilable output for keyword names, unescaped tag strings, or member-name collisions.** The tag-constants generator escapes tag paths before embedding them in string literals and `@`-prefixes segments that are C# keywords, and reserves the enclosing type name and injected `Self` member so nested tags like `A.A` or a child named `Self` don't collide. The attribute-set generator's `Validate` now rejects C# keyword identifiers, a property named the same as the class (CS0542), and a property whose generated `{Name}Attribute` handle collides with another property.

### Documentation

- **README feature list caught up with 0.8.0 / 0.9.0.** It now covers `AbilityTriggers`, `ModifierMagnitudeCalculation` (MMC), the stateful `GameplayCueNotify_Actor`, `AbilityTask_WaitAttributeChange`, `TryActivateAbilityByClass` + `IAbilitySystemInterface` / `GetAbilitySystem`, the `PostAttributeBaseChange` hook and meta-attribute marking — all of which were already documented in the usage guide but missing from the README. The editor cheat sheet now lists **Gameplay Cue Notify (Actor)**.
- **Fixed two stale rows in the usage guide's Unreal-GAS migration table** (§20): `ModifierMagnitudeCalculation` still said *"Not provided — cover with an Execution or SetByCaller"* (added in 0.8.0) and `IAbilitySystemInterface` still said *"Unity idiom; no interface needed"* (added in 0.9.0). Both contradicted the guide's own §6 / §8.

## [0.9.1] - 2026-07-09

### Changed

- **Performance — pooling on two hot paths (no behavior change).** The ASC now rents/returns the granted-ability snapshot list it builds on every event/activation sweep (`TryActivateAbilitiesByTag`, `TryActivateAbilityByClass`, ability-cancel, and both trigger paths) from a per-instance pool instead of allocating a fresh `List` every time — reentrancy-safe, zero steady-state allocation. Stateful `GameplayCueNotify_Actor` instances are now recycled through an idle pool (bucketed per notify) on remove and reused on the next activation, instead of `new GameObject` + `Destroy` each time (delayed-fade-out / non-auto-destroy cues still take the original destroy/keep path). Adds `GameplayCueManager.PooledActorCount` for debugging.

## [0.9.0] - 2026-07-08

### Added

- **`AbilityTask_WaitAttributeChange`** — wait inside an ability until a watched attribute changes (wraps the ASC's `OnAttributeChanged`; supports an external target ASC and once-or-continuous modes).
- **`TryActivateAbilityByClass`** (a `Type` overload and generic `TryActivateAbilityByClass<T>()`) — activate the first granted ability of a given type, alongside the existing by-handle / by-tag activators.
- **`AbilitySystemComponent.GetAbilitySystem(GameObject)` + `IAbilitySystemInterface`** — resolve an ASC from any object: it prefers an `IAbilitySystemInterface` the object implements (for when the ASC lives on a child / companion object), then falls back to `GetComponent` (mirrors Unreal's `GetAbilitySystemComponentFromActor`).
- **`AttributeSet.PostAttributeBaseChange`** hook — called after an Instant/Periodic effect changes a BaseValue (mirrors Unreal's `PostAttributeBaseChange`), for reacting to permanent-value changes.
- **Meta-attribute misuse guard** (the intent of Unreal's `HideFromModifiers`) — `AttributeSet.MarkMeta` / `IsMeta`, plus an editor-only warning when a Duration/Infinite effect's modifier targets a meta attribute (which should only be written by Instant/Execution). The attribute-set codegen now emits `MarkMeta(...)` for attributes flagged `IsMeta` in the definition.

## [0.8.0] - 2026-07-08

### Added

- **Ability Triggers — abilities can auto-activate from gameplay events or owned tags.** A `GameplayAbility` can now declare `AbilityTriggers`, each a `(TriggerTag, TriggerSource)` pair. Three sources, matching Unreal's `EGameplayAbilityTriggerSource`: `GameplayEvent` (activate when a matching `SendGameplayEvent` fires — hierarchical tag match, with the event data passed in as the ability's trigger data), `OwnedTagAdded` (activate once when the owner gains the tag), and `OwnedTagPresent` (activate when the tag appears and auto-cancel the ability when it disappears). The ASC matches triggers centrally across all granted abilities, so there is no per-ability event subscription to leak.

- **`GameplayCueNotify_Actor` — stateful, persistent gameplay cues.** Complements the existing stateless `GameplayCueNotify_Static`: for Duration/Infinite effects, an `_Actor` cue spawns a single persistent instance attached to the target and drives the full `OnActive → WhileActive (per-frame) → OnRemove` lifecycle, instead of replaying a one-shot every frame. Assign a `SpawnPrefab` for a zero-code looping VFX/aura (auto-attached, auto-destroyed on remove), or subclass to override the lifecycle hooks. `GameplayCueManager` tracks one live instance per `(notify, target)`.

- **`ModifierMagnitudeCalculation` (MMC) — custom modifier magnitude via a code asset.** A new `MagnitudeType.CustomCalculationClass` lets a `GameplayEffect` modifier compute its magnitude from a ScriptableObject that reads source/target attributes and the effect spec — e.g. "damage = Strength × 1.5". Mirrors Unreal's `UGameplayModMagnitudeCalculation`; sits alongside the existing `GameplayEffectExecutionCalculation` (MMC computes one modifier's value; Execution can change multiple attributes). Existing ScalableFloat / SetByCaller / CurveTable magnitudes are unchanged.

## [0.7.3] - 2026-07-08

### Fixed

- **"Scan Project for Gameplay Tags" no longer picks up `RequestTag("…")` that is commented out.** The scanner ran its regex over the raw source, so tags inside `//` line comments and `/* */` block comments (e.g. old, commented-out code) got added to the registry. It now strips comments first while preserving string literals (so a `//` inside a string doesn't swallow a real tag after it). Added EditMode tests for the extraction.

## [0.7.2] - 2026-07-05

### Changed

- **The auto-created `GameplayTagsSettings` asset now lands in `Assets/Sigil/`** (was `Assets/LikeonGAS/`, a leftover from before the Sigil rebrand). Only affects **new** projects: adding a tag when no tag-settings asset exists yet creates one there. Existing projects are unaffected — the tools locate the tag settings by type (`t:GameplayTagsSettings`), so an asset already anywhere (including the old `Assets/LikeonGAS/` path) keeps being used.

## [0.7.1] - 2026-07-05

### Changed

- **Custom `AbilityTask`s can now be written in any assembly** (combat package, samples, or your own game code), not just the core. `AbilityTask.InitTask` is now `protected internal` instead of `internal`, so a subclass's static factory (in any assembly) can bind the task to its owning ability — matching Unreal's subclassable `UAbilityTask`. No change for existing tasks.

## [0.7.0] - 2026-07-05

### Added

- **Boilerplate script templates.** *Create → Sigil → GAS → Ability (C# Script)* / *Cue Notify (C# Script)* and *Create → Sigil → Combat → Execution Calculation (C# Script)* scaffold an empty `GameplayAbility` / `GameplayCueNotify` / `GameplayEffectExecutionCalculation` subclass (with the right base class, override stub, and a `[CreateAssetMenu]`), so you don't start these code-backed types from a blank file.

### Changed

- **Menu paths rebranded `Likeon` → `Sigil` and categorized.** Asset-create menus now live under **Sigil/GAS**, **Sigil/Combat** (Attack Definition, Bullet Definition, Combat Settings, Damage Execution, Ability Action Library), and **Sigil/Movement** (in the movement package); editor tool menus (GAS Debugger, Gameplay Tags, tag-constants generator, docs) are under **Sigil/GAS**. Purely cosmetic — existing assets are unaffected.

- **Playable Demo sample unified to the `Likeon.GAS.Sample.PlayableDemo` naming convention** — namespace, the `PlayableDemo` component class, the `PlayableDemo.unity` scene, and the asmdef file names, matching the Combat/Movement demos. The old `GASDemo` namespace/class/scene names are gone. Sample-internal; no core API change.

### Removed

- **BREAKING: the entire combat layer (`Runtime/Combat/`, 22 files) was moved out of the core into a new companion package, [`com.likeon.gas.combat`](https://github.com/forestlii/sigil-combat).** This restores the module boundary the original combat framework already had (abilities / attributes / combat as separate modules) and makes the core a pure ability system. Removed types: `AttackDefinition` / `AttackApplication` / `AttackResult`, the `CombatFlow` pipeline (`AttackRequest` / `AttackResultProcessor` / `CombatSystemComponent` / `CombatFlowComponent`), `AbilityActionLibrary`, `PoiseComponent`, `TargetingSystemComponent`, `MeleeAttackTrace`, `CollisionTrace`, `MovementCancellation`, `DamageExecutionCalculation`, `CombatTeamAgent`, `CombatSettings`, `CombatTypes`, `ICombatInterface`, `IWeapon` / `WeaponComponent`, and `BulletDefinition` / `BulletInstance` / `BulletLauncher`. Namespace is unchanged (`Likeon.GAS`), so **migration = add the `com.likeon.gas.combat` package and reference its `Likeon.GAS.Combat` assembly**; no `using` changes. The `Sigil/Combat` asset-create menus now ship with that package. Combat is attribute-name based, so it composes with your codegen attribute sets.
- **The Playable Demo sample moved to the combat companion package** (it is combat-centric). The core package no longer ships a sample.
- **BREAKING: the built-in attribute sets (`AS_Health`, `AS_Combat`, `AS_Mana`, `AS_Poise`, `AS_Stamina`) were removed from the core package.** Concrete attribute sets are game *content*, not framework mechanism — the core package now ships only the attribute *system* (`AttributeSet` base class, `GameplayAttribute`, and the codegen tooling), not a fixed Health/Mana/etc. set. Define your own with the `AttributeSetDefinition` codegen (see 0.6.0). Framework systems (poise, death filtering, damage execution) already resolve attributes **by name**, so they work with any generated set. **Migration**: generate your attribute sets in your own namespace, then update `AbilityLoadout.GrantedAttributeSets` and every `GameplayEffect` attribute reference — both the `[SerializeReference]` `{class, ns, asm}` and the `attributeSetType` full name. The bundled samples now ship their own generated sets under `Samples~/*/GenAttributes`.

## [0.6.0] - 2026-07-05

### Added

- **Attribute sets can now be authored in the editor — no hand-written C# required (codegen).** Unreal's GAS forces attributes to be written in C++; Sigil closes that gap. Create an **`AttributeSetDefinition`** asset (*Create → Sigil → GAS → Attribute Set Definition*), declare attributes (name, default value, optional min / max-attribute clamp, meta flag) in the inspector, and click **Generate C#** — `AttributeSetCodeGenerator` emits a compiling `AttributeSet` subclass (`<Name>.g.cs`: fields, `RegisterAttributes`, typed `…Attribute` handles, clamps in `PreAttributeChange`) plus a one-time hand-written partial stub (`<Name>.cs`) that the generator never overwrites. The asset is the single source of truth; generation is one-way (asset → C#), and custom logic (extra clamps via the generated `OnPreAttributeChange` hook, meta pipelines via `PostGameplayEffectExecute`) lives in the hand-written partial. Because the output is a real type, code still references attributes with full compile-time safety (`From<AS_X>("Health")`) and it drops straight into `AbilityLoadout.GrantedAttributeSets`.
- **Gameplay tag constants generator.** *Sigil → GAS → Generate Gameplay Tag Constants* reads the `GameplayTagsSettings` registry and emits a nested static class (`Game.GameplayTags.Movement.State.Sprint`, with a `Self` field for tags that are also parents) so code gets type-safe tag references instead of stringly-typed `RequestTag("…")`, auto-synced from the registry rather than hand-maintained. (Tags were already editor-creatable; this only adds the code-side constants.)

### Fixed

- **`AbilityLoadout` inspector — the "Granted Attribute Sets" list is now editable.** This `[SerializeReference] List<AttributeSet>` relied on Unity's default managed-reference UI, which offers no clear way to pick a concrete type (and the attribute-set subclasses expose only `readonly` fields, so a picked element looked empty) — added elements appeared stuck and uneditable. The `AbilityLoadout` editor now draws the list with an explicit **"+ Add Attribute Set"** type dropdown (the same pattern `InputControlSetup` already uses for its processor/checker lists, now extracted into a shared `SerializeReferenceListGUI` helper), plus an inline hint explaining that per-loadout starting values come from init effects rather than the set instance.

## [0.5.0] - 2026-07-03

### Added

- **GAS Debugger window** (`Sigil → GAS → GAS Debugger`). A lightweight Play-Mode inspector for any live `AbilitySystemComponent`: select any GameObject in the Hierarchy/Scene (the ASC is resolved on it or its parents) — or pick one from the toolbar dropdown — and watch its **attributes** (Base/Current per attribute set, recently-changed rows flash, modified Current values highlighted), **owned tags** (with per-source counts), **granted abilities** (active/blocked state, activation group, cooldown progress), and **active gameplay effects** (remaining duration, stacks, period, inhibited state, granted tags) update live. A scrolling **event log** records ability activation/failure (with reason)/end, grants/removals, effect add/remove/stack changes, attribute changes (with source), gameplay events and tag flips while the window is open. Editor-only — ships in the Editor assembly, zero runtime overhead in builds.
- **Read-only debug/UI accessors on `AbilitySystemComponent`**: `GetAttributeSets()` (enumerate all held attribute sets) and `GetOwnedGameplayTagCounts(list)` (explicit owned tags with their reference counts; also `GameplayTagCountContainer.FillTagCounts`). No behavior changes.

### Fixed

- **Composite 2D axis input (e.g. WASD) no longer collapses to a constant direction.** `InputSystemComponent` decided how to read an input callback by the *triggering control's* value type — but for composite bindings that control is the individual key (a float `ButtonControl`), not the composite. Reading a Vector2 action as float threw internally and fell back to a constant `(1, 0)`, so every WASD key produced the same direction (in the Movement Demo, all four keys "moved forward"). The value type check now uses the callback's action value type (`ctx.valueType`), so 2D composites read the real axis vector. Scalar/button bindings are unaffected.

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
- **Playable Demo ships as a prefab + scene** (designer workflow), not just runtime `AddComponent`. An editor generator (`DemoPrefabBuilder`, menu *Sigil ▸ GAS ▸ Demo ▸ Build All*) bakes `DemoPlayer` / `DemoEnemy` prefabs (under `Resources/`) and a wired `GASDemo.unity` scene; player/enemy attributes + abilities come from `PlayerLoadout` / `EnemyLoadout` assets via `AbilitySystemComponent.initialLoadouts` (no code `AddAttributeSet` / `GiveAbility`). Player/enemy construction is shared between the prefab generator and the runtime fallback through `DemoActorBuilder`. `GASDemo` is now thin orchestration: in prefab mode it only wires the cross-boundary bits a prefab can't hold (camera `ViewSource` / third-person camera / HUD) plus dynamic event subscriptions; with no scene instances it falls back to building everything at runtime (so the demo still "just runs" when added to an empty GameObject, and the headless smoke tests keep working). Reference fields on `DemoPlayerController` / `DemoRanged` are now visible in the Inspector. New smoke tests `M` / `N` / `O` cover prefab instantiation and the adopt path.
- **Demo now also showcases the input/ability wiring**: input dispatch (keys → `InputTag` → `InputProcessor_ActivateAbilityByTag` → ability, no direct `TryActivate`), context switching (`Push/PopInputSetup` for a vehicle scheme where the melee key becomes a horn), ability block/cancel via an `AbilityInteractionRules` asset (a channeled Focus blocks melee; ranged cancels Focus), and weapon → different abilities (`WeaponComponent` injects `Weapon.Sword`/`Weapon.Axe`; the melee key polymorphs to light/heavy). New demo script `DemoFocusAbility`. Still no framework changes.
- **Demo is now data-driven** — all config (input control setups, ability-interaction rules, abilities, attacks, bullet, effects) lives in a designer-editable `DemoConfig` asset (with the sub-assets nested inside it). `GASDemo` reads it from a `Config` field (wired in the demo scene); leave it empty and it falls back to building the same defaults in code (so a bare `AddComponent` / the headless smoke test still works). Menu **Sigil ▸ GAS ▸ Generate Demo Config Assets** regenerates the asset + wires the scene. New: `DemoConfig`, `DemoConfigBuilder`.
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
- **Movement** — `MovementSystemComponent`, `CharacterMovementSystemComponent`, state mirrored to the ASC; data-driven locomotion layer (in-air state, view-relative aim offset, core-state tags → Animator) + sample layered Animator Controller generator (`Sigil ▸ GAS ▸ Samples`).
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
