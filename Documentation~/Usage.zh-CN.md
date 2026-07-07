# Sigil 使用文档

[English](Usage.md) | [简体中文](Usage.zh-CN.md)

**Sigil — Action Combat & Ability Framework for Unity (GAS-style)**　by Likeon

一套面向 Unity 的动作战斗与能力框架，核心思想是 GameplayTag 驱动的状态总线架构（GAS 风格）。
用 **GameplayTag 作为状态总线**，把 *输入 → 技能 → 效果 → 属性 → 战斗 → 移动 → 表现* 解耦串联。

> 本文按"怎么做 X"组织。所有代码示例均对应框架真实 API。命名空间统一为 `Likeon.GAS`。

---

## 目录

1. [安装](#1-安装)
2. [核心概念一图流](#2-核心概念一图流)
3. [5 分钟快速上手](#3-5-分钟快速上手)
4. [GameplayTag 标签](#4-gameplaytag-标签)
5. [属性 Attributes](#5-属性-attributes)
6. [效果 Gameplay Effects](#6-效果-gameplay-effects)
7. [技能 Abilities](#7-技能-abilities)
8. [AbilitySystemComponent 速查](#8-abilitysystemcomponent-速查)
9. [输入分发（状态驱动按键多态）](#9-输入分发状态驱动按键多态)
10. [战斗（近战/远程）—— 在配套包](#10-战斗近战远程--在配套包)
11. [移动 / Locomotion（配套包）](#11-移动--locomotion-在配套包)
12. [表现层（Cue / 上下文特效 / 相机）](#12-表现层cue--上下文特效--相机)
13. [编辑器工具](#13-编辑器工具)
14. [联网](#14-联网)
15. [常见问题](#15-常见问题)
16. [进阶系统（本版新增）](#16-进阶系统本版新增)
17. [接 UI（订阅事件）](#17-接-ui订阅事件不绑-ui-方案)
18. [常用配方 Recipes](#18-常用配方-recipes)
19. [设计取舍与反模式](#19-设计取舍与反模式)
20. [给 UE GAS 用户的迁移速查](#20-给-ue-gas-用户的迁移速查)
21. [编辑器速查（创建菜单 / 工具 / codegen）](#21-编辑器速查创建菜单--工具--codegen)

---

## 1. 安装

**方式 A：本地包**
把 `com.likeon.gas` 文件夹放进工程的 `Packages/` 目录，或 Package Manager → `+` → *Add package from disk* → 选 `package.json`。

**方式 B：Git URL**
Package Manager → `+` → *Add package from git URL* → 填仓库地址。

**依赖**：包声明依赖 **Input System**（`com.unity.inputsystem`），安装 Sigil 时自动一起装上。核心用它做输入绑定（见 §9.1）；Playable Demo 用它读键鼠。
> `Likeon.GAS.Runtime` 程序集引用 Input System：`InputConfig` 把每个 `InputTag` 映射到一个 `InputActionReference`，`InputSystemComponent` 启用时自动绑定这些动作（派发到 `ReceiveInput`）。若不用 Input System，也可从任意来源手动调 `ReceiveInput(tag, event, data)`。
> ⚠️ 跑 Demo 还需把 **Project Settings ▸ Player ▸ Active Input Handling** 设为 *Input System Package*（或 *Both*），否则新输入系统读不到键鼠。

**程序集**：`Likeon.GAS.Runtime`（核心）、`Likeon.GAS.Editor`（编辑器工具）。

**配套包（可选）**：移动与运动动画在独立包 `com.likeon.gas.movement`（[sigil-movement](https://github.com/forestlii/sigil-movement)），依赖核心、命名空间不变。需要移动就和核心一起装（见 §11）。

---

## 2. 核心概念一图流

```
[输入] --InputTag--> [InputSystemComponent] --门控/多态分发--> 激活
                                                              │
                                                              ▼
[GameplayAbility] --(改属性靠)--> [GameplayEffect] --> [AttributeSet 属性]
       │                                                      ▲
       ├-- （combat 配套包）命中判定 [MeleeAttackTrace] --> [AttackDefinition] -┘
       ├-- （movement 配套包）驱动 [MovementSystemComponent]（状态写回 ASC 标签 = 状态总线）
       └-- 触发表现 [GameplayCue] / [ContextEffect] / 相机模式栈

贯穿一切：[GameplayTag] —— 角色"当前状态"由 ASC 持有的标签表达
```

记住一句话：**几乎所有"按状态分支"的逻辑，都是在查 ASC 上的 GameplayTag。**

---

## 3. 5 分钟快速上手

目标：一个角色，有血量，能激活一个技能，受到伤害掉血。

### 3.1 给角色加 ASC + 属性

```csharp
using Likeon.GAS;
using UnityEngine;

public class Character : MonoBehaviour
{
    public AbilitySystemComponent ASC { get; private set; }

    void Awake()
    {
        ASC = gameObject.AddComponent<AbilitySystemComponent>();
        ASC.AddAttributeSet(new AS_Health());   // 这里的 AS_Health 是你自己 codegen 生成的示例集（§5.1）：Health / MaxHealth / IncomingDamage / IncomingHealing
        ASC.AddAttributeSet(new AS_Stamina());

        // 监听血量变化（驱动血条 UI）
        ASC.OnAttributeChanged += d =>
            Debug.Log($"{d.Attribute} : {d.OldValue} -> {d.NewValue}（来源 {d.Source?.SourceASC}）");
    }
}
```

### 3.2 写一个技能

技能是 `GameplayAbility` 的子类（`ScriptableObject`），重写 `OnActivateAbility`：

```csharp
using Likeon.GAS;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Abilities/Heal")]
public class GA_Heal : GameplayAbility
{
    protected override void OnActivateAbility(GameplayEventData triggerData)
    {
        if (!CommitAbility()) { EndAbility(true); return; } // 扣消耗+进冷却（若配了 CostEffect/CooldownEffect）
        Debug.Log("治疗！");
        // …在这里施加治疗效果、播动画等…
        EndAbility();
    }
}
```

在 Project 里 *Create → Game/Abilities/Heal* 生成一个资产，设它的 **Ability Tags**（如 `Ability.Heal`）。

### 3.3 授予并激活

```csharp
public GA_Heal HealAbility; // 拖资产进来

void Start()
{
    var handle = ASC.GiveAbility(HealAbility);   // 授予 → 返回句柄
    ASC.TryActivateAbility(handle);              // 用句柄激活
    // 或：ASC.TryActivateAbilitiesByTag(GameplayTag.RequestTag("Ability.Heal"));
}
```

### 3.4 造成伤害

伤害走 **GameplayEffect**。新建一个伤害 GE 资产（见 [第 6 节](#6-效果-gameplay-effects)），然后：

```csharp
var spec = targetASC.MakeOutgoingSpec(damageEffect)
    .SetSetByCallerMagnitude(GameplayTag.RequestTag("Data.Damage"), 25f);
targetASC.ApplyGameplayEffectSpecToSelf(spec);
// AS_Health 会把 IncomingDamage 映射成 -Health，OnAttributeChanged 随之触发
```

---

## 4. GameplayTag 标签

层级点分字符串，子标签命中父标签。

```csharp
var sprint = GameplayTag.RequestTag("Movement.State.Sprint");
sprint.MatchesTag(GameplayTag.RequestTag("Movement.State")); // true（子命中父）
sprint.MatchesTagExact(...);                                    // 精确比对

// 容器
var c = new GameplayTagContainer();
c.AddTag(sprint);
c.HasTag(GameplayTag.RequestTag("Movement.State")); // true（层级）
c.HasAny(otherContainer); c.HasAll(otherContainer);

// 查询（用于"按状态分支"）
var q = GameplayTagQuery.MakeQuery_MatchAllTags(sprint);
q.Matches(c); // true
// 还有 MakeQuery_MatchAnyTags / MakeQuery_MatchNoTags / All(...) / Any(...)
```

**ASC 上的"拥有标签"**（角色当前状态）：

```csharp
asc.AddLooseGameplayTag(GameplayTag.RequestTag("State.Stunned"));
asc.HasMatchingGameplayTag(GameplayTag.RequestTag("State"));    // true（层级）
asc.RemoveLooseGameplayTag(GameplayTag.RequestTag("State.Stunned"));
```
松散标签带引用计数：多个来源加同一标签，少一方移除不会误删。

> 编辑器里别手打字符串——用 [标签选择器](#13-编辑器工具)。

---

## 5. 属性 Attributes

属性集**不**再内置于框架——你用 `AttributeSetDefinition` codegen 自己生成（见 [§5.1](#51-在编辑器里定义属性集不用手写-c)）。下文以生成的 `AS_Health`（Health / MaxHealth / IncomingDamage / IncomingHealing）为例；`AS_Stamina`、`AS_Mana` 等只是同样方式生成的其它集。每个属性是 `GameplayAttributeData`（区分 `BaseValue` 永久值 / `CurrentValue` 含临时增益的当前值）。

```csharp
var health = asc.GetAttributeSet<AS_Health>();
float hp = health.Health.CurrentValue;
GameplayAttribute hpAttr = health.HealthAttribute; // 属性句柄，给 GE 的修改器用
```

**伤害 Meta 管线**：不要直接改 `Health`。伤害打进中间属性 `IncomingDamage`，`AS_Health` 在结算后把它清零并映射成 `-Health`（自动 clamp 到 `[0, MaxHealth]`）。治疗同理走 `IncomingHealing`。

> 📌 **属性初始化/装备加成怎么配**：默认值简单的直接写在字段构造里（如 `new GameplayAttributeData(100f)`）；数据驱动的初始化走 `AbilityLoadout.GrantedEffects` 配一个**常驻（Infinite）GE**，等级由 ASC 的 `attributeInitializeLevel` 决定（曲线表 magnitude 按级查值）。**装备/天赋这类"以后还会变"的加成务必用 Infinite GE 而不是 Instant**——为什么见 [§19.1](#19-设计取舍与反模式)。

**自定义属性集**：继承 `AttributeSet`，在 `RegisterAttributes()` 里登记字段：

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

### 5.1 在编辑器里定义属性集（不用手写 C#）

UE 的 GAS 逼你用 C++ 写属性；Sigil 让你在编辑器里定义、工具替你生成 C#——策划不用找程序员就能加属性，而代码端拿到的仍是真类型、编译期安全。

1. *Create → Sigil → GAS → **Attribute Set Definition***。在资产上填类名 + 命名空间，列出属性：名字、默认值、可选钳制（下限，和/或"夹到某属性"如 `Health` → `MaxHealth`）、Meta 标记。
2. 点 **生成 C#**。在资产旁写两个文件：
   - `<类名>.g.cs` —— 生成的 `AttributeSet` 子类（字段、`RegisterAttributes`、强类型 `…Attribute` 句柄、`PreAttributeChange` 里的钳制）。**别手改它**——改属性去编辑资产后重新生成。
   - `<类名>.cs` —— 手写 partial，只生成**一次**、之后永不覆盖。自定义逻辑写这里：用生成代码会调用的 `partial void OnPreAttributeChange(…)` 钩子加钳制、override `PostGameplayEffectExecute` 写伤害 Meta 管线。

定义资产是唯一真源，生成是单向的（资产 → C#）。产物是普通类型——代码里 `From<AS_PlayerStats>("Health")` 引用、丢进 `AbilityLoadout.GrantedAttributeSets`，和手写集一模一样。改属性的**数值/钳制范围**随便改；只有**增删属性**才需要重新生成 + 重编译。

---

## 6. 效果 Gameplay Effects

`GameplayEffect` 是 ScriptableObject：*Create → Sigil → GAS → Gameplay Effect*。

**关键字段**：
- **DurationType**：`Instant`（改基础值，如一次伤害）/ `HasDuration`（限时改当前值）/ `Infinite`（持续到手动移除）
- **Period**：>0 则周期结算（如每秒中毒）
- **Modifiers**：每条 = 目标属性 + 运算符（Add/Multiply/Divide/Override）+ 修改量
  - 修改量来源：`ScalableFloat`（基础 + 每级增量）、`SetByCaller`（运行时按标签传入）、`CurveTableBased`（按等级查曲线表）或 `CustomCalculationClass`（MMC 资产——见下）
- **Executions**：复杂结算（见 `GameplayEffectExecutionCalculation`，如带减伤的伤害）
- **GrantedTags**：持续期间挂在目标身上的标签（仅 Duration/Infinite）
- **ApplicationRequiredTags / BlockedTags**：施加条件
- **OngoingRequiredTags**：持续期间需满足，否则效果被抑制
- **GameplayCues**：施加时触发的表现 cue 标签
- **Stacking 叠层**（仅 Duration/Infinite）：
  - **StackingType**：`None`（每次施加各算各的独立实例）/ `AggregateByTarget`（按目标合并成带层数的一个实例）/ `AggregateBySource`（按来源各合并一个）
  - **StackLimitCount**：层数上限（>0 生效，0=无上限）；封顶后再施加只刷新不加层
  - **StackDurationRefreshPolicy**：再施加是否刷新时长（DoT 续期）
  - **StackPeriodResetPolicy**：再施加是否重置周期计时
  - **StackExpirationPolicy**：到期时整组清空 / 掉一层并刷新 / 仅刷新
  - 修改量与周期结算**按层放大**（如中毒 ×5 = 5 倍/跳）；层数变化触发 `OnActiveEffectStackChanged`，`ActiveGameplayEffect.StackCount` 供 UI 显示 ×N

**做一个"用 SetByCaller 传伤害"的瞬时伤害 GE**：DurationType=Instant，加一条修改器：目标属性 = `AS_Health.IncomingDamage`，运算符 = Add，修改量 = SetByCaller(`Data.Damage`)。施加：

```csharp
asc.ApplyGameplayEffectToSelf(buffEffect, level: 1);   // 简单施加
var spec = asc.MakeOutgoingSpec(damageEffect)
              .SetSetByCallerMagnitude(GameplayTag.RequestTag("Data.Damage"), 30f);
var handle = asc.ApplyGameplayEffectSpecToSelf(spec);  // 带数据施加
asc.RemoveActiveGameplayEffect(handle);                // 移除持续效果
```

**自定义伤害公式**（执行计算）——继承 `GameplayEffectExecutionCalculation`，**combat 配套包**附带示例 `DamageExecutionCalculation`（`Damage − 减伤 → IncomingDamage`，按属性名解析）。把它作为资产放进 GE 的 *Executions*。

**属性联动修改量**（MMC）——当*某一条 modifier* 的值依赖属性（如"伤害 = 力量 × 1.5"），继承 `ModifierMagnitudeCalculation`，把该 modifier 的修改量设为 `CustomCalculationClass` 并挂上你的资产。它读取源/目标属性 + 效果 spec、返回单个数值——不像 Execution 能改多个属性。单条属性联动的 modifier 用 MMC；多属性公式（减伤、暴击）用 Execution。

```csharp
[CreateAssetMenu(menuName = "MyGame/MMC Damage From Strength")]
public class MMC_DamageFromStrength : ModifierMagnitudeCalculation
{
    public GameplayAttribute Strength;
    public override float CalculateBaseMagnitude(
        GameplayEffectSpec spec, AbilitySystemComponent source, AbilitySystemComponent target)
        => (source != null ? source.GetAttributeValue(Strength) : 0f) * 1.5f;
}
```

---

## 7. 技能 Abilities

### 7.1 写技能

继承 `GameplayAbility`，重写 `OnActivateAbility`。常用字段（Inspector 配）：
- **AbilityTags**：身份标签（用于按标签激活 / 关系匹配）
- **ActivationGroup**：`Independent` / `ExclusiveReplaceable` / `ExclusiveBlocking`（独占互斥）
- **ActivationOwnedLooseTags**：激活期间给角色挂的标签（如滑铲挂 `State.Sliding`）
- **ActivationRequiredTags / BlockedTags**：激活准入
- **CostEffect / CooldownEffect**：消耗与冷却（CooldownEffect 的 GrantedTags 当冷却标签）
- **AdditionalCosts**：模块化非属性消耗（弹药、充能…）——一组 `AbilityCost` ScriptableObject，各带 `OnlyApplyCostOnHit`。`CommitAbility` 扣"非命中"的；命中后由技能调 `ApplyOnHitCosts()` 扣"仅命中"的
- **EnableTick**：开启后，ASC 在技能激活期间每帧调 `AbilityTick(deltaTime)`（重写它做蓄力/扫描等逐帧逻辑；AbilityTask 之外的非协程替代）
- **EffectContainerMap**：按标签组织"命中要施加的效果"

```csharp
[CreateAssetMenu(menuName = "Game/Abilities/Slide")]
public class GA_Slide : GameplayAbility
{
    protected override void OnActivateAbility(GameplayEventData triggerData)
    {
        if (!CommitAbility()) { EndAbility(true); return; }
        // 播动画、改移动参数、开判定……
        EndAbility();
    }
    // 可重写：CanActivate / CheckCost / CheckCooldown / OnEndAbility(bool wasCancelled)
}
```

> ⚠️ **`CommitAbility()` 不是可选项**。Cost 与 Cooldown 只在 `CommitAbility()` 里检查并施加——技能逻辑里不调它，消耗和冷却就被**静默绕过**（激活前的 `CanActivate` 只做预检，不扣账）。约定：`OnActivateAbility` 开头 `if (!CommitAbility()) { EndAbility(true); return; }`，并且**检查返回值**（提交失败=资源不足/冷却中，应立即取消结束）。

### 7.2 授予 / 激活 / 取消

```csharp
var h = asc.GiveAbility(slideTemplate, level: 1);
asc.TryActivateAbility(h);
asc.TryActivateAbilitiesByTag(GameplayTag.RequestTag("Ability.Slide"));
asc.ClearAbility(h); // 移除
```

### 7.3 批量授予 AbilityLoadout

`AbilityLoadout` 资产（*Create → Sigil → GAS → Ability Loadout*）打包"技能 + 常驻效果 + 属性集（强类型，Inspector 选具体子类）"，一次授予：

```csharp
var handles = asc.GrantLoadout(defaultLoadout); // 返回 GrantedAbilityHandles，可整批回收
```

### 7.4 激活组互斥

- `Independent`：不互斥
- `ExclusiveReplaceable`：可被其它独占技能打断替换
- `ExclusiveBlocking`：阻止其它独占技能激活

新独占技能激活时会自动打断 `ExclusiveReplaceable` 组。运行时可用 `asc.ChangeActivationGroup(...)` 切换激活中技能的组（由 `CanChangeActivationGroup` 守卫）。

### 7.5 状态感知的能力关系

`AbilityInteractionRules`（*Create → Sigil → GAS → Ability Interaction Rules*）把"技能之间 block/cancel/激活准入"数据化：每条 `AbilityTagRule` 含 `AbilityTag`（规则作用于哪些技能）+ `AbilityTagsToBlock` / `AbilityTagsToCancel`（本技能激活时挡/取消带这些标签的技能）+ `ActivationRequiredTags` / `ActivationBlockedTags`（激活准入）。其中**条件规则** `ConditionalAbilityTagRules` 带 `ActorTagQuery`（`GameplayTagQuery`），仅当角色当前标签满足查询时该组规则才生效（状态感知）。挂到 ASC：

```csharp
asc.SetInteractionRules(myRules);   // 或直接设 asc.InteractionRules 字段
```

> 实现现状（单机版）：`AbilityTagsToBlock`、`AbilityTagsToCancel` 与（规则/技能上的）`ActivationRequiredTags`/`ActivationBlockedTags` 均已在激活流程中强制生效。技能激活期间，它经 `AbilityTagsToBlock` 贡献的标签会阻止任何 `AbilityTags` 命中这些标签的技能激活；该阻挡按所有激活中来源**引用计数**、来源全部结束时解除（`AbilitySystemComponent.AreAbilityTagsBlocked(...)` 暴露当前阻挡集）。这与 `AbilityTagsToCancel` 不同——后者是打断已激活技能，而非门控后续激活。

> 📦 **完整范例**：**Playable Demo** 的 `DemoConfig.asset` 里有一份配好的 `AbilityInteractionRules`——持续型*专注*技能 block 近战（`AbilityTagsToBlock`）、开火远程 cancel 专注（`AbilityTagsToCancel`）。导入示例打开即可看/抄。

---

### 7.6 事件 / 标签触发激活（AbilityTriggers）

技能可以不经显式 `TryActivate` 自动激活——往它的 **AbilityTriggers** 列表加条目，每条是 `TriggerTag` + `TriggerSource`：

- **GameplayEvent** —— `asc.SendGameplayEvent(tag)` 发来匹配标签时激活（层级匹配：`Event.Combat.Hit` 事件会触发监听 `Event.Combat` 的技能）。事件的 `GameplayEventData` 作为技能的 `triggerData` 传入。
- **OwnedTagAdded** —— 拥有者获得该标签时激活一次（计数 0→有；1→2 不会重复触发）。
- **OwnedTagPresent** —— 标签出现时激活，标签消失时**自动取消**技能（把技能生命周期绑到标签存在期间）。

```csharp
myAbility.AbilityTriggers.Add(new AbilityTrigger {
    TriggerTag = GameplayTag.RequestTag("Event.Combat.HitConfirmed"),
    TriggerSource = EGameplayAbilityTriggerSource.GameplayEvent,
});
// …授予后，asc.SendGameplayEvent("Event.Combat.HitConfirmed") 即自动激活它。
```

触发激活仍走正常的 `CanActivate` 检查（消耗/冷却/阻挡标签）。这是做反应式技能的内置方式——比"授予一个在 `WaitGameplayEvent` 上循环的被动"更干净（§18.3）。

---

## 8. AbilitySystemComponent 速查

```csharp
// 属性
asc.AddAttributeSet(new AS_Health());
var h = asc.GetAttributeSet<AS_Health>();
asc.ApplyModToAttributeBase(h.HealthAttribute, EAttributeModifierOp.Add, +10f); // 直接改基础值

// 标签
asc.AddLooseGameplayTag(tag); asc.RemoveLooseGameplayTag(tag);
asc.HasMatchingGameplayTag(tag);
var owned = new GameplayTagContainer(); asc.GetOwnedGameplayTags(owned);

// 技能
var handle = asc.GiveAbility(template); asc.TryActivateAbility(handle);
asc.TryActivateAbilitiesByTag(tag);
asc.ClearAbility(handle);
IReadOnlyCollection<GameplayAbilitySpec> granted = asc.GetGrantedAbilities(); // 列技能栏

// 效果
var ge = asc.ApplyGameplayEffectToSelf(effect, level); // 返回激活句柄（瞬时效果句柄无效）
asc.ApplyGameplayEffectSpecToSelf(spec);
asc.RemoveActiveGameplayEffect(ge);
asc.GetCooldownRemainingForTags(cooldownTags, out float remain, out float dur);

// 激活效果只读枚举（列 buff/debuff、读剩余时长）
IReadOnlyList<ActiveGameplayEffect> actives = asc.GetActiveGameplayEffects();
ActiveGameplayEffect one = asc.GetActiveGameplayEffect(ge); // 按句柄取，读 one.TimeRemaining

// 事件
asc.OnAttributeChanged += d => { /* d.Attribute / d.OldValue / d.NewValue / d.Source */ };
asc.OnAbilityActivated += ability => { };
asc.OnAbilityEnded += (ability, cancelled) => { };
asc.OnTagChanged += (tag, present) => { };
asc.OnActiveEffectAdded += active => { };   // 持续/永久效果登记时（瞬时不触发）
asc.OnActiveEffectRemoved += active => { }; // 到期/显式移除/被顶替时
asc.OnActiveEffectStackChanged += (active, oldN, newN) => { }; // 叠层层数变化（刷 ×N 角标）
asc.OnAbilityGiven += spec => { };          // 技能授予（含 loadout/全局批量）
asc.OnAbilityRemoved += spec => { };        // 技能移除（在销毁前回调，spec 仍可读）

// 游戏事件 / 表现
asc.SendGameplayEvent(eventTag, data);
asc.ExecuteGameplayCue(cueTag, cueParams);
```

---

## 9. 输入分发（状态驱动按键多态）

这是框架的招牌：**同一个按键，在不同角色状态下触发不同技能**（如 X 键：冲刺时滑铲、否则下蹲）。

### 9.1 组件

`InputSystemComponent`（挂在角色上）。它不绑定具体输入后端，只认 `ReceiveInput(inputTag, triggerEvent, data)`：

```csharp
inputSys.ReceiveInput(
    GameplayTag.RequestTag("InputTag.Crouch"),
    InputTriggerEvent.Started,
    InputActionData.Empty);
```

用 Unity Input System 时，在 `InputConfig` 资产的 `InputActionMappings` 配映射（每条：`InputTag` → 一个 `InputActionReference`），把该 `InputConfig` 赋给 `InputSystemComponent`。启用时它自动订阅每个动作的 started/performed/canceled 并派发 `ReceiveInput`——无需额外组件。

#### 端到端：从一个键到一个技能

"按键 → tag → 技能"是**两段映射**，分两处配（中间用 InputTag 解耦）：

1. **按键 → InputTag**：在 `InputConfig` 资产的 `InputActionMappings` 加一条 = 一个 `InputActionReference`（你 `.inputactions` 资产里的某个动作，如 *Crouch*）→ 一个 `InputTag`（如 `InputTag.Crouch`）。把该 `InputConfig` 赋给角色的 `InputSystemComponent`，启用时自动绑定。
2. **InputTag → 技能**：在 `InputControlSetup` 的 `inputProcessors` 里加一个 `InputProcessor_ActivateAbilityByTag`，设 `InputTags = InputTag.Crouch`、`AbilityTag = Ability.Crouch`（按技能资产的 `AbilityTags` 匹配，内部走 `TryActivateAbilitiesByTag`）。
3. **挂上去**：把该 `InputControlSetup` 放进 `InputSystemComponent` 的 `inputControlSetups`。

运行时：按下键 → `InputSystemComponent` 派发 `InputTag.Crouch` → 当前控制集筛出监听它的处理器 → 激活 `Ability.Crouch`。要"同键多态"，见下方 9.2 的滑铲/下蹲表。

### 9.2 控制集与多态

`InputControlSetup`（*Create → Sigil → GAS → Input Control Setup*）持有：
- **检查器 InputChecker**（门控，全部通过才放行；`InputChecker_TagRelationship` 按状态放行/拦截）
- **处理器 InputProcessor**（多态分发落点）
- **ExecutionType**：`FirstOnly`（只执行第一个通过的——多态关键）/ `MatchAll`

**配"X 键 = 冲刺时滑铲 / 否则下蹲"**：在一个 InputControlSetup 里，`ExecutionType = FirstOnly`，加两个 `InputProcessor_ActivateAbilityByTag`，监听同一个 InputTag：

| 顺序 | 处理器 | StateQuery | AbilityTag |
|---|---|---|---|
| [0] 滑铲（排前） | InputProcessor_ActivateAbilityByTag | `MatchAllTags(Movement.State.Sprint)` | `Ability.Slide` |
| [1] 下蹲（排后） | InputProcessor_ActivateAbilityByTag | （空） | `Ability.Crouch` |

冲刺中按键 → [0] 状态条件通过 → 滑铲，`FirstOnly` 返回；非冲刺 → [0] 不通过 → [1] 下蹲。

**同一套路、按武器门控（一个近战键 → 不同武器不同技能）**——`StateQuery` 改查武器标签即可。`WeaponComponent`（在 combat 配套包里）装备时把 `Weapon.Sword` / `Weapon.Axe` 注入 ASC，于是：

| 顺序 | InputTags | StateQuery | AbilityTag |
|---|---|---|---|
| [0] | `InputTag.Melee` | `MatchAllTags(Weapon.Sword)` | `Ability.MeleeAttack`（轻击） |
| [1] | `InputTag.Melee` | `MatchAllTags(Weapon.Axe)` | `Ability.HeavyAttack`（重击） |

装备剑 → 只有 [0] 通过 → 轻击；换斧 → 只有 [1] 通过 → 重击。这就是"武器 → 不同技能"，纯靠数据配。

> 📦 **想看一套完整能跑的配置？** 导入 **Playable Demo** 示例，打开它的 `DemoConfig.asset`——里面有接好线的 `InputControlSetup`（正是这套近战多态 + 远程 + 专注，外加一套载具键位），可在 Inspector 里看、抄、改。

#### 切换整套键位（载具 / 瞄准 / UI）

`InputSystemComponent` 内部维护一个**控制集栈**，当前生效的永远是栈顶：

```csharp
inputSys.PushInputSetup(vehicleSetup);  // 进载具：压一套载具键位，立即生效
inputSys.PopInputSetup();               // 下载具：弹出，自动回到上一套
```

**不需要手动"重新读取/reload 配置"**——`ReceiveInput` 每次都派发到栈顶 `GetCurrentInputSetup()`，所以 push 一套新 `InputControlSetup` 后整套键位（门控检查器 + 处理器）就换了，pop 后原样恢复上一套。典型用法：进载具 push 载具控制集（同一个物理键 `InputTag.Brake` 现在映射到刹车技能而非角色技能）、进 UI push 一套只放行 UI 输入的控制集（其 `EnableInputBuffer` 通常关掉）。`PopInputSetup` 在仅剩一套时不操作（底层基础集不会被弹光）。

> 这套"冲刺状态"从哪来？由[移动系统](#11-移动--locomotion)把 `Movement.State.Sprint` 写到 ASC 标签上——这就是"状态总线"。

### 9.3 输入缓冲（连招预输入）

`InputConfig` 里定义缓冲窗口；攻击动画用 Animation Event 调 `inputSys.OpenInputBufferWindow(tag)` 开窗，窗口期内被拦的输入存入缓冲，`CloseInputBufferWindow` 时重放。

---

## 10. 战斗（近战/远程）—— 在配套包

> 📦 **近战与远程战斗已拆成独立配套包** `com.likeon.gas.combat`（GitHub：[sigil-combat](https://github.com/forestlii/sigil-combat)），**不在核心包里**。
>
> 为什么：战斗是搭在能力系统之上的一个*领域*、不是核心机制——拆出去让 GAS 核心保持为纯能力系统。命名空间不变（`Likeon.GAS`），和核心一起装即可。它与移动配套包平级、互不依赖。

配套包提供：`AttackDefinition` / `AttackApplication`（命中施加什么）、`MeleeAttackTrace` / `CollisionTrace`（动画事件驱动的命中检测）、`CombatSystemComponent` + `CombatFlow` 管线（`AttackRequest` → `AttackResult` → `AttackResultProcessor`）、`PoiseComponent`（韧性/破防）、`TargetingSystemComponent`（锁定）、`IWeapon` / `WeaponComponent`（武器标签注入实现"不同武器不同技能"）、`BulletDefinition` / `BulletInstance` / `BulletLauncher`（投射物）、`DamageExecutionCalculation`（减伤+格挡）、`CombatTeamAgent`（阵营），以及 `ICombatInterface` 契约。它按**名字**解析属性，与你的 codegen 属性集组合即可。

👉 **完整细节见配套包的使用文档**：`com.likeon.gas.combat/Documentation~/Usage.zh-CN.md`。旗舰 **Playable Demo** 也随该包交付。

---

## 11. 移动 / Locomotion —— 在配套包

> 📦 **移动与运动动画已拆为独立配套包** `com.likeon.gas.movement`（GitHub: [sigil-movement](https://github.com/forestlii/sigil-movement)），**不在核心包内**。
>
> 理由：移动是 GameplayTag 状态总线的*消费方*、非能力系统本身——拆出让 GAS 核心专注。命名空间不变（`Likeon.GAS`），与核心一起装即可。核心也可单独配你自己的移动方案。

配套包提供：`MovementSystemComponent` / `CharacterMovementSystemComponent`（标签驱动的 CharacterController 移动状态机，状态可镜像到 ASC，驱动"冲刺→滑铲"这类[输入多态](#9-输入分发状态驱动按键多态)）、`LocomotionAnimationDriver` + `LocomotionMath`（运动动画数据层 → Animator 参数）、`MovementDefinition` / `MovementSettings` / `MovementTags`、示例分层 Animator Controller 生成器。

👉 **完整用法见配套包的使用文档**：`com.likeon.gas.movement/Documentation~/Usage.zh-CN.md`。

---

## 12. 表现层（Cue / 上下文特效 / 相机）

### 12.1 GameplayCue（tag 驱动表现）

效果施加时自动触发 cue（GE 的 **GameplayCues** 字段）：Instant→`Executed`，Duration/Infinite→施加 `OnActive`、移除 `Removed`。

写一个 cue 处理器：`GameplayCueNotify_Static`（*Create → Sigil → GAS → Gameplay Cue Notify (Static)*），配粒子 + 音效 + 响应的 CueTag（层级匹配）。注册：

```csharp
GameplayCueManager.Instance.RegisterCueNotify(myCueNotify);
asc.ExecuteGameplayCue(GameplayTag.RequestTag("GameplayCue.Hit"),
    GameplayCueParameters.At(hitPoint, hitNormal));
// 也可订阅 GameplayCueManager.Instance.OnGameplayCue 自己处理
```

**有状态 cue**（`GameplayCueNotify_Actor`，*Create → Sigil → GAS → Gameplay Cue Notify (Actor)*）—— 用于需要贯穿整个 Duration/Infinite 效果的持久表现（buff 光环、引导光束、DoT 粒子），而不是每帧重播一次性表现。`OnActive` 时 spawn **一个**挂在 target 上的实例，走 `OnActive → WhileActive（逐帧）→ OnRemove`；效果移除时销毁它。填个 `SpawnPrefab` 即可零代码得到 loop 特效/光环，或子类重写钩子做动态行为（跟随、按剩余时间淡出）。`GameplayCueManager` 按 `(notify, target)` 各保留一个活跃实例。

### 12.2 上下文特效（脚步/受击按表面）

在世界物体上挂 `SurfaceType`（标 `SurfaceType.Grass` 等）。角色挂 `ContextEffectComponent`，配 `ContextEffectsLibrary`（效果标签 + 情景标签 → 音效/粒子）。

```csharp
// 脚步：从地面射线命中播放，自动按表面选音效
ctxFx.PlayContextEffectFromHit(GameplayTag.RequestTag("ContextEffect.Footstep"), groundHit);
ctxFx.PlayContextEffect(effectTag, location, surfaceTag); // 也可直接给表面
```

### 12.3 相机模式栈

`CameraSystemComponent` 驱动 Unity Camera。默认第三人称（`CameraMode_ThirdPerson`，含 SphereCast 穿透规避）。

```csharp
var cam = gameObject.AddComponent<CameraSystemComponent>();
var mode = new CameraMode_ThirdPerson { ArmLength = 5f, PivotOffset = new Vector3(0,1.6f,0) };
cam.Configure(Camera.main, playerTransform, mode);
mode.AddLookInput(mouseDeltaX, -mouseDeltaY); // 鼠标看

// 进入瞄准：压一套新模式（按 BlendTime 平滑混入），退出弹出
cam.PushCameraMode(aimMode);
cam.PopCameraMode(aimMode);
```

---

## 13. 编辑器工具

- **GameplayTag 选择器**：所有 `GameplayTag` 字段变成层级下拉 + 搜索，旁边 `+` 一键新增标签。
- **GameplayTagContainer 多选**：列出已含标签（可删）+ 下拉去重添加。
- **标签注册表 `GameplayTagsSettings`** + 顶部菜单窗口 **`Sigil ▸ GAS ▸ Gameplay Tags`**：集中增删标签（下拉候选来源），窗口内也有"扫描工程补标签"按钮。插件编辑器入口统一在 **Likeon** 菜单下，不挂 Project Settings。
- **标签扫描**：菜单 `Sigil ▸ GAS ▸ Scan Project for Gameplay Tags`，扫工程里 `RequestTag("...")` 字面量一键补进注册表。
- **增强 Inspector**：GameplayEffect / GameplayAbility / AbilityLoadout 带摘要与配置校验提示（combat 配套包另附一个 `AttackDefinition` inspector）。`AbilityLoadout` 的属性集列表和 `InputControlSetup` 的检查器/处理器列表都按子类型下拉添加 `[SerializeReference]` 项。
- **属性集代码生成**：`AttributeSetDefinition` 资产 + 其「生成 C#」按钮，不用手写 C# 就能定义属性集——见 [§5.1](#51-在编辑器里定义属性集不用手写-c)。
- **GameplayTag 常量生成**：菜单 `Sigil ▸ GAS ▸ Generate Gameplay Tag Constants` 把标签注册表变成一个嵌套静态类（`Game.GameplayTags.Movement.State.Sprint`，既是标签又是父级的节点带 `Self`），代码就能强类型引用标签、替代 `RequestTag("…")`——从注册表自动同步，不用手维护常量文件。

### 13.1 运行时调试器 GAS Debugger

菜单 **`Sigil ▸ GAS ▸ GAS Debugger`**。Play Mode 下回答"技能为什么放不出来 / 这个 buff 到底挂上没有"这类问题：

- **选目标**：在 Hierarchy/Scene 里选中任意 GameObject 即可——在它自己和父链上找 `AbilitySystemComponent`（选中角色的武器骨骼也能定位到宿主）；或用工具栏下拉直接挑场景里任意一个 ASC（不限于主角）。`Lock` 锁定当前目标后随便点别的不跳，`Ping` 在 Hierarchy 里高亮它。
- **Attributes**：按属性集分组列出每个属性的 Base / Current。刚变过的行闪黄；Current ≠ Base（有临时修改器在生效）时 Current 高亮。
- **Owned Tags**：当前拥有的标签及计数（如 `State.Sprinting ×2`——两个来源都挂了它）。
- **Abilities**：已授予技能的激活状态（Active/Blocked）、激活组、冷却剩余进度条。
- **Active Effects**：每个存活效果的剩余时长进度条、层数 ×N、周期倒计时、授予标签；被 Ongoing 条件抑制的整行淡显并标 `(inhibited)`。
- **Event Log**：窗口开着时滚动记录该 ASC 的事件流——技能激活 / **失败（带原因枚举）** / 结束、授予/移除、效果增删/叠层、属性变更（带来源技能/发起者）、GameplayEvent、标签增减。排查"按了没反应"直接看 Failed 行的原因。

纯 Editor 工具（在 `Likeon.GAS.Editor` 程序集），打包产物零开销。

---

## 14. 联网

当前版本为**单机权威逻辑**。网络复制 / 预测**未实现**，规划为后续阶段。所有"会改状态"的入口集中在 `AbilitySystemComponent`，便于届时统一接 authority 判断。

> 工作量评估、拆桶与两大未决战略点（要不要做客户端预测、网络库选型 NGO 待重新评估）已存档在仓库的 `MD/design/阶段6-联网-工作量评估.md`（开发档，非发布包内）。

---

## 15. 常见问题

> 💡 **排查前先开调试器**：`Sigil ▸ GAS ▸ GAS Debugger`（[§13.1](#131-运行时调试器-gas-debugger)）选中出问题的角色——下面大多数问题它一眼就能定位。

**Q：技能激活不了？**
**开调试器看 Event Log 的 `Failed` 行——失败原因枚举直接告诉你**（RequiredTags 缺 / BlockedTags 挡 / Cost 不足 / 冷却中 / 激活组互斥 / 被其它技能 block）。人肉核对清单：① AbilityTags 配了吗（按标签激活需要）；② ActivationRequiredTags/BlockedTags 是否被当前状态挡住（Owned Tags 面板看当前标签）；③ CostEffect 是否扣不起；④ 是否被冷却或激活组互斥挡住（Abilities 面板看冷却条/Blocked 标记）。

**Q：伤害没生效？**
调试器 Attributes 面板盯着 `IncomingDamage`/`Health`（变更行会闪黄）打一下就知道断在哪层。确认伤害 GE 改的是 `AS_Health.IncomingDamage`（不是直接改 Health），目标 ASC 有 `AS_Health`，且 SetByCaller 标签和 GE 里配的一致。

**Q："冲刺时滑铲"不触发？**
确认移动系统已把 `Movement.State.Sprint` 镜像到 ASC（同物体有 ASC 时自动；否则手动 `move.SetGameplayTagsProvider(asc)`），且滑铲处理器排在下蹲前、ExecutionType=FirstOnly。

**Q：近战判定没命中？**
确认：① Animation Event 调了 `BeginAttackTrace`/`EndAttackTrace`；② 判定点 socket Transform 配在 Entries 里；③ 目标有 Collider + ASC；④ 双方 `CombatTeamAgent` 是敌对（不同 TeamId）。

**Q：标签下拉是空的？**
打开顶部菜单 *Sigil ▸ GAS ▸ Gameplay Tags* 窗口加标签，或跑一次 *Sigil ▸ GAS ▸ Scan Project for Gameplay Tags*（窗口里也有该按钮）。

---

## 16. 进阶系统（本版新增）

### 16.1 技能任务 AbilityTask（协程异步）

技能里要"等一会 / 等事件 / 边播动画边等命中"时用 AbilityTask（协程封装，技能结束自动取消，防悬挂协程）：

```csharp
protected override void OnActivateAbility(GameplayEventData triggerData)
{
    // 播放 Animator 状态并等其结束，同时监听匹配的命中事件
    var t = AbilityTask_PlayMontageAndWaitForEvent.PlayMontageAndWaitForEvent(
        this, animator, "Attack01", duration: 0.8f, eventTags: hitTags);
    t.OnEventReceived += (tag, data) => { /* 命中确认：施伤 / 连段 */ };
    t.OnCompleted     += () => EndAbility();
    t.OnCancelled     += () => EndAbility(true);
    t.Activate();
}
```

其它任务：`AbilityTask_WaitDelay`（延时）、`WaitDelayOneFrame`（一帧）、`AbilityTask_WaitGameplayEvent`（等事件）、`AbilityTask_WaitInputPress`（等输入，可标签门控）、`AbilityTask_WaitTargetData`（驱动 TargetActor 采集目标）。

> `PlayMontageAndWaitForEvent` 的 `animator` 可留空——此时只按 `duration` 计时驱动 5 个回调（OnCompleted / OnBlendOut / OnInterrupted / OnCancelled / OnEventReceived），便于纯逻辑/测试。

> **自己写任务**（0.7.1 起任何程序集都能写）：继承 `AbilityTask`，加个静态工厂 `new MyTask{…}; task.InitTask(ability); return task;`，在 `OnActivate()` 里干活（每帧循环用 `RunCoroutine(...)`），在 `OnDestroy(bool)` 里清理。技能结束时框架自动取消任务，所以 `OnDestroy` 一定会跑——凡是"生命周期该跟技能走"的东西都适合（跟随光标、蓄力循环、生成的指示物）。combat 配套包的 `AbilityTask_GroundReticle`（火球地面光标）就是范例。

### 16.2 全局技能/效果 GlobalAbilitySystem

把一个技能/效果一次性施加到**所有已注册 ASC**（全场 buff/debuff、环境效果、阶段技能）：

```csharp
// 角色挂 GlobalAbilitySystemRegistrant 组件即自动注册（或手动 GlobalAbilitySystem.Instance.RegisterASC）
GlobalAbilitySystem.Instance.ApplyEffectToAll(poisonAuraEffect);  // 全场中毒
GlobalAbilitySystem.Instance.ApplyAbilityToAll(emoteAbility);     // 全员获得技能
GlobalAbilitySystem.Instance.RemoveEffectFromAll(poisonAuraEffect);
```

后注册的 ASC 会自动补上所有已全局应用的项；同一项重复施加幂等。

### 16.3 游戏阶段 GamePhaseSubsystem

用层级 GameplayTag 管理嵌套游戏阶段——**父子阶段共存、兄弟阶段互斥**：

```csharp
// 阶段是 GamePhaseAbility 资产（Create → Sigil → GAS → Game Phase Ability），设 GamePhaseTag
GamePhaseSubsystem.Instance.StartPhase(gameAsc, playingPhase);    // Game.Playing
GamePhaseSubsystem.Instance.StartPhase(gameAsc, warmUpPhase);     // Game.Playing.WarmUp（与父共存）
GamePhaseSubsystem.Instance.StartPhase(gameAsc, postGamePhase);   // 结束兄弟 WarmUp，保留父 Game.Playing

GamePhaseSubsystem.Instance.WhenPhaseStartsOrIsActive(
    GameplayTag.RequestTag("Game.Playing"), EPhaseTagMatchType.PartialMatch,
    tag => { /* 进入 Playing 任意子阶段时 */ });
bool inPlaying = GamePhaseSubsystem.Instance.IsPhaseActive(GameplayTag.RequestTag("Game.Playing"));
```

### 16.4 通用碰撞检测 CollisionTrace —— 在 combat 配套包

> 📦 `CollisionTrace`（及 §16.5 `MovementCancellation`）现在在 **combat 配套包** `com.likeon.gas.combat` 里，不在核心。

非近战的通用碰撞命中（陷阱、AOE 区域、环境伤害区）。区别于 `MeleeAttackTrace`（绑 AttackDefinition 施伤）：CollisionTrace 只产出命中事件、施伤由你决定：

```csharp
var trace = obj.AddComponent<CollisionTrace>();
trace.SetSockets(point1, point2);          // 检测点
trace.Radius = 0.5f;
trace.HitFilter = go => IsEnemy(go);        // 可选过滤（阵营等）
trace.OnHit += col => ApplyDamage(col.gameObject);
trace.ToggleTraceState(true);               // 开启（每次激活内每目标只命中一次）
```

### 16.5 移动取消 MovementCancellation

带 root motion 位移的攻击动画，允许玩家移动"取消"位移（动作游戏手感）。Animation Event 在窗口起止调 `BeginWindow`/`EndWindow`，窗口期玩家移动则切掉 `Animator.applyRootMotion`：

```csharp
var mc = character.AddComponent<MovementCancellation>(); // 自动找 Animator + CharacterController
// 动画 Animation Event 调用：位移段起点 → mc.BeginWindow()；段终点 → mc.EndWindow()
```

---

## 17. 接 UI（订阅事件，不绑 UI 方案）

Sigil **只做逻辑、不含 UI 框架**——它把"会变的东西"以事件广播出来，你用任意 UI 方案（UGUI / UI Toolkit / 第三方）订阅渲染。画 HUD 是宿主的事，Sigil 负责暴露数据。常见对应：

| UI 想画 | 订阅 |
|---|---|
| 血条/法力/耐力条 | `asc.OnAttributeChanged(AttributeChangeData)` —— `.Attribute/.OldValue/.NewValue/.Source` |
| 状态图标（眩晕/buff 有无） | `asc.OnTagChanged(tag, present)` |
| buff/debuff 图标条（带倒计时 + ×N 层数） | `asc.OnActiveEffectAdded/Removed/StackChanged` + 轮询 `asc.GetActiveGameplayEffects()` 读 `TimeRemaining` / `StackCount` |
| 技能冷却填充 | 轮询 `asc.GetCooldownRemainingForTags(...)` |
| 技能栏（loadout 驱动增删） | `asc.OnAbilityGiven / OnAbilityRemoved` + `asc.GetGrantedAbilities()` |
| 技能激活反馈 | `asc.OnAbilityActivated / OnAbilityEnded` |
| 伤害飘字 | `combat.OnDealtDamage / OnAttackResultReceived` |
| 削韧条 | `poise.OnPoiseBroken / OnPoiseRecovered` |
| 锁定标记 | `targeting.OnTargetLockOn / OnTargetLockOff` |
| 武器图标 | `weapon.OnEquipped / OnUnequipped / OnWeaponActiveStateChanged / OnTargetingChanged` |

buff 图标条示例（订阅增减 + 每帧刷剩余时长）：

```csharp
void OnEnable()  { asc.OnActiveEffectAdded += Add; asc.OnActiveEffectRemoved += Remove; }
void OnDisable() { asc.OnActiveEffectAdded -= Add; asc.OnActiveEffectRemoved -= Remove; }

void Add(ActiveGameplayEffect e)    { /* 实例化一个图标，记下 e */ }
void Remove(ActiveGameplayEffect e) { /* 销毁对应图标 */ }

void Update() // 刷倒计时
{
    foreach (var e in asc.GetActiveGameplayEffects())
        UpdateIcon(e, e.TimeRemaining); // Infinite 为正无穷，自行判断隐藏倒计时
}
```

> 注：瞬时效果（Instant）不产生激活实例，故 **不触发** `OnActiveEffectAdded`，也不进枚举——它的影响通过 `OnAttributeChanged`（如扣血）体现。

---

## 18. 常用配方 Recipes

> 用现有零件拼出的常见玩法模式——每个都是"字段/API 已在前文介绍过、这里教怎么组合"。

### 18.1 免疫（挡效果施加）

给伤害 GE 设 **ApplicationBlockedTags** = `Status.Immune`。目标身上有该标签时，效果**施加直接失败**（不是施加后再抵消）。免疫状态本身用一个 Duration GE 发：`GrantedTags = [Status.Immune]`，持续期间自动挂标签、到期自动摘。

```csharp
asc.ApplyGameplayEffectToSelf(immunityBuff);   // 5 秒免疫（Duration GE，GrantedTags=Status.Immune）
// 期间任何 ApplicationBlockedTags 含 Status.Immune 的 GE 都施加失败
```

### 18.2 沉默 / 眩晕（挡技能激活）

反过来挡技能：给技能设 **ActivationBlockedTags** = `Status.Silenced`。沉默 debuff = Duration GE（`GrantedTags = [Status.Silenced]`）。持续期间技能激活被拒（调试器 Event Log 显示 `Failed: ... (ActivationBlockedTags)`），到期自动恢复。要"沉默瞬间还打断正在施放的技能"，配合 §7.5 的 `AbilityTagsToCancel`。

### 18.3 被动 / 反应式技能（Proc）

"被打时反击 / 受惊跳起"这类：技能常驻激活监听事件——

```csharp
[CreateAssetMenu(menuName = "Game/Abilities/CounterAttack")]
public class GA_Counter : GameplayAbility
{
    // 资产上勾 ActivateOnGranted = true（授予即激活，常驻监听）
    protected override void OnActivateAbility(GameplayEventData triggerData)
    {
        var t = AbilityTask_WaitGameplayEvent.WaitGameplayEvent(
            this, GameplayTag.RequestTag("Event.Combat.WasHit")); // onlyTriggerOnce 默认 false=持续监听
        t.OnEventReceived += (tag, data) => { /* data.Instigator = 打我的人 → 反击 */ };
        t.Activate();
        // 不调 EndAbility——常驻
    }
}
```

事件由攻击方（或受击回调）发出：

```csharp
victimASC.SendGameplayEvent(GameplayTag.RequestTag("Event.Combat.WasHit"),
    new GameplayEventData { Instigator = attacker, EventMagnitude = damage });
```

### 18.4 蓄力技能（按住充能、松开释放）

按键**按下**激活技能，技能里等**松开**（`InputTriggerEvent.Canceled`），用等待时长放缩伤害：

```csharp
protected override void OnActivateAbility(GameplayEventData triggerData)
{
    if (!CommitAbility()) { EndAbility(true); return; }
    var t = AbilityTask_WaitInputPress.WaitInputPress(
        this, GameplayTag.RequestTag("InputTag.ChargedAttack"),
        triggerEvent: InputTriggerEvent.Canceled);           // 等松开
    t.OnPress += heldSeconds =>
    {
        float charge = Mathf.Clamp01(heldSeconds / maxChargeSeconds);
        var spec = ASC.MakeOutgoingSpec(damageEffect)
            .SetSetByCallerMagnitude(GameplayTag.RequestTag("Data.Damage"), baseDamage * (1f + charge));
        // …目标采集后 ApplyGameplayEffectSpecToSelf 到目标…
        EndAbility();
    };
    t.Activate();
}
```

要逐帧刷充能条，配合 `EnableTick` + `AbilityTick(dt)`（§7.1）。

### 18.5 多伤害类型（火/冰抗性）

**别**为每种元素开一个 Damage 属性。做法（Fortnite 同款模式）：伤害类型用**标签**表达，抗性属性按类型开：

1. 伤害 GE 的 **AssetTags** 标 `Damage.Type.Fire`（或运行时 `spec.AddDynamicAssetTags(...)` 注入——武器附魔场景）。
2. 自定义属性集加 `FireResistance` / `ColdResistance`。
3. 写一个 ExecutionCalculation，按 spec 的类型标签选对应抗性：

```csharp
public override void Execute(GameplayEffectSpec spec, AbilitySystemComponent src,
    AbilitySystemComponent tgt, List<GameplayExecutionOutput> outputs)
{
    float damage = spec.GetSetByCallerMagnitude(DamageTag, 0f);
    var resist = tgt.GetAttributeSet<AS_Resistances>();
    foreach (var t in spec.GetAllAssetTags())                       // 静态 AssetTags + 动态注入
        if (t.MatchesTag(GameplayTag.RequestTag("Damage.Type.Fire")) && resist != null)
            damage -= resist.FireResistance.CurrentValue;
    // …clamp、写入 IncomingDamage（参考 combat 配套包的 DamageExecutionCalculation）…
}
```

注意：这个模式下"总抗性"没有意义（火抗 30% + 冰抗 30% ≠ 总减伤 60%），UI 按类型分别显示。

### 18.6 伤害飘字

两条路（Playable Demo 用的是后者）：① 订阅 `combat.OnDealtDamage / OnAttackResultReceived`（§17 表）；② 走 Cue——`AS_Health` 的伤害管线结算处 `ExecuteGameplayCue(GameplayCue.Damage, params)`，`GameplayCueParameters.Magnitude` 带伤害值，cue 处理器/订阅方在命中点生成飘字。

---

## 19. 设计取舍与反模式

### 19.1 Instant vs Duration vs Infinite 怎么选

判断标准：**这次修改需要"被记住、以后能撤销/调整"吗？**

| 场景 | 用 | 原因 |
|---|---|---|
| 一次性伤害/治疗/扣资源 | **Instant** | 改 BaseValue，改完就完，无需追溯 |
| 限时 buff/debuff（5 秒加速） | **Duration** | 改 CurrentValue，到期自动回滚 |
| 装备加成 / 天赋 / 光环 | **Infinite** | 持续到显式移除，**换装备/洗点时能干净撤销** |
| DoT/HoT（每秒跳血） | Duration/Infinite + **Period** | 周期结算走 Instant 语义改 BaseValue |

**为什么装备/天赋别用 Instant——数字说话**：MaxHealth 基础 100，天赋一阶 +10%。
- 用 Instant：BaseValue 被永久写成 110；升二阶（设计意图"总共 +20%"）再乘 1.1 → **121，错**；且退天赋没法精确还原。
- 用 Infinite 修改器：BaseValue 恒 100，一阶挂 ×1.1 → Current 110；二阶换成 ×1.2 → **120，对**；移除效果 → 干净回 100。

### 19.2 SetByCaller vs Meta Attribute vs Execution 怎么选

三个都能"把运行时算的数送进结算"，分工不同：

| 机制 | 是什么 | 什么时候用 |
|---|---|---|
| **SetByCaller**（§6） | 施加时按标签塞一个 float 进 spec | 单个简单数值（本次攻击伤害、击退力度）——最轻，优先用 |
| **Meta Attribute**（§5） | 中间属性（IncomingDamage），结算后由属性集消费清零 | **多个来源要汇进同一个结果**再统一结算（普攻 GE、DoT、反伤都写 IncomingDamage，clamp/破盾逻辑只写一处） |
| **Execution**（§6） | 自定义计算类，多属性输入输出 | 公式要**读双方多个属性**（攻方 Damage、守方减伤、格挡状态）——最重也最强 |

三者常组合：Execution 读 SetByCaller 传入的基础值 + 双方属性 → 算出结果写 Meta Attribute（combat 配套包的 `DamageExecutionCalculation` 正是这条链）。

### 19.3 反模式清单（别这么干）

- ❌ **技能里不调 `CommitAbility()`** → Cost/Cooldown 静默失效（见 §7.1 警告框）。
- ❌ **一个 GameObject 挂多个 `AbilitySystemComponent`** → `GetComponent` 解析、移动系统标签镜像、调试器目标解析全都假设单 ASC，多挂行为未定义。一个角色 = 一个 ASC；属性再多用多个 AttributeSet 拆，不是多个 ASC。
- ❌ **绕过管线直接改属性**（`health.Health.CurrentValue = 50`）→ 跳过 PreAttributeChange 钳制、不触发事件、下次聚合重算会被覆盖。正路：GE，或代码侧 `ApplyModToAttributeBase`。
- ❌ **拿标签计数做玩法逻辑**（`GetOwnedGameplayTagCounts` 读到 ×3 就触发某机制）→ 计数是**多来源引用记账**（供调试器/UI），来源增减时序不保证；玩法判断只问有无：`HasMatchingGameplayTag`。
- ❌ **技能自监听自己的"停止"输入做开关** → `ReceiveInput` 先广播事件再走处理器分发，同一次按键会"先结束技能、又立刻被处理器重新激活"。开关型技能用 movement 包的 `InputProcessor_ToggleAbilityByTag`（切换判定收敛在处理器）。
- ❌ **包还没编译完就 Import Sample** → `[SerializeReference]` 资产（Loadout 属性集）会因类型解析不到被写坏。先等包 resolve + 编译结束再导。

---

## 20. 给 UE GAS 用户的迁移速查

从 Unreal GAS 过来的话，多数概念**同名同义**直接用；下表列差异与对应物：

| UE GAS | Sigil 对应 | 差异说明 |
|---|---|---|
| `UAbilitySystemComponent` | `AbilitySystemComponent`（MonoBehaviour） | 挂角色 GameObject 上 |
| `IAbilitySystemInterface` | `GetComponent` / `GetComponentInParent<AbilitySystemComponent>()` | Unity 惯用法，无需接口 |
| OwnerActor / AvatarActor 分离 | **合一**（ASC 所在 GameObject 即两者） | 单机语境简化；联网阶段再评估 |
| `AttributeSet` + `ATTRIBUTE_ACCESSORS` | `AttributeSet` 子类 + `Register()` 登记 | `PreAttributeChange` / `PostGameplayEffectExecute` 等钩子同名同义 |
| Meta Attribute（Damage） | 同（`AS_Health.IncomingDamage`） | 管线一致 |
| GE Instant / HasDuration / Infinite / Period | 同名同义 | — |
| `ModifierMagnitudeCalculation`（MMC） | **未提供** | 用 Execution 或 SetByCaller 覆盖 |
| `ExecutionCalculation` | `GameplayEffectExecutionCalculation` | ScriptableObject 资产 |
| SetByCaller / Stacking / GrantedTags / 施加条件标签 | 同名同义 | Stacking 含 AggregateByTarget/BySource |
| `FGameplayEffectContext` 子类 + `AllocGameplayEffectContext` | 直接 C# 继承 `GameplayEffectContext`，构造 `GameplayEffectSpec` 时传入 | 不需要全局 Alloc 钩子；便捷路径 `MakeOutgoingSpec` 用基类 |
| Instancing Policy 三态 | 恒 **InstancedPerActor**（授予即克隆） | UE 5.5 起 NonInstanced 也已弃用 |
| AbilityTriggers（事件标签触发技能） | `GameplayAbility.AbilityTriggers`（GameplayEvent / OwnedTagAdded / OwnedTagPresent） | 已内置，见 §7.6 |
| Input ID 枚举绑定 | `InputTag` + `InputConfig` + `InputProcessor` 多态 | 更接近 Lyra 的 Enhanced Input 方向（§9） |
| `CommitAbility` / Cost GE / Cooldown GE | 同名同义 | 冷却剩余：`GetCooldownRemainingForTags` |
| `GameplayCueNotify_Static` / `GameplayCueNotify_Actor` | 同（ScriptableObject） | 无状态与有状态（持久实例）两种形态均已提供，见 §12.1 |
| Gameplay Debugger（Shift+'） | `Sigil ▸ GAS ▸ GAS Debugger`（§13.1） | 选中任意 GameObject 检视 |
| Replication / Prediction / NetExecutionPolicy | **未实现**（单机权威） | 规划为后续阶段（§14） |

---

## 21. 编辑器速查（创建菜单 / 工具 / codegen）

下面除标注*要写代码*的外，全部**在编辑器里**配、零代码。战斗/移动的资产在配套包里（见 §10 / §11）。

### 数据资产 —— *Create → Sigil → GAS → …*（Project 窗口右键）

| 菜单项 | 是什么 |
|---|---|
| **Gameplay Effect** | 改属性/挂标签的效果（瞬时/持续/无限、周期、叠层、SetByCaller）。 |
| **Ability Loadout** | 批量授予"技能+效果+属性集"，整批撤销。 |
| **Ability Interaction Rules** | 数据驱动的技能间 阻挡/取消/激活准入（按标签、状态感知）。 |
| **Input Config** | 物理键 → `InputTag` 绑定。 |
| **Input Control Setup** | 输入处理器链（FirstOnly / MatchAll → 一键多态）。 |
| **Curve Table** | 按等级缩放数值的曲线**表**（属性初始化用）。 |
| **Gameplay Tags Settings** | 层级标签**注册表**。 |
| **Attribute Set Definition** | 声明属性集 → **codegen**（见下）。 |
| **Gameplay Cue Notify (Static)** | 标签驱动的 VFX/SFX 表现。 |
| **Surface Effect Library** | 表面类型 → 音效/粒子 映射。 |
| **Target Source（Self / Event Data）** | 技能目标的来源。 |
| **Game Phase Ability** | 嵌套阶段的游戏阶段技能。 |

### 需写代码的类型 —— 一键空子类模板（*Assets → Create → Sigil → …*）

这几类要写逻辑，框架给空子类脚手架（对好基类 + override 桩 + `[CreateAssetMenu]`）：
**Ability (C# Script)** → `GameplayAbility`；**Cue Notify (C# Script)** → `GameplayCueNotify`；**Execution Calculation (C# Script)** → `GameplayEffectExecutionCalculation`。生成后就像上面的数据资产一样建实例。

### 编辑器工具 / 窗口 —— 顶部 *Sigil ▸ GAS ▸ …*

| 菜单 | 作用 |
|---|---|
| **Gameplay Tags** | 标签注册表窗口（层级下拉+搜索+新增标签，零代码）。 |
| **GAS Debugger** | Play 模式实时查看任意 ASC —— 属性/标签/技能/激活效果 + 事件日志。 |
| **Generate Gameplay Tag Constants** | 读注册表 → 生成强类型 tag 常量类。 |
| **Scan Project for Gameplay Tags** | 扫源码里的 `RequestTag("…")` 字面量补进注册表。 |
| **Documentation** | 打开本文档。 |

### ⭐ 属性集 codegen（属性不用写 C++/C#）

在 **Attribute Set Definition** 资产的 Inspector 里声明属性（名字/默认值/钳制/Max/Meta），点 **「Generate C#」** → 生成可编译的 `AttributeSet` 子类。数据资产=唯一真源、单向生成（资产→`.g.cs`，只读）、逻辑写手写 partial。见 [§5.1](#51-在编辑器里定义属性集不用手写-c)。

### 增强编辑 UI

- **GameplayTag / GameplayTagContainer** 字段绘制层级下拉 + 搜索 + 一键新增标签。
- **增强 Inspector**（带摘要+校验提示）：Gameplay Effect、Gameplay Ability、Ability Loadout、Input Control Setup、Attribute Set Definition（combat 包里还有 Attack Definition）。
- **SerializeReference 子类下拉**：给 `Ability Loadout` 的属性集列表、`Input Control Setup` 的处理器/检查器列表按类型添加条目。

---

*本文档随框架版本更新。问题反馈与版本管理见项目仓库。*
