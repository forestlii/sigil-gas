# Sigil · Playable Demo

[English](README.md) | [简体中文](README.zh-CN.md)

A runtime-built playable demo showcasing multiple combat lines at once: melee → damage → GameplayCue,
ranged projectiles, lock-on switching, poise/stagger, and stacking buffs — plus a third-person camera,
enemy health bars, stamina, and a self-explanatory on-screen HUD.

## How to play

1. In the Package Manager, select **Sigil** and click **Import** on *Playable Demo* under the **Samples** tab.
2. Open the imported scene at `Assets/Samples/.../PlayableDemo/GASDemo.unity`.
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
| Observability | The HUD renders purely from the framework's public events / read-only enumerations — a live demo of "GAS broadcasts data, UI binds to it" |

## How it's built

- `GASDemo.cs` (MonoBehaviour) **procedurally builds** the ground, camera, player and 3 enemies and wires them up in `Awake` — no external art assets (programmer art: capsules).
- `DemoPlayerController.cs`: reads keyboard/mouse input and calls already-implemented framework features (ability activation / lock-on switching / applying buffs); it contains no combat logic itself.
- `DemoRangedAbility.cs` / `DemoRanged.cs`: a ranged ability (costs stamina) that fires from the muzzle via `BulletLauncher`; attaches a visible sphere to the logical bullet.
- `DemoMeleeAbility.cs` / `DemoHealthBar.cs` / `DemoHUD.cs`: the melee ability, a world-space health bar, and the on-screen HUD.
- `Editor/DemoSceneBuilder.cs`: menu **Likeon ▸ GAS ▸ Build Demo Scene** regenerates the scene in one click.

## Assemblies

- `Likeon.GAS.Sample.PlayableDemo` (runtime, namespace `GASDemo`; references `Likeon.GAS.Runtime` + `Unity.InputSystem`)
- `Likeon.GAS.Sample.PlayableDemo.Editor` (editor, the scene builder)

## Notes

- This is a **feature demo**; the art is placeholder capsules — replace with your own models / animations / VFX in a real project.
- The tags used by the demo (`Ability.MeleeAttack` / `Ability.RangedAttack` / `Data.Damage` / `Data.PoiseDamage` / `GameplayCue.Hit` / `State.Staggered` / `SurfaceType.Stone`) are registered at runtime via `RequestTag`; no need to pre-add them to the registry.
- The demo just exercises features that are **already implemented and tested** in the core package — it adds no extra combat logic.
