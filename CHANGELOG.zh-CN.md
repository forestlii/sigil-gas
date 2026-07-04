# 更新日志

[English](CHANGELOG.md) | [简体中文](CHANGELOG.zh-CN.md)

本文件记录 Sigil 的所有重要变更。格式遵循 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，
版本号遵循 [语义化版本](https://semver.org/lang/zh-CN/)。

## [未发布]

### 新增

- **属性集现在可以在编辑器里定义、无需手写 C#（代码生成）。** UE 的 GAS 逼你用 C++ 写属性，Sigil 把这个痛点补上了。新建一个 **`AttributeSetDefinition`** 资产（*Create → Likeon → GAS → Attribute Set Definition*），在面板里声明属性（名字、默认值、可选的下限 / 上限属性钳制、Meta 标记），点 **生成 C#**——`AttributeSetCodeGenerator` 产出一个可编译的 `AttributeSet` 子类（`<类名>.g.cs`：字段、`RegisterAttributes`、强类型 `…Attribute` 句柄、`PreAttributeChange` 里的钳制），外加一个只生成一次、生成器永不覆盖的手写 partial 桩（`<类名>.cs`）。资产是唯一真源，生成是单向的（资产 → C#）；自定义逻辑（用生成的 `OnPreAttributeChange` 钩子加钳制、用 `PostGameplayEffectExecute` 写 Meta 管线）写在手写 partial 里。因为产物是真类型，代码照样能强类型引用属性（`From<AS_X>("Health")`），也能直接丢进 `AbilityLoadout.GrantedAttributeSets`。
- **GameplayTag 常量生成器。** *Likeon → GAS → Generate Gameplay Tag Constants* 读 `GameplayTagsSettings` 注册表，生成一个嵌套静态类（`Game.GameplayTags.Movement.State.Sprint`，对既是标签又是父级的节点生成 `Self` 字段），让代码用强类型 tag 引用替代弱类型的 `RequestTag("…")`，且从注册表自动同步、不用再手维护。（tag 本来就能在编辑器里创建；这里只补代码端常量。）

### 修复

- **`AbilityLoadout` 面板——「Granted Attribute Sets（属性集）」列表现在可编辑。** 该字段是 `[SerializeReference] List<AttributeSet>`，此前靠 Unity 默认的 managed-reference UI 绘制：既没有清晰的"选具体类型"入口，加上属性集子类的字段全是 `readonly`（选了类型也看着空），导致加进去的元素点不动、无法编辑。现在 `AbilityLoadout` 的 Inspector 用显式的 **"+ 添加属性集"** 类型下拉绘制该列表（复用 `InputControlSetup` 处理器/检查器列表早就在用的那套，抽成共享的 `SerializeReferenceListGUI`），并加一行提示说明"按装载自定义起始数值应走初始化效果、而非属性集实例本身"。

## [0.5.0] - 2026-07-03

### 新增

- **GAS 调试器窗口**（`Likeon → GAS → GAS Debugger`）。Play Mode 下检视任意存活 `AbilitySystemComponent` 的轻量工具：在 Hierarchy/Scene 里选中任意 GameObject（在其自身与父链上解析 ASC），或从工具栏下拉直接挑一个，即可实时查看它的**属性**（按属性集分组的 Base/Current，最近变更的行闪烁、Current 与 Base 不同时高亮）、**拥有标签**（带多来源计数）、**已授予技能**（激活/被阻挡状态、激活组、冷却进度条）与**激活效果**（剩余时长、层数、周期、抑制状态、授予标签）。窗口开着时滚动记录**事件日志**：技能激活/失败（带原因）/结束、授予/移除、效果增删/叠层、属性变更（带来源）、GameplayEvent 与标签翻转。纯 Editor 工具——只进 Editor 程序集，打包零开销。
- **`AbilitySystemComponent` 只读调试/UI 访问器**：`GetAttributeSets()`（枚举全部持有的属性集）与 `GetOwnedGameplayTagCounts(list)`（显式拥有标签及引用计数；同时开放 `GameplayTagCountContainer.FillTagCounts`）。零行为变化。

### 修复

- **复合 2D 轴输入（如 WASD）不再坍缩成恒定方向。** `InputSystemComponent` 此前按**触发控件**的值类型决定怎么读输入回调——但复合绑定的触发控件是实际按下的那个键（float 的 `ButtonControl`），不是复合后的 Vector2。对 Vector2 动作按 float 读会内部抛异常、落进兜底值 `(1, 0)`，于是 WASD 四个键读出同一个方向（Movement Demo 里"按什么都往前走"）。判型现改用回调的**动作值类型**（`ctx.valueType`），2D 复合轴读出真实向量；标量/按钮路径不受影响。

## [0.4.1] - 2026-07-02

### 修复

- **在 Inspector 里配置的 `GameplayTagQuery` 现在真正生效了。** 此前是否为空由一个序列化标志位 `isEmpty` 记录，而它只有代码侧的工厂方法（`MakeQuery_*` / `All` / `Any`）才会清零；在 Inspector 里配的查询（走默认构造）`isEmpty` 恒为 true，被静默当成"无条件 → 永远放行"，导致其 `tags` 和嵌套 `expressions` 全部被忽略——影响任何配在数据资产上的标签查询，例如 `InputControlSetup` 里 `InputProcessor.StateQuery`。现改为运行时按内容推导是否为空（标签类查询看 `tags`、表达式类查询看 `expressions`）。工厂构造的查询与既有资产不受影响。

## [0.4.0] - 2026-07-01

### 新增

- **模块化额外消耗**（`AbilityCost`）。技能现在可在单一的属性消耗 `CostEffect` 之外，挂一组可插拔的**非属性**消耗（弹药、充能、自定义资源）。每个 `AbilityCost` 是 `ScriptableObject`，带 `CheckCost` / `ApplyCost` 与 `OnlyApplyCostOnHit` 开关；激活要求**所有**消耗都买得起，`CommitAbility` 扣"非命中"的那些，命中后由 `GameplayAbility.ApplyOnHitCosts()` 扣"仅命中"的那些。消耗随每个被授予的技能实例克隆（对齐 UE `Instanced`），充能状态不会在角色间串味。
- **技能级逐帧 Tick**（`GameplayAbility.AbilityTick(float)`，由 `EnableTick` 开关）。ASC 每帧为开启该开关的激活技能驱动 `AbilityTick`——蓄力 / 持续扫描等逐帧逻辑的协程 `AbilityTask` 之外的替代。
- **授予即激活（被动 / 光环技能）**——`GameplayAbility.ActivateOnGranted`。开启后技能在被授予的瞬间即尝试激活（对齐 UE `TryActivateAbilityOnSpawn`），仍受 `CanActivate` 约束。任意授予路径都生效（`GiveAbility` / `GrantLoadout` / `GlobalAbilitySystem`）。
- **能力动作库**（`AbilityActionLibrary`）——一个 ScriptableObject，把多个 `AbilityActionSet` 按能力标签汇集，给一个标签 + 施法者/目标状态选出该播的攻击动作（`SelectBestAbilityActions`），对齐 UE `GCS_AbilityActionSetSettings`。经 `CombatSystemComponent` 的 `ActionLibrary` / `QueryAbilityActions(...)` / `PlayAbilityActionByTag(...)` 接入。（此前 `AbilityActionSet` 数据壳已有，但无容器、无消费者。）
- **战斗设置**（`CombatSettings`）——一个带静态 `Active` 访问器的 ScriptableObject，对齐 UE `GCS_CombatSystemSettings`：`MainMeshLookupTag` 与 `DisableAffiliationCheck`（调试开关，让攻击无视队伍归属；接入 `CombatTeamAgent.IsHostile`，默认关）。
- **战斗契约接口**（`ICombatInterface`）——战斗角色实现的契约（战斗目标、`QueryAbilityActions`、当前武器、防御输入、旋转/移动集/移动状态标签、死亡生命周期、移动输入方向），对齐 UE `GCS_CombatInterface`、类型适配 Unity。辅助 `CombatInterface.Get(go)` 从 GameObject 层级解析。

- **授予时动态标签**——`AbilityLoadout.GrantedAbility.DynamicTags` + `GiveAbility(template, level, dynamicTags)` 重载，把标签附加到被授予的技能实例（模板不动），对齐 UE `FGGA_AbilitySet_GameplayAbility.DynamicTags`（如给一次授予标 `Slot.Primary`）。
- **`AbilityTask.ExternalConfirm(endTask)`**——对齐 UE `UAbilityTask::ExternalConfirm`；基类按需结束任务，`AbilityTask_WaitTargetData` 重写为确认目标采集。
- **`Custom` 目标确认类型**——`EGameplayTargetingConfirmation` 加 `Custom`（与 UserConfirmed 一样等外部确认，但交由自定义逻辑择机）。
- **组件级 `OnPostGameplayEffectExecute`**——`AbilitySystemComponent` 现在在任意 GameplayEffect 结算后广播（对齐 UE `GGA_AttributeSystemComponent`），伤害统计 / AI 感知的订阅方无需逐 AttributeSet 重写钩子。
- **武器来源对象 + 瞄准开关 + 多 trace 段**（`IWeapon` / `WeaponComponent`）。武器现在可携带 `SourceObject`（它背靠的装备实例 / 数据资产——做装备、掉落、数据表系统时溯源用），暴露武器层瞄准开关（`SetTargeting` / `ToggleTargeting` / `IsTargeting` + `OnTargetingChanged`，区别于锁定系统），并能通过 `AdditionalTraces`（+ `RefreshTraceInstances()`）同时驱动多套碰撞判定（双刃 / 主副多段命中），不再只有单判定 + 下标。原有的单个 `MeleeTrace` 仍作主判定段照常工作。

### 变更

- **BREAKING — 激活组改名**。枚举 `EAbilityActivationPolicy` → `EAbilityActivationGroup`，值 `Parallel` / `Replaceable` / `Blocking` → `Independent` / `ExclusiveReplaceable` / `ExclusiveBlocking`；字段 `GameplayAbility.ActivationPolicy` → `ActivationGroup`；ASC 方法 `IsActivationPolicyBlocked` / `RegisterAbilityPolicy` / `UnregisterAbilityPolicy` / `CancelAbilitiesWithPolicy` → `…ActivationGroup`。让**命名**与底层概念一致（这些方法本就叫 `ChangeActivationGroup`）。现有技能资产经 `[FormerlySerializedAs]` 保留取值，枚举 int 顺序不变。
- **BREAKING — `AbilityLoadout` 属性集改强类型**。`GrantedAttributeSetTypes`（`List<string>` 类型名 + 反射解析）改为 `[SerializeReference] List<AttributeSet> GrantedAttributeSets`——Inspector 选具体属性集子类，授予时按所选类型给每个 ASC 新建独立实例。重命名类不再静默断链、不再有类型名字符串。原先用字符串列表配的 loadout 资产需重新选一遍属性集。
- **BREAKING — `OnAttributeChanged` 现携带来源**。事件签名从 `Action<GameplayAttribute, float, float>` 改为 `Action<AttributeChangeData>`，`AttributeChangeData` 含 `Attribute` / `OldValue` / `NewValue` / `Source`（一个 `GameplayEffectContext`——谁/哪个效果造成的变更；无单一来源时为 null，如移除或抑制翻转）。伤害/治疗路径会把攻击者上下文透传过去，飘血字 UI 现在能知道"谁打了谁"。

### 修复

- **`AbilityLoadout` 的属性集现在能存盘了**。`AttributeSet` 及其子类缺 `[Serializable]`，导致上面那条 `[SerializeReference] List<AttributeSet> GrantedAttributeSets` 在 loadout **存成资产或烘进 prefab 时被 Unity 静默丢弃**——内存里的 API 正常，但真正的策划工作流（Inspector 配 → 保存 → 实例化）会丢掉全部属性集。给 `AttributeSet` / `AS_Health` / `AS_Stamina` / `AS_Poise` / `AS_Mana` / `AS_Combat` 加 `[Serializable]` 后所选类型正常序列化；`GrantLoadout` 再按类型给每个 ASC 新建实例。新增一个 EditMode 测试守这条。
- **攻击类型标签现在真正到达伤害效果（修死配置）**。`AttackDefinition.AttackTags`（近战/远程、劈砍/打击等）文档写着"作为动态资产标签加进效果 spec"，但实际从未注入，导致目标无法按"被什么类型攻击"做反应。新增 `GameplayEffectSpec.DynamicAssetTags`（+ `AddDynamicAssetTags` / `GetAllAssetTags`）；两条施加路（`AttackApplication` 与 `MeleeAttackTrace`，含效果容器）现在都把 `AttackTags` 注入 spec，攻击类型并入 `AttackResult.AggregatedSourceTags`（让受击处理器能查攻击类型，如重击→硬直），`RemoveEffectsWithTags` 也改为认动态资产标签。

- **`AbilityInteractionRules.AbilityTagsToBlock` 现已强制生效**。此前该字段被收集却从未被读取，"激活期间阻挡其它技能激活"实际不生效（只有 `AbilityTagsToCancel` 有效）。现在激活中的技能会把它的 block 标签贡献到 ASC 上一个**引用计数**的集合；任何 `AbilityTags` 命中的技能在所有阻挡来源结束前都被拒绝激活。新增 API：`AbilitySystemComponent.AreAbilityTagsBlocked(...)`，以及 `AbilityInteractionRules.AddBaseRule(...)` / `AddConditionalRules(...)`（便于用代码构造规则）。由 6 个新增 PlayMode 测试覆盖（`AbilityBlockTagsPlayTests`）。测试总数：**EditMode 21 + PlayMode 95 = 116**。

### 变更

- **可玩 Demo 升级为功能展示场**。示例从"只演示近战"升级为多条战斗线同场展示（近战 / 远程子弹 / 3 敌人锁定切换 / 削韧破防 / buff 叠层）+ 自解释 HUD（全靠订阅可观测性事件渲染）。框架零改动，仅把已实现并测试过的功能调用出来。新增 demo 脚本 `DemoRanged` / `DemoRangedAbility` / `DemoHUD`。
- **可玩 Demo 改为 prefab + 场景交付**（策划工作流），不再纯运行时 `AddComponent`。编辑器生成器（`DemoPrefabBuilder`，菜单 *Likeon ▸ GAS ▸ Demo ▸ Build All*）烘出 `DemoPlayer` / `DemoEnemy` prefab（放 `Resources/`）和接好线的 `GASDemo.unity` 场景；玩家/敌人的属性集 + 技能由 `PlayerLoadout` / `EnemyLoadout` 资产经 `AbilitySystemComponent.initialLoadouts` 提供（无需代码 `AddAttributeSet` / `GiveAbility`）。玩家/敌人的结构构建由 `DemoActorBuilder` 在 prefab 生成器与运行时回退之间共用。`GASDemo` 现在是薄编排：prefab 模式下只接 prefab 接不了的跨边界引用（相机 `ViewSource` / 第三人称相机 / HUD）+ 动态事件订阅；场景没摆实例时回退到运行时现场构建（所以 demo 挂到空物体上仍"挂上就跑"，headless 冒烟测试照常）。`DemoPlayerController` / `DemoRanged` 的引用字段现在在 Inspector 可见。新增冒烟测试 `M` / `N` / `O` 覆盖 prefab 实例化与 adopt 路径。
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
