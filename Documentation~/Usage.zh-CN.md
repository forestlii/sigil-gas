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
10. [近战战斗](#10-近战战斗)
11. [移动 / Locomotion（配套包）](#11-移动--locomotion-在配套包)
12. [表现层（Cue / 上下文特效 / 相机）](#12-表现层cue--上下文特效--相机)
13. [编辑器工具](#13-编辑器工具)
14. [联网](#14-联网)
15. [常见问题](#15-常见问题)
16. [进阶系统（本版新增）](#16-进阶系统本版新增)
17. [接 UI（订阅事件）](#17-接-ui订阅事件不绑-ui-方案)

---

## 1. 安装

**方式 A：本地包**
把 `com.likeon.gas` 文件夹放进工程的 `Packages/` 目录，或 Package Manager → `+` → *Add package from disk* → 选 `package.json`。

**方式 B：Git URL**
Package Manager → `+` → *Add package from git URL* → 填仓库地址。

**依赖**：包声明依赖 **Input System**（`com.unity.inputsystem`），安装 Sigil 时会自动一起装上（供 Playable Demo 与可选的 `UnityInputBinder` 使用）。
> 架构上**核心仍与输入解耦**：`Likeon.GAS.Runtime` 程序集不引用 Input System，核心 API（`ReceiveInput` / 技能 / 效果 / 移动）不依赖它；`UnityInputBinder`（`Likeon.GAS.UnityInput`）与 Demo 的程序集带 `ENABLE_INPUT_SYSTEM` 约束，未启用 Input System 时静默不编译，不影响核心。
> ⚠️ 跑 Demo 还需把 **Project Settings ▸ Player ▸ Active Input Handling** 设为 *Input System Package*（或 *Both*），否则新输入系统读不到键鼠。

**程序集**：`Likeon.GAS.Runtime`（核心）、`Likeon.GAS.UnityInput`（可选输入适配器）、`Likeon.GAS.Editor`（编辑器工具）。

**配套包（可选）**：移动与运动动画在独立包 `com.likeon.gas.movement`（[sigil-movement](https://github.com/forestlii/sigil-movement)），依赖核心、命名空间不变。需要移动就和核心一起装（见 §11）。

---

## 2. 核心概念一图流

```
[输入] --InputTag--> [InputSystemComponent] --门控/多态分发--> 激活
                                                              │
                                                              ▼
[GameplayAbility] --(改属性靠)--> [GameplayEffect] --> [AttributeSet 属性]
       │                                                      ▲
       ├-- 命中判定 [MeleeAttackTrace] --> [AttackDefinition] -┘
       ├-- 驱动 [MovementSystemComponent]（状态写回 ASC 标签 = 状态总线）
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
        ASC.AddAttributeSet(new AS_Health());   // 内置：Health / MaxHealth / IncomingDamage / IncomingHealing
        ASC.AddAttributeSet(new AS_Stamina());

        // 监听血量变化（驱动血条 UI）
        ASC.OnAttributeChanged += (attr, oldV, newV) =>
            Debug.Log($"{attr} : {oldV} -> {newV}");
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

内置属性集：`AS_Health`、`AS_Stamina`、`AS_Mana`、`AS_Combat`。每个属性是 `GameplayAttributeData`（区分 `BaseValue` 永久值 / `CurrentValue` 含临时增益的当前值）。

```csharp
var health = asc.GetAttributeSet<AS_Health>();
float hp = health.Health.CurrentValue;
GameplayAttribute hpAttr = health.HealthAttribute; // 属性句柄，给 GE 的修改器用
```

**伤害 Meta 管线**：不要直接改 `Health`。伤害打进中间属性 `IncomingDamage`，`AS_Health` 在结算后把它清零并映射成 `-Health`（自动 clamp 到 `[0, MaxHealth]`）。治疗同理走 `IncomingHealing`。

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

---

## 6. 效果 Gameplay Effects

`GameplayEffect` 是 ScriptableObject：*Create → Likeon → GAS → Gameplay Effect*。

**关键字段**：
- **DurationType**：`Instant`（改基础值，如一次伤害）/ `HasDuration`（限时改当前值）/ `Infinite`（持续到手动移除）
- **Period**：>0 则周期结算（如每秒中毒）
- **Modifiers**：每条 = 目标属性 + 运算符（Add/Multiply/Divide/Override）+ 修改量
  - 修改量来源：`ScalableFloat`（基础 + 每级增量）或 `SetByCaller`（运行时按标签传入）
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

**自定义伤害公式**（执行计算）——继承 `GameplayEffectExecutionCalculation`，框架已附带示例 `DamageExecutionCalculation`（`AS_Combat.Damage − 减伤 → IncomingDamage`）。把它作为资产放进 GE 的 *Executions*。

---

## 7. 技能 Abilities

### 7.1 写技能

继承 `GameplayAbility`，重写 `OnActivateAbility`。常用字段（Inspector 配）：
- **AbilityTags**：身份标签（用于按标签激活 / 关系匹配）
- **ActivationGroup**：`Independent` / `Exclusive_Replaceable` / `Exclusive_Blocking`（独占互斥）
- **ActivationOwnedLooseTags**：激活期间给角色挂的标签（如滑铲挂 `State.Sliding`）
- **ActivationRequiredTags / BlockedTags**：激活准入
- **CostEffect / CooldownEffect**：消耗与冷却（CooldownEffect 的 GrantedTags 当冷却标签）
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

### 7.2 授予 / 激活 / 取消

```csharp
var h = asc.GiveAbility(slideTemplate, level: 1);
asc.TryActivateAbility(h);
asc.TryActivateAbilitiesByTag(GameplayTag.RequestTag("Ability.Slide"));
asc.ClearAbility(h); // 移除
```

### 7.3 批量授予 AbilityLoadout

`AbilityLoadout` 资产（*Create → Likeon → GAS → Ability Loadout*）打包"技能 + 常驻效果 + 属性集类型"，一次授予：

```csharp
var handles = asc.GrantLoadout(defaultLoadout); // 返回 GrantedAbilityHandles，可整批回收
```

### 7.4 激活组互斥

- `Independent`：不互斥
- `Exclusive_Replaceable`：可被其它独占技能打断替换
- `Exclusive_Blocking`：阻止其它独占技能激活

新独占技能激活时会自动打断 Replaceable 组。

### 7.5 状态感知的能力关系

`AbilityTagRelationshipMapping`（*Create → Likeon → GAS → Ability Tag Relationship Mapping*）把"技能之间 block/cancel/激活准入"数据化，其中 **Layered** 带 `GameplayTagQuery`，按角色当前状态动态生效。挂到 ASC：

```csharp
asc.SetTagRelationshipMapping(myMapping);
```

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
asc.OnAttributeChanged += (attr, oldV, newV) => { };
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

用 Unity Input System 时，挂 `UnityInputBinder`（把 `InputActionReference` 绑到 InputTag，自动调 ReceiveInput）。

### 9.2 控制集与多态

`InputControlSetup`（*Create → Likeon → GAS → Input Control Setup*）持有：
- **检查器 InputChecker**（门控，全部通过才放行；`InputChecker_TagRelationship` 按状态放行/拦截）
- **处理器 InputProcessor**（多态分发落点）
- **ExecutionType**：`FirstOnly`（只执行第一个通过的——多态关键）/ `MatchAll`

**配"X 键 = 冲刺时滑铲 / 否则下蹲"**：在一个 InputControlSetup 里，`ExecutionType = FirstOnly`，加两个 `InputProcessor_ActivateAbilityByTag`，监听同一个 InputTag：

| 顺序 | 处理器 | StateQuery | AbilityTag |
|---|---|---|---|
| [0] 滑铲（排前） | InputProcessor_ActivateAbilityByTag | `MatchAllTags(Movement.State.Sprint)` | `Ability.Slide` |
| [1] 下蹲（排后） | InputProcessor_ActivateAbilityByTag | （空） | `Ability.Crouch` |

冲刺中按键 → [0] 状态条件通过 → 滑铲，`FirstOnly` 返回；非冲刺 → [0] 不通过 → [1] 下蹲。

把控制集压栈：`inputSys.PushInputSetup(setup);`（进瞄准/载具/UI 时可 push 另一套，退出 `PopInputSetup`）。

> 这套"冲刺状态"从哪来？由[移动系统](#11-移动--locomotion)把 `Movement.State.Sprint` 写到 ASC 标签上——这就是"状态总线"。

### 9.3 输入缓冲（连招预输入）

`InputConfig` 里定义缓冲窗口；攻击动画用 Animation Event 调 `inputSys.OpenInputBufferWindow(tag)` 开窗，窗口期内被拦的输入存入缓冲，`CloseInputBufferWindow` 时重放。

---

## 10. 近战战斗

### 10.1 攻击定义

`AttackDefinition`（*Create → Likeon → GAS → Attack Definition*）：命中后施加什么。
- **TargetEffect**：命中施加的主效果（伤害 GE）
- **SetByCallerMagnitudes**：传给效果的数值（如 `Data.Damage = 20`）
- **TargetEffectContainer**：额外批量效果
- **TargetGameplayCues**：命中表现
- **KnockbackDistance / HitStallingDuration**：击退 / 顿帧

### 10.2 判定组件

`MeleeAttackTrace`（挂在角色上）：用骨骼 socket（挂在骨骼上的 Transform）做球扫命中检测。配置 `Entries`（每条 = 一个 AttackDefinition + 一组判定点）。

**判定窗口由 Animation Event 驱动**——在攻击动画的命中帧区间起止，分别调用：

```csharp
// Animation Event 调用（int 参数=Entries 下标）
public void BeginAttackTrace(int index);
public void EndAttackTrace();
```

命中流程（自动）：球扫 → 阵营过滤（`CombatTeamAgent`，只打敌对）→ 一次窗口每目标只命中一次 → 施加 AttackDefinition 的效果 → 生成 `AttackResult` 登记到目标 `CombatSystemComponent` → 触发 cue + 顿帧。

### 10.3 阵营

`CombatTeamAgent`（TeamId）。相同=友方，不同=敌方，-1=中立。`CombatTeamAgent.IsHostile(source, target)` 判定。

### 10.4 伤害公式

默认走 SetByCaller 直接到 `IncomingDamage`；要带减伤，用 `DamageExecutionCalculation`（读 `AS_Combat.Damage` − 目标减伤 → IncomingDamage），放进伤害 GE 的 Executions。

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

写一个 cue 处理器：`GameplayCueNotify_Static`（*Create → Likeon → GAS → Gameplay Cue Notify (Static)*），配粒子 + 音效 + 响应的 CueTag（层级匹配）。注册：

```csharp
GameplayCueManager.Instance.RegisterCueNotify(myCueNotify);
asc.ExecuteGameplayCue(GameplayTag.RequestTag("GameplayCue.Hit"),
    GameplayCueParameters.At(hitPoint, hitNormal));
// 也可订阅 GameplayCueManager.Instance.OnGameplayCue 自己处理
```

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
- **标签注册表 `GameplayTagsSettings`** + 顶部菜单窗口 **`Likeon ▸ GAS ▸ Gameplay Tags`**：集中增删标签（下拉候选来源），窗口内也有"扫描工程补标签"按钮。插件编辑器入口统一在 **Likeon** 菜单下，不挂 Project Settings。
- **标签扫描**：菜单 `Likeon ▸ GAS ▸ Scan Project for Gameplay Tags`，扫工程里 `RequestTag("...")` 字面量一键补进注册表。
- **增强 Inspector**：GameplayEffect / GameplayAbility / AttackDefinition / AbilityLoadout 带摘要与配置校验提示。
- **SerializeReference 选择器**：`InputControlSetup` 的检查器/处理器列表按子类型下拉添加。

---

## 14. 联网

当前版本为**单机权威逻辑**。网络复制 / 预测**未实现**，规划为后续阶段。所有"会改状态"的入口集中在 `AbilitySystemComponent`，便于届时统一接 authority 判断。

> 工作量评估、拆桶与两大未决战略点（要不要做客户端预测、网络库选型 NGO 待重新评估）已存档在仓库的 `MD/design/阶段6-联网-工作量评估.md`（开发档，非发布包内）。

---

## 15. 常见问题

**Q：技能激活不了？**
检查：① AbilityTags 配了吗（按标签激活需要）；② ActivationRequiredTags/BlockedTags 是否被当前状态挡住；③ CostEffect 是否扣不起（资源不足）；④ 是否被冷却或激活组互斥挡住。

**Q：伤害没生效？**
确认伤害 GE 改的是 `AS_Health.IncomingDamage`（不是直接改 Health），目标 ASC 有 `AS_Health`，且 SetByCaller 标签和 GE 里配的一致。

**Q："冲刺时滑铲"不触发？**
确认移动系统已把 `Movement.State.Sprint` 镜像到 ASC（同物体有 ASC 时自动；否则手动 `move.SetGameplayTagsProvider(asc)`），且滑铲处理器排在下蹲前、ExecutionType=FirstOnly。

**Q：近战判定没命中？**
确认：① Animation Event 调了 `BeginAttackTrace`/`EndAttackTrace`；② 判定点 socket Transform 配在 Entries 里；③ 目标有 Collider + ASC；④ 双方 `CombatTeamAgent` 是敌对（不同 TeamId）。

**Q：标签下拉是空的？**
打开顶部菜单 *Likeon ▸ GAS ▸ Gameplay Tags* 窗口加标签，或跑一次 *Likeon ▸ GAS ▸ Scan Project for Gameplay Tags*（窗口里也有该按钮）。

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
// 阶段是 GamePhaseAbility 资产（Create → Likeon → GAS → Game Phase Ability），设 GamePhaseTag
GamePhaseSubsystem.Instance.StartPhase(gameAsc, playingPhase);    // Game.Playing
GamePhaseSubsystem.Instance.StartPhase(gameAsc, warmUpPhase);     // Game.Playing.WarmUp（与父共存）
GamePhaseSubsystem.Instance.StartPhase(gameAsc, postGamePhase);   // 结束兄弟 WarmUp，保留父 Game.Playing

GamePhaseSubsystem.Instance.WhenPhaseStartsOrIsActive(
    GameplayTag.RequestTag("Game.Playing"), EPhaseTagMatchType.PartialMatch,
    tag => { /* 进入 Playing 任意子阶段时 */ });
bool inPlaying = GamePhaseSubsystem.Instance.IsPhaseActive(GameplayTag.RequestTag("Game.Playing"));
```

### 16.4 通用碰撞检测 CollisionTrace

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
| 血条/法力/耐力条 | `asc.OnAttributeChanged(attr, old, new)` |
| 状态图标（眩晕/buff 有无） | `asc.OnTagChanged(tag, present)` |
| buff/debuff 图标条（带倒计时 + ×N 层数） | `asc.OnActiveEffectAdded/Removed/StackChanged` + 轮询 `asc.GetActiveGameplayEffects()` 读 `TimeRemaining` / `StackCount` |
| 技能冷却填充 | 轮询 `asc.GetCooldownRemainingForTags(...)` |
| 技能栏（loadout 驱动增删） | `asc.OnAbilityGiven / OnAbilityRemoved` + `asc.GetGrantedAbilities()` |
| 技能激活反馈 | `asc.OnAbilityActivated / OnAbilityEnded` |
| 伤害飘字 | `combat.OnDealtDamage / OnAttackResultReceived` |
| 削韧条 | `poise.OnPoiseBroken / OnPoiseRecovered` |
| 锁定标记 | `targeting.OnTargetLockOn / OnTargetLockOff` |
| 武器图标 | `weapon.OnEquipped / OnUnequipped / OnWeaponActiveStateChanged` |

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

*本文档随框架版本更新。问题反馈与版本管理见项目仓库。*
