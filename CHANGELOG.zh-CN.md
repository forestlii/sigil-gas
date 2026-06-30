# 更新日志

[English](CHANGELOG.md) | [简体中文](CHANGELOG.zh-CN.md)

本文件记录 Sigil 的所有重要变更。格式遵循 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，
版本号遵循 [语义化版本](https://semver.org/lang/zh-CN/)。

## [未发布]

### 新增

- **模块化额外消耗**（`AbilityCost`）。技能现在可在单一的属性消耗 `CostEffect` 之外，挂一组可插拔的**非属性**消耗（弹药、充能、自定义资源）。每个 `AbilityCost` 是 `ScriptableObject`，带 `CheckCost` / `ApplyCost` 与 `OnlyApplyCostOnHit` 开关；激活要求**所有**消耗都买得起，`CommitAbility` 扣"非命中"的那些，命中后由 `GameplayAbility.ApplyOnHitCosts()` 扣"仅命中"的那些。消耗随每个被授予的技能实例克隆（对齐 UE `Instanced`），充能状态不会在角色间串味。
- **技能级逐帧 Tick**（`GameplayAbility.AbilityTick(float)`，由 `EnableTick` 开关）。ASC 每帧为开启该开关的激活技能驱动 `AbilityTick`——蓄力 / 持续扫描等逐帧逻辑的协程 `AbilityTask` 之外的替代。
- **授予即激活（被动 / 光环技能）**——`GameplayAbility.ActivateOnGranted`。开启后技能在被授予的瞬间即尝试激活（对齐 UE `TryActivateAbilityOnSpawn`），仍受 `CanActivate` 约束。任意授予路径都生效（`GiveAbility` / `GrantLoadout` / `GlobalAbilitySystem`）。
- **能力动作库**（`AbilityActionLibrary`）——一个 ScriptableObject，把多个 `AbilityActionSet` 按能力标签汇集，给一个标签 + 施法者/目标状态选出该播的攻击动作（`SelectBestAbilityActions`），对齐 UE `GCS_AbilityActionSetSettings`。经 `CombatSystemComponent` 的 `ActionLibrary` / `QueryAbilityActions(...)` / `PlayAbilityActionByTag(...)` 接入。（此前 `AbilityActionSet` 数据壳已有，但无容器、无消费者。）
- **战斗设置**（`CombatSettings`）——一个带静态 `Active` 访问器的 ScriptableObject，对齐 UE `GCS_CombatSystemSettings`：`MainMeshLookupTag` 与 `DisableAffiliationCheck`（调试开关，让攻击无视队伍归属；接入 `CombatTeamAgent.IsHostile`，默认关）。
- **战斗契约接口**（`ICombatInterface`）——战斗角色实现的契约（战斗目标、`QueryAbilityActions`、当前武器、防御输入、旋转/移动集/移动状态标签、死亡生命周期、移动输入方向），对齐 UE `GCS_CombatInterface`、类型适配 Unity。辅助 `CombatInterface.Get(go)` 从 GameObject 层级解析。

### 变更

- **BREAKING — 激活组改名**。枚举 `EAbilityActivationPolicy` → `EAbilityActivationGroup`，值 `Parallel` / `Replaceable` / `Blocking` → `Independent` / `ExclusiveReplaceable` / `ExclusiveBlocking`；字段 `GameplayAbility.ActivationPolicy` → `ActivationGroup`；ASC 方法 `IsActivationPolicyBlocked` / `RegisterAbilityPolicy` / `UnregisterAbilityPolicy` / `CancelAbilitiesWithPolicy` → `…ActivationGroup`。让**命名**与底层概念一致（这些方法本就叫 `ChangeActivationGroup`）。现有技能资产经 `[FormerlySerializedAs]` 保留取值，枚举 int 顺序不变。
- **BREAKING — `OnAttributeChanged` 现携带来源**。事件签名从 `Action<GameplayAttribute, float, float>` 改为 `Action<AttributeChangeData>`，`AttributeChangeData` 含 `Attribute` / `OldValue` / `NewValue` / `Source`（一个 `GameplayEffectContext`——谁/哪个效果造成的变更；无单一来源时为 null，如移除或抑制翻转）。伤害/治疗路径会把攻击者上下文透传过去，飘血字 UI 现在能知道"谁打了谁"。

### 修复

- **`AbilityInteractionRules.AbilityTagsToBlock` 现已强制生效**。此前该字段被收集却从未被读取，"激活期间阻挡其它技能激活"实际不生效（只有 `AbilityTagsToCancel` 有效）。现在激活中的技能会把它的 block 标签贡献到 ASC 上一个**引用计数**的集合；任何 `AbilityTags` 命中的技能在所有阻挡来源结束前都被拒绝激活。新增 API：`AbilitySystemComponent.AreAbilityTagsBlocked(...)`，以及 `AbilityInteractionRules.AddBaseRule(...)` / `AddConditionalRules(...)`（便于用代码构造规则）。由 6 个新增 PlayMode 测试覆盖（`AbilityBlockTagsPlayTests`）。测试总数：**EditMode 21 + PlayMode 95 = 116**。

### 变更

- **可玩 Demo 升级为功能展示场**。示例从"只演示近战"升级为多条战斗线同场展示（近战 / 远程子弹 / 3 敌人锁定切换 / 削韧破防 / buff 叠层）+ 自解释 HUD（全靠订阅可观测性事件渲染）。框架零改动，仅把已实现并测试过的功能调用出来。新增 demo 脚本 `DemoRanged` / `DemoRangedAbility` / `DemoHUD`。
- **Demo 进一步演示输入/技能接线**：输入分发（键 → `InputTag` → `InputProcessor_ActivateAbilityByTag` → 技能，不直接 `TryActivate`）、上下文切换（`Push/PopInputSetup` 切载具键位，近战键改鸣笛）、用 `AbilityInteractionRules` 资产做技能 block/cancel（持续型专注挡近战、远程取消专注）、武器 → 不同技能（`WeaponComponent` 注入 `Weapon.Sword`/`Weapon.Axe`，近战键多态成轻击/重击）。新增 demo 脚本 `DemoFocusAbility`。仍无框架改动。
- **Demo 改为数据驱动**——全部配置（输入控制集、技能交互规则、技能、攻击、子弹、效果）收进一个**策划可在 Inspector 编辑**的 `DemoConfig` 资产（子资产嵌入其中）。`GASDemo` 从 `Config` 字段读取（demo 场景已接好）；留空则回退到代码建同一套默认值（裸 `AddComponent` / headless 冒烟测试仍可跑）。菜单 **Likeon ▸ GAS ▸ Generate Demo Config Assets** 可重新生成资产并接进场景。新增 `DemoConfig`、`DemoConfigBuilder`。
- demo 冒烟测试从 1 个扩到 **11** 个（近战 / 锁定 / 远程 / 叠层 + 输入分发 / 武器切换 / 专注挡近战 / 远程取消专注 / 载具鸣笛 + 配置完整性 / 指定配置构建）全过；现**随 Sample 发布**（`Samples~/PlayableDemo/Tests/`），导入可玩 Demo 即带可运行测试。测试总数升至 **EditMode 21 + PlayMode 102 = 123**。
- 测试迁入包内 `Tests/` 目录（PlayMode + EditMode），随包发布，用户加 `"testables"` 即可运行。

## [0.3.0] - 2026-06-29

在 Unity 6000.4.10f1 实测 **EditMode 21 + PlayMode 86 = 107 个自动化测试全过**。

### 变更

- **BREAKING — 移动拆为配套包** `com.likeon.gas.movement`。`MovementSystemComponent`、`CharacterMovementSystemComponent`、运动动画层（`LocomotionAnimationDriver` / `LocomotionMath` / `LocomotionTypes`）、`MovementDefinition` / `MovementSettings` / `MovementTags`，以及示例 Animator Controller 生成器都迁到那里。命名空间不变（`Likeon.GAS`）；装配套包即可继续用移动。理由：移动是 GameplayTag 状态总线的*消费方*、非能力系统本身——拆出让 GAS 核心专注。
- 可玩 demo 改用极简内置 `CharacterController` 移动，核心示例自给自足、不依赖 movement 包。

## [0.2.0] - 2026-06-29

在 Unity 6000.4.10f1 实测 **EditMode 21 + PlayMode 86 = 107 个自动化测试全过**。

### 新增

- **GameplayEffect 叠层** — `StackingType`（None / AggregateByTarget / AggregateBySource）、`StackLimitCount`、时长刷新与周期重置策略，以及到期策略（清空整叠 / 掉一层并刷新 / 仅刷新）。修改量与周期结算按层数放大；`OnActiveEffectStackChanged`（effect, old, new）+ `ActiveGameplayEffect.StackCount` 驱动 ×N UI 角标。
- **技能授予/移除可观测性** — `AbilitySystemComponent.OnAbilityGiven` / `OnAbilityRemoved` 事件（载荷 `GameplayAbilitySpec`，loadout 与全局授予也触发；移除在实例销毁前回调）+ 只读 `GetGrantedAbilities()`，让 loadout 驱动的技能栏自动增删。

## [0.1.0] - 2026-06-29

首个公开版本。在 Unity 6000.4.10f1 实测 **EditMode 21 + PlayMode 76 = 97 个自动化测试全过**，含可玩 Demo。单机权威逻辑，阶段 6 联网为后续路线。

### 新增

- **核心能力系统** — GameplayTag、AttributeSet（`AS_Health`/`AS_Stamina`/`AS_Mana`/`AS_Combat`/`AS_Poise`）、GameplayEffect、GameplayAbility、AbilitySystemComponent、`AbilityLoadout`、`AbilityInteractionRules`。
- **技能任务框架** — `WaitDelay`、`WaitDelayOneFrame`、`WaitGameplayEvent`、`WaitInputPress`、`WaitTargetData`、`PlayMontageAndWaitForEvent`；技能结束自动取消。
- **目标采集** — `TargetActor`（线/球 trace）、`TargetData`/`TargetDataHandle`、`TargetSource`。
- **输入分发** — 分发、门控、缓冲、控制集栈；状态驱动按键多态；可选 `Likeon.GAS.UnityInput` 适配器。
- **近战战斗** — `AttackDefinition`、`AbilityActionSet`、`MeleeAttackTrace`、`DamageExecutionCalculation`、`CombatTeamAgent`、`CombatSystemComponent`。
- **削韧** — `AS_Poise` + `PoiseComponent`（破防、硬直、恢复、复位）。
- **锁定** — `TargetingSystemComponent`（过滤、选择、切换、自动解锁、事件）。
- **投射物** — `BulletDefinition` / `BulletInstance` / `BulletLauncher`（运动、散射、穿透、子弹链、`Tick(dt)`）。
- **武器** — `IWeapon` + `WeaponComponent`（装备、标签注入、激活态、远程发射）。
- **受击反应管线** — `CombatFlowComponent` + 一组 `AttackResultProcessor`（死亡、gameplay 事件、cue）。
- **`GlobalAbilitySystem`** — 一次性给所有注册 ASC 施加技能/效果；后注册者自动补全；可选 `GlobalAbilitySystemRegistrant`。
- **`GamePhaseSubsystem` + `GamePhaseAbility`** — 嵌套层级 tag 阶段（父子共存、兄弟互斥）+ 开始/结束观察者。
- **`CollisionTrace`** — 通用 OverlapSphere 命中检测，含去重、状态开关、过滤（陷阱 / AOE / 环境伤害），与 `MeleeAttackTrace` 分工。
- **`MovementCancellation`** — 动画事件驱动的窗口，玩家移动时切换 `Animator.applyRootMotion`。
- **移动** — `MovementSystemComponent`、`CharacterMovementSystemComponent`，状态镜像到 ASC；数据驱动运动层（空中状态、视角相对 aim offset、核心状态标签 → Animator）+ 示例分层 Animator Controller 生成器（`Likeon ▸ GAS ▸ Samples`）。
- **可观测性** — 供任意 UI/AI/存档消费方订阅的变更事件：`OnAttributeChanged`、`OnTagChanged`、`OnAbilityActivated/Ended`、`OnGameplayEvent`、**`OnActiveEffectAdded`/`OnActiveEffectRemoved`** + 只读 `GetActiveGameplayEffects()` / `GetActiveGameplayEffect(handle)`（buff/debuff 条带剩余时长），以及 战斗/削韧/锁定/武器 事件。
- **表现** — GameplayCue、表面效果（`SurfaceEffectComponent`/`SurfaceEffectLibrary`）、相机混合栈。
- **编辑器套件** — GameplayTag 选择器、标签注册表与窗口、`[SerializeReference]` 选择器、资产 Inspector、标签扫描器——统一收在顶部 **Likeon** 菜单下。
- **可玩 Demo** — `Samples~/PlayableDemo`，可从 Package Manager 导入。

### 修复

- **阶段嵌套** — 修正会在启动子阶段时误结束父阶段的字面条件；现父子按文档意图正确共存。

### 已知边界

- 暂无联网 — 单机权威逻辑。
- **不含包内 UI 框架——刻意如此。** Sigil 对外广播变更事件（属性、标签、技能、激活效果、战斗等）；任何 UI 方案（UGUI / UI Toolkit / 第三方）自行订阅。
- 运动含数据驱动层 + 示例 Animator Controller 生成器，但不含成品动画 clip。
- Demo 为程序员美术（胶囊体）。

[Unreleased]: #未发布
[0.3.0]: #030---2026-06-29
[0.2.0]: #020---2026-06-29
[0.1.0]: #010---2026-06-29
