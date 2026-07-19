# 更新日志

[English](CHANGELOG.md) | [简体中文](CHANGELOG.zh-CN.md)

本文件记录 Sigil 的所有重要变更。格式遵循 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，
版本号遵循 [语义化版本](https://semver.org/lang/zh-CN/)。

## [Unreleased]

### 修复

- **周期效果不再双重计数其 modifier（DoT / 回血数值原本是错的）。** `RecalculateCurrentValue` 聚合 CurrentValue 时现在跳过周期效果：周期效果每个周期按 Instant 语义把 modifier 落到 BaseValue，若同时又把它当持续修饰聚合进 CurrentValue，同一 magnitude 会被算两次——-10 HP/s 的 DoT 第一个 tick 掉 20、整个存续期读数低一个 tick。并给周期循环加了 `Period <= 0` 兜底，防止运行时把资产 `Period` 改成 0 时死循环。
- **`GameplayTagQuery` 不再因 null 子表达式抛异常。** 表达式类查询的 `expressions` 列表含 null 元素（Inspector 里给 `[SerializeReference]` 加了元素但还没选具体类型时的默认状态）会在求值时抛 `NullReferenceException`。三处表达式循环现在跳过 null 元素，`IsEmpty` 也把"全 null"视为空。
- **ASC 销毁时回收授予的技能克隆。** 授予的技能（及其克隆的 `AdditionalCosts`）是 `HideAndDontSave` 克隆、不随场景卸载回收；此前没有 `OnDestroy`，每生成/销毁一个带技能的角色就泄漏一批 ScriptableObject。现在 `OnDestroy` 逐个销毁（仅 Play 模式，避免编辑器下 `Destroy` 报错）并反注册 owned-tag 触发订阅。
- **被抑制的效果现在会一并摘除授予标签与 cue，而不只是回退属性修饰。** 效果被抑制（`OngoingRequiredTags` 不再满足）时原先只回退属性修饰，`GrantedTags` 与持续 cue 仍挂着——被抑制的定身效果属性放开了、`State.Rooted` 标签还在，所有按标签判断的系统仍认为目标定身中。现在抑制翻转时同步摘/挂授予标签与 cue（并在效果移除路径加了对应守卫，避免计数双减）。
- **添加第二个同类型属性集现在会被拒绝并告警。** `AddAttributeSet` 原本只挡同一实例、不挡同类型的另一实例；两个 Loadout 授予同一属性集类型会产生两个实例，属性解析只静默命中第一个，第二套读写静默无效。现在同类型添加会被 `LogWarning` 拒绝。
- **指向未注册属性集的 modifier 现在会告警（仅编辑器），不再静默丢弃。** GE 的 modifier 引用了未添加的属性集（如 Loadout A 的初始化 GE 引用了 Loadout B 才加的属性集）时，原先直接 `continue` 吞掉、无任何提示。`ExecuteEffectSpec` / `RecalculateAffectedAttributes` 现在输出仅编辑器的 `LogWarning`，指明效果与属性名。
- **四个运行时单例现在进入 Play Mode 时重置。** `GlobalAbilitySystem`、`GameplayCueManager`、`GameplayTagManager`、`GamePhaseSubsystem` 用 `static _instance ??= new()` 且无重置钩子；禁用 Domain Reload（官方推荐的快速进入 Play 模式选项）时会跨会话残留已销毁对象与陈旧状态。现在各自用 `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` 清空 `_instance`。
- **`GlobalAbilitySystem` 现在按原 level 给晚注册的 ASC 补发全局效果。** `ApplyEffectToAll(effect, level)` 只记效果不记 level，晚注册的 ASC 一律按 level 1 补发——曲线表 magnitude 会算错。现在按效果记录 level 并在补发时使用。
- **`GamePhaseSubsystem` 不再在"兄弟阶段的结束回调里启动了另一阶段"时丢失本阶段的 `onEnded`。** `StartPhase` 用共享字段 `_pendingOnEnded` 传回调；兄弟阶段拆除时嵌套的 `StartPhase` 会覆盖它，导致外层阶段的回调丢失。`OnBeginPhase` 现在一进来就把待定回调取进局部量（消费语义）。`StartPhase` 在激活失败时还会 `ClearAbility` 回收授予的阶段技能，不再泄漏。
- **`GameplayCueManager` 生命周期修复。** `Clear()` 现在会销毁活跃的 cue 实例，而不只是清字典（原来它们成孤儿 GameObject 泄漏）；若某 `OnActive` 记录的实例被外部销毁，现在会重建它，而不是被陈旧的 fake-null 条目静默吞掉；`UnregisterCueNotify` 现在级联——销毁该 notify 的活跃实例并清它的对象池桶。
- **标签注册表现在会拒绝格式非法的标签名。** `GameplayTagsSettings.AddTag` 原来只查空白，含空段（`A..B`、`.A`、`A.`）、引号或其它杂字符的名字能进注册表并击穿常量生成器。现在用新增的 `IsValidTagName` 校验点分段格式（每段只含字母/数字/`_`/`-`）。
- **效果结算现在是重入安全的（延迟队列）。** 在结算 / 属性变化钩子里施加或移除效果（合理写法——反伤、斩杀链、"受伤→驱散护盾"）原来会当场执行：可能在 tick 中途改动 `_activeEffects` 导致某效果本帧被跳过、对已移除的效果继续处理、或无限递归 `ExecuteEffectSpec` 直到 StackOverflow 崩进程。现在 ASC 把效果结算放进一个作用域；作用域内调用的 `ApplyGameplayEffectSpecToSelf` / `RemoveActiveGameplayEffect` 会入队，等最外层作用域退出时统一 flush（对齐 UE 的 `FScopedAbilityListLock` + 延迟移除，带每次 flush 256 操作上限，钩子无限自喂时报错中止）。稳态（非重入）调用行为完全不变；重入的 Apply 返回 Invalid 句柄、重入的 Remove 返回 true（"已受理"）。*(重入安全 3 阶段中的第 1 阶段——技能激活(C1)、输入(C6/C7)待后续；见 `MD/design/重入安全-延迟队列方案.md`。)*
- **代码生成器不再对关键字名、未转义标签串、成员重名生成不可编译代码。** 标签常量生成器把标签路径嵌进字符串字面量前先转义，并给是 C# 关键字的段加 `@` 前缀，还预留外层类型名与注入的 `Self` 成员，使 `A.A` 这类嵌套或名为 `Self` 的子段不撞名。属性集生成器的 `Validate` 现在会拒绝 C# 关键字标识符、与类名同名的属性（CS0542）、以及生成的 `{Name}Attribute` 句柄与另一属性撞名的情况。

### 文档

- **README 功能清单补齐到 0.8.0 / 0.9.0。** 现已覆盖 `AbilityTriggers`、`ModifierMagnitudeCalculation`（MMC）、有状态的 `GameplayCueNotify_Actor`、`AbilityTask_WaitAttributeChange`、`TryActivateAbilityByClass` + `IAbilitySystemInterface` / `GetAbilitySystem`、`PostAttributeBaseChange` 钩子与 meta 属性标记——这些在使用文档里早已写了，只是 README 没跟上。编辑器速查表补上 **Gameplay Cue Notify (Actor)**。
- **修正使用文档 UE GAS 迁移表（§20）里两行过时描述**：`ModifierMagnitudeCalculation` 还写着*"未提供——用 Execution 或 SetByCaller 覆盖"*（0.8.0 已实现）、`IAbilitySystemInterface` 还写着*"Unity 惯用法，无需接口"*（0.9.0 已实现）。两行都与文档自身的 §6 / §8 自相矛盾。

## [0.9.1] - 2026-07-09

### 变更

- **性能 —— 两条热路径池化（行为不变）。** ASC 每次事件/激活遍历时构建的"已授予技能快照 List"（`TryActivateAbilitiesByTag`、`TryActivateAbilityByClass`、技能取消、以及两条 trigger 路径）改为从每实例池借还，而非每次 `new List` —— 重入安全、稳态零分配。有状态 `GameplayCueNotify_Actor` 实例移除时回收进空闲池（按 notify 分桶）、下次激活复用，不再每次 `new GameObject` + `Destroy`（延迟淡出 / 不自动销毁的 Cue 仍走原销毁/保留路径）。新增 `GameplayCueManager.PooledActorCount` 供调试。

## [0.9.0] - 2026-07-08

### 新增

- **`AbilityTask_WaitAttributeChange`** —— 在技能里等某个被监听属性发生变化再继续（包装 ASC 的 `OnAttributeChanged`；支持外部目标 ASC 与"一次/持续"两种模式）。
- **`TryActivateAbilityByClass`**（`Type` 重载 + 泛型 `TryActivateAbilityByClass<T>()`）—— 按类型激活第一个已授予的该类技能，与现有的 by-handle / by-tag 激活入口并列。
- **`AbilitySystemComponent.GetAbilitySystem(GameObject)` + `IAbilitySystemInterface`** —— 从任意对象解析 ASC：优先走对象实现的 `IAbilitySystemInterface`（应对 ASC 挂在子物体/伙伴对象的情况），否则退回 `GetComponent`（对齐 UE `GetAbilitySystemComponentFromActor`）。
- **`AttributeSet.PostAttributeBaseChange`** 钩子 —— 在 Instant/Periodic 效果改完 BaseValue 后调用（对齐 UE `PostAttributeBaseChange`），用于响应"永久值"变化。
- **Meta 属性误用防护**（UE `HideFromModifiers` 的意图）—— `AttributeSet.MarkMeta` / `IsMeta`，以及当 Duration/Infinite 效果的 modifier 指向 meta 属性（本应只被 Instant/Execution 写）时的编辑器警告。属性集 codegen 现在会为定义里标了 `IsMeta` 的属性生成 `MarkMeta(...)` 调用。

## [0.8.0] - 2026-07-08

### 新增

- **Ability Triggers —— 技能可由游戏事件或拥有的标签自动激活。** `GameplayAbility` 现在可声明 `AbilityTriggers`，每条是一对 `(TriggerTag, TriggerSource)`。三种触发源，对齐 UE `EGameplayAbilityTriggerSource`：`GameplayEvent`（收到匹配的 `SendGameplayEvent` 时激活——标签层级匹配，事件数据作为技能的 triggerData 传入）、`OwnedTagAdded`（拥有该标签时激活一次）、`OwnedTagPresent`（标签出现时激活、消失时自动取消技能）。ASC 对所有已授予技能集中匹配触发，因此没有 per-ability 的事件订阅会泄漏。

- **`GameplayCueNotify_Actor` —— 有状态、持久的 gameplay cue。** 补齐无状态的 `GameplayCueNotify_Static`：对 Duration/Infinite 效果，`_Actor` cue 会 spawn 一个挂在 target 上的持久实例，走完整的 `OnActive → WhileActive（逐帧）→ OnRemove` 生命周期，而不是每帧重播一次性表现。填个 `SpawnPrefab` 即可零代码得到 loop 特效/光环（自动挂载、移除时自动销毁），或子类重写生命周期钩子。`GameplayCueManager` 按 `(notify, target)` 各管一个活跃实例。

- **`ModifierMagnitudeCalculation`（MMC）—— 用代码资产自定义修改量。** 新增 `MagnitudeType.CustomCalculationClass`，让 `GameplayEffect` 的 modifier 通过一个 ScriptableObject 读取源/目标属性和效果 spec 来算修改量——例如"伤害 = 力量 × 1.5"。对齐 UE `UGameplayModMagnitudeCalculation`；与现有的 `GameplayEffectExecutionCalculation` 并列（MMC 算一条 modifier 的值，Execution 可改多个属性）。现有 ScalableFloat / SetByCaller / CurveTable 修改量不受影响。

## [0.7.3] - 2026-07-08

### 修复

- **「Scan Project for Gameplay Tags」不再把注释掉的 `RequestTag("…")` 也扫进注册表。** 扫描器原来直接在源码原文上跑正则，所以 `//` 行注释和 `/* */` 块注释里的标签（比如注释掉的旧代码）也被加了进去。现在先剥注释再扫，且保留字符串字面量（字符串里的 `//` 不会误吞后面的真标签）。补了 EditMode 测试。

## [0.7.2] - 2026-07-05

### 变更

- **自动创建的 `GameplayTagsSettings` 资产现在落在 `Assets/Sigil/`**（原为 `Assets/LikeonGAS/`，Sigil 品牌重命名前的遗留）。只影响**新**工程：在还没有标签表资产时加标签会在那里新建一个。已有工程不受影响——工具按类型（`t:GameplayTagsSettings`）定位标签表，所以工程里已有的资产（包括旧 `Assets/LikeonGAS/` 路径的）会继续沿用。

## [0.7.1] - 2026-07-05

### 变更

- **自定义 `AbilityTask` 现在可写在任何程序集**（combat 包 / 示例 / 你自己的游戏代码），不再限核心。`AbilityTask.InitTask` 从 `internal` 改为 `protected internal`，让子类的静态工厂（任何程序集）都能把任务绑定到所属技能——对齐 UE 可派生的 `UAbilityTask`。现有任务不受影响。

## [0.7.0] - 2026-07-05

### 新增

- **样板脚本模板。** *Create → Sigil → GAS → Ability (C# Script)* / *Cue Notify (C# Script)* 和 *Create → Sigil → Combat → Execution Calculation (C# Script)* 一键生成空的 `GameplayAbility` / `GameplayCueNotify` / `GameplayEffectExecutionCalculation` 子类（带好基类、override 桩、`[CreateAssetMenu]`），这些"要写代码的类型"不用从空文件起手。

### 变更

- **菜单路径品牌 `Likeon` → `Sigil` 并分类。** 资产创建菜单现归到 **Sigil/GAS**、**Sigil/Combat**（Attack Definition / Bullet Definition / Combat Settings / Damage Execution / Ability Action Library）、**Sigil/Movement**（movement 包）；编辑器工具菜单（GAS Debugger、Gameplay Tags、tag 常量生成器、文档）归到 **Sigil/GAS**。纯外观改动——不影响已有资产。

- **可玩 Demo 示例统一到 `Likeon.GAS.Sample.PlayableDemo` 命名约定** —— 命名空间、`PlayableDemo` 组件类、`PlayableDemo.unity` 场景、asmdef 文件名，与 Combat/Movement 示例对齐。旧的 `GASDemo` 命名空间/类/场景已不复存在。仅示例内部，核心 API 无变化。

### 移除

- **破坏性变更：整个战斗层（`Runtime/Combat/`，22 个文件）从核心移出，拆成独立配套包 [`com.likeon.gas.combat`](https://github.com/forestlii/sigil-combat)。** 这恢复了原战斗框架本就有的模块边界（能力/属性/战斗是三个独立模块），让核心回归纯能力系统。移除的类型：`AttackDefinition` / `AttackApplication` / `AttackResult`、`CombatFlow` 管线（`AttackRequest` / `AttackResultProcessor` / `CombatSystemComponent` / `CombatFlowComponent`）、`AbilityActionLibrary`、`PoiseComponent`、`TargetingSystemComponent`、`MeleeAttackTrace`、`CollisionTrace`、`MovementCancellation`、`DamageExecutionCalculation`、`CombatTeamAgent`、`CombatSettings`、`CombatTypes`、`ICombatInterface`、`IWeapon` / `WeaponComponent`、`BulletDefinition` / `BulletInstance` / `BulletLauncher`。命名空间不变（`Likeon.GAS`），所以**迁移 = 加装 `com.likeon.gas.combat` 包、引用它的 `Likeon.GAS.Combat` 程序集**，无需改 `using`。`Sigil/Combat` 资产创建菜单现随该包交付。战斗按属性名解析，与你的 codegen 属性集组合即可。
- **Playable Demo 示例迁到 combat 配套包**（它是战斗向的）。核心包不再带示例。
- **破坏性变更：内置属性集（`AS_Health`、`AS_Combat`、`AS_Mana`、`AS_Poise`、`AS_Stamina`）已从核心包移除。** 具体属性集是游戏*内容*、不是框架机制——核心包现在只提供属性*系统*（`AttributeSet` 基类、`GameplayAttribute`、codegen 工具），不再预设一套 Health/Mana 等固定属性。用 `AttributeSetDefinition` codegen 定义你自己的属性集（见 0.6.0）。框架系统（削韧、死亡过滤、伤害执行）已改为**按名解析**属性，因此对任何生成的属性集都适用。**迁移**：在你自己的命名空间里生成属性集，然后更新 `AbilityLoadout.GrantedAttributeSets` 及每一处 `GameplayEffect` 的属性引用——`[SerializeReference]` 的 `{class, ns, asm}` 和 `attributeSetType` 全名两类都要改。随包 Samples 现在自带各自生成的属性集（`Samples~/*/GenAttributes`）。

## [0.6.0] - 2026-07-05

### 新增

- **属性集现在可以在编辑器里定义、无需手写 C#（代码生成）。** UE 的 GAS 逼你用 C++ 写属性，Sigil 把这个痛点补上了。新建一个 **`AttributeSetDefinition`** 资产（*Create → Sigil → GAS → Attribute Set Definition*），在面板里声明属性（名字、默认值、可选的下限 / 上限属性钳制、Meta 标记），点 **生成 C#**——`AttributeSetCodeGenerator` 产出一个可编译的 `AttributeSet` 子类（`<类名>.g.cs`：字段、`RegisterAttributes`、强类型 `…Attribute` 句柄、`PreAttributeChange` 里的钳制），外加一个只生成一次、生成器永不覆盖的手写 partial 桩（`<类名>.cs`）。资产是唯一真源，生成是单向的（资产 → C#）；自定义逻辑（用生成的 `OnPreAttributeChange` 钩子加钳制、用 `PostGameplayEffectExecute` 写 Meta 管线）写在手写 partial 里。因为产物是真类型，代码照样能强类型引用属性（`From<AS_X>("Health")`），也能直接丢进 `AbilityLoadout.GrantedAttributeSets`。
- **GameplayTag 常量生成器。** *Sigil → GAS → Generate Gameplay Tag Constants* 读 `GameplayTagsSettings` 注册表，生成一个嵌套静态类（`Game.GameplayTags.Movement.State.Sprint`，对既是标签又是父级的节点生成 `Self` 字段），让代码用强类型 tag 引用替代弱类型的 `RequestTag("…")`，且从注册表自动同步、不用再手维护。（tag 本来就能在编辑器里创建；这里只补代码端常量。）

### 修复

- **`AbilityLoadout` 面板——「Granted Attribute Sets（属性集）」列表现在可编辑。** 该字段是 `[SerializeReference] List<AttributeSet>`，此前靠 Unity 默认的 managed-reference UI 绘制：既没有清晰的"选具体类型"入口，加上属性集子类的字段全是 `readonly`（选了类型也看着空），导致加进去的元素点不动、无法编辑。现在 `AbilityLoadout` 的 Inspector 用显式的 **"+ 添加属性集"** 类型下拉绘制该列表（复用 `InputControlSetup` 处理器/检查器列表早就在用的那套，抽成共享的 `SerializeReferenceListGUI`），并加一行提示说明"按装载自定义起始数值应走初始化效果、而非属性集实例本身"。

## [0.5.0] - 2026-07-03

### 新增

- **GAS 调试器窗口**（`Sigil → GAS → GAS Debugger`）。Play Mode 下检视任意存活 `AbilitySystemComponent` 的轻量工具：在 Hierarchy/Scene 里选中任意 GameObject（在其自身与父链上解析 ASC），或从工具栏下拉直接挑一个，即可实时查看它的**属性**（按属性集分组的 Base/Current，最近变更的行闪烁、Current 与 Base 不同时高亮）、**拥有标签**（带多来源计数）、**已授予技能**（激活/被阻挡状态、激活组、冷却进度条）与**激活效果**（剩余时长、层数、周期、抑制状态、授予标签）。窗口开着时滚动记录**事件日志**：技能激活/失败（带原因）/结束、授予/移除、效果增删/叠层、属性变更（带来源）、GameplayEvent 与标签翻转。纯 Editor 工具——只进 Editor 程序集，打包零开销。
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
- **可玩 Demo 改为 prefab + 场景交付**（策划工作流），不再纯运行时 `AddComponent`。编辑器生成器（`DemoPrefabBuilder`，菜单 *Sigil ▸ GAS ▸ Demo ▸ Build All*）烘出 `DemoPlayer` / `DemoEnemy` prefab（放 `Resources/`）和接好线的 `GASDemo.unity` 场景；玩家/敌人的属性集 + 技能由 `PlayerLoadout` / `EnemyLoadout` 资产经 `AbilitySystemComponent.initialLoadouts` 提供（无需代码 `AddAttributeSet` / `GiveAbility`）。玩家/敌人的结构构建由 `DemoActorBuilder` 在 prefab 生成器与运行时回退之间共用。`GASDemo` 现在是薄编排：prefab 模式下只接 prefab 接不了的跨边界引用（相机 `ViewSource` / 第三人称相机 / HUD）+ 动态事件订阅；场景没摆实例时回退到运行时现场构建（所以 demo 挂到空物体上仍"挂上就跑"，headless 冒烟测试照常）。`DemoPlayerController` / `DemoRanged` 的引用字段现在在 Inspector 可见。新增冒烟测试 `M` / `N` / `O` 覆盖 prefab 实例化与 adopt 路径。
- **Demo 进一步演示输入/技能接线**：输入分发（键 → `InputTag` → `InputProcessor_ActivateAbilityByTag` → 技能，不直接 `TryActivate`）、上下文切换（`Push/PopInputSetup` 切载具键位，近战键改鸣笛）、用 `AbilityInteractionRules` 资产做技能 block/cancel（持续型专注挡近战、远程取消专注）、武器 → 不同技能（`WeaponComponent` 注入 `Weapon.Sword`/`Weapon.Axe`，近战键多态成轻击/重击）。新增 demo 脚本 `DemoFocusAbility`。仍无框架改动。
- **Demo 改为数据驱动**——全部配置（输入控制集、技能交互规则、技能、攻击、子弹、效果）收进一个**策划可在 Inspector 编辑**的 `DemoConfig` 资产（子资产嵌入其中）。`GASDemo` 从 `Config` 字段读取（demo 场景已接好）；留空则回退到代码建同一套默认值（裸 `AddComponent` / headless 冒烟测试仍可跑）。菜单 **Sigil ▸ GAS ▸ Generate Demo Config Assets** 可重新生成资产并接进场景。新增 `DemoConfig`、`DemoConfigBuilder`。
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
- **移动** — `MovementSystemComponent`、`CharacterMovementSystemComponent`，状态镜像到 ASC；数据驱动运动层（空中状态、视角相对 aim offset、核心状态标签 → Animator）+ 示例分层 Animator Controller 生成器（`Sigil ▸ GAS ▸ Samples`）。
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
