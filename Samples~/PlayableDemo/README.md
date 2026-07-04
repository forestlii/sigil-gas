# Sigil · Playable Demo

[English](README.md) | [简体中文](README.zh-CN.md)

A runtime-built playable demo showcasing multiple combat lines at once: melee → damage → GameplayCue,
ranged projectiles, lock-on switching, poise/stagger, and stacking buffs — plus a third-person camera,
enemy health bars, stamina, and a self-explanatory on-screen HUD.

## How to play

1. In the Package Manager, select **Sigil** and click **Import** on *Playable Demo* under the **Samples** tab.
2. Open the imported scene at `Assets/Samples/.../PlayableDemo/PlayableDemo.unity`.
3. Press **Play**.

Controls (also shown in the top-left HUD):

| Key | Action |
|---|---|
| **WASD** | move |
| **Shift** | sprint |
| **Mouse** | look |
| **Space / LMB** | melee |
| **RMB / F** | ranged projectile |
| **Tab** | lock-on toggle |
| **Q / E** | switch lock-on target (left / right) |
| **R** | stack a Power buff (stacking demo) |
| **1 / 2** | switch weapon: Sword / Axe (melee key becomes light / heavy) |
| **G** | Focus (channeled; melee blocked, ranged cancels it) |
| **V** | toggle Vehicle mode (melee key becomes a horn) |

> ⚠️ The project's *Active Input Handling* must be **Input System Package** (or Both). This demo uses the
> new Input System (`Keyboard/Mouse.current`); the old Input Manager alone will throw.

## What it shows

| Feature | How to see it |
|---|---|
| Melee + Cue | Swing with Space/LMB; hit enemies flash red + spark |
| Ranged projectile | Fire with RMB/F (`BulletLauncher`); auto-aims at the locked target |
| Lock-on | Tab locks the nearest enemy ahead; Q/E cycle between 3 enemies; HUD shows the current target |
| Poise / stagger | Sustained attacks drain the target's poise bar; at zero the enemy turns yellow (HUD shows the poise bar + ★ stagger) |
| Stacking buffs | Tap R to stack Power (+MaxHealth per stack); HUD shows the `×N` stack count + countdown |
| **Input dispatch** (key → tag → ability) | Keys feed `InputSystemComponent.ReceiveInput(InputTag, …)`; an `InputControlSetup` of `InputProcessor_ActivateAbilityByTag` maps InputTag → ability — nothing calls `TryActivate` directly |
| **Weapon → different abilities** | Press 1/2 to equip Sword / Axe (`WeaponComponent` injects `Weapon.Sword` / `Weapon.Axe`); the same melee key polymorphs to a light slash or a heavy hit (FirstOnly processors gated by the weapon tag) |
| **Ability block / cancel** | Hold-cast Focus (G) grants `State.Focusing`; an `AbilityInteractionRules` asset **blocks** melee while focusing and lets ranged **cancel** Focus — HUD shows "FOCUSING — melee blocked" |
| **Context switch (vehicle)** | Press V to `PushInputSetup` a vehicle scheme; the same melee key now broadcasts a horn `GameplayEvent` instead of attacking. `PopInputSetup` restores combat |
| Observability | The HUD renders purely from the framework's public events / read-only enumerations — a live demo of "GAS broadcasts data, UI binds to it" |

## How it's built

- `DemoConfig.cs` (ScriptableObject) holds **all the designer-tunable config** — the input control setups (key→tag→ability + the FirstOnly "mutual exclusion"), the `AbilityInteractionRules` (block/cancel), the abilities, attacks, bullet and effects — as asset references. The demo scene wires a `DemoConfig.asset` onto `PlayableDemo.Config`, so a designer edits these `.asset`s in the Inspector without touching code. (Leave `Config` empty and `PlayableDemo` falls back to building the same defaults in code, via `DemoConfig.CreateDefault()`.) Regenerate the asset + scene wiring with the menu **Sigil ▸ GAS ▸ Generate Demo Config Assets** (`DemoConfigBuilder`).
- `PlayableDemo.cs` (MonoBehaviour) **procedurally builds** the ground, camera, player and 3 enemies in `Awake` — no external art assets (programmer art: capsules). It also wires the `InputSystemComponent` + two `InputControlSetup`s (combat / vehicle), the two `WeaponComponent`s (Sword / Axe), the abilities, and an `AbilityInteractionRules` asset (Focus blocks melee; ranged cancels Focus).
- `DemoPlayerController.cs`: reads keyboard/mouse and feeds it to `InputSystemComponent.ReceiveInput(InputTag, …)` (input dispatch — nothing calls `TryActivate` directly), plus weapon switching (1/2), Focus (G), vehicle toggle (V), lock-on, and stacking buffs. It contains no combat logic itself.
- `DemoMeleeAbility.cs`: the melee ability — light or heavy depending on `TraceEntryIndex`; self-manages its hit window via `AbilityTask_WaitDelay`.
- `DemoFocusAbility.cs`: a channeled "Focus" ability that grants `State.Focusing` while active (used by the block/cancel demo).
- `DemoRangedAbility.cs` / `DemoRanged.cs`: a ranged ability (costs stamina) that fires from the muzzle via `BulletLauncher`; attaches a visible sphere to the logical bullet.
- `DemoHealthBar.cs` / `DemoHUD.cs`: a world-space health bar, and the on-screen HUD (weapon / vehicle / focus / horn indicators included).
- `Editor/DemoSceneBuilder.cs`: menu **Sigil ▸ GAS ▸ Build Demo Scene** regenerates the scene in one click.

## Assemblies

- `Likeon.GAS.Sample.PlayableDemo` (runtime, namespace `PlayableDemo`; references `Likeon.GAS.Runtime` + `Unity.InputSystem`)
- `Likeon.GAS.Sample.PlayableDemo.Editor` (editor, the scene builder)

## Notes

- This is a **feature demo**; the art is placeholder capsules — replace with your own models / animations / VFX in a real project.
- The tags used by the demo (`Ability.MeleeAttack` / `Ability.HeavyAttack` / `Ability.RangedAttack` / `Ability.Focus` / `Data.Damage` / `Data.PoiseDamage` / `GameplayCue.Hit` / `State.Staggered` / `State.Focusing` / `Weapon.Sword` / `Weapon.Axe` / `InputTag.Melee` / `InputTag.Ranged` / `InputTag.Focus` / `Event.Honk` / `SurfaceType.Stone`) are registered at runtime via `RequestTag`; no need to pre-add them to the registry.
- The demo just exercises features that are **already implemented and tested** in the core package — it adds no extra combat logic.
