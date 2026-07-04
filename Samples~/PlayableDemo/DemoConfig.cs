// DemoConfig：把 demo 的全部可配置数据收成一个 ScriptableObject 资产，让**策划在 Inspector 里配**——
// 这正是框架"数据驱动 / 数据分层"的意义所在：输入分发(按键→tag→技能 + FirstOnly 互斥)、技能互斥规则
// (block/cancel)、技能 / 攻击 / 子弹 / 效果，全是资产引用，改它们不用碰代码。
//
// 两条路：
//  · 真实工作流（推荐）：demo 场景里把一个 DemoConfig.asset 拖到 PlayableDemo.Config 上；策划改这些 .asset 即可。
//    用菜单 *Sigil ▸ GAS ▸ Generate Demo Config Assets* 一键烘出一整套默认资产并接进场景。
//  · 零设置回退：PlayableDemo.Config 留空时（裸 AddComponent / headless 测试），用下方 CreateDefault() 在内存里
//    建同一套默认值——保证 demo 仍能"挂上就跑"、冒烟测试仍能 headless 验证。
using System.Collections.Generic;
using Likeon.GAS;
using UnityEngine;

namespace Likeon.GAS.Sample.PlayableDemo
{
    [CreateAssetMenu(fileName = "DemoConfig", menuName = "Sigil/GAS/Demo/Demo Config")]
    public class DemoConfig : ScriptableObject
    {
        [Header("输入分发（按键→InputTag→技能；FirstOnly=输入互斥/多态）")]
        [Tooltip("战斗控制集：近战/远程/专注键的处理器，近战键按武器多态成轻击/重击")]
        public InputControlSetup CombatInput;
        [Tooltip("载具控制集：同一个近战键改广播鸣笛事件（演示整套键位切换）")]
        public InputControlSetup VehicleInput;

        [Header("技能互斥规则（block / cancel）")]
        [Tooltip("数据驱动的技能间关系：专注 block 近战、远程 cancel 专注")]
        public AbilityInteractionRules InteractionRules;

        [Header("技能")]
        public DemoMeleeAbility MeleeAbility;   // 剑·轻击（TraceEntryIndex=0）
        public DemoMeleeAbility HeavyAbility;   // 斧·重击（TraceEntryIndex=1）
        public DemoRangedAbility RangedAbility;
        public DemoFocusAbility FocusAbility;

        [Header("攻击 / 子弹 / 效果")]
        public AttackDefinition LightAttack;    // 剑（含伤害/削韧数值）
        public AttackDefinition HeavyAttack;    // 斧
        public BulletDefinition Bullet;         // 远程子弹（其 Attack 含远程伤害）
        public GameplayEffect PowerBuff;        // R 键叠层 buff

        // ===== 标签（与控制集/规则里配的一致）=====
        public static readonly GameplayTag MeleeTag = GameplayTag.RequestTag("Ability.MeleeAttack");
        public static readonly GameplayTag HeavyTag = GameplayTag.RequestTag("Ability.HeavyAttack");
        public static readonly GameplayTag RangedTag = GameplayTag.RequestTag("Ability.RangedAttack");
        public static readonly GameplayTag FocusTag = GameplayTag.RequestTag("Ability.Focus");
        public static readonly GameplayTag FocusingTag = GameplayTag.RequestTag("State.Focusing");
        public static readonly GameplayTag DataDamage = GameplayTag.RequestTag("Data.Damage");
        public static readonly GameplayTag DataPoiseDamage = GameplayTag.RequestTag("Data.PoiseDamage");
        public static readonly GameplayTag HitCue = GameplayTag.RequestTag("GameplayCue.Hit");
        public static readonly GameplayTag InputMelee = GameplayTag.RequestTag("InputTag.Melee");
        public static readonly GameplayTag InputRanged = GameplayTag.RequestTag("InputTag.Ranged");
        public static readonly GameplayTag InputFocus = GameplayTag.RequestTag("InputTag.Focus");
        public static readonly GameplayTag SwordTag = GameplayTag.RequestTag("Weapon.Sword");
        public static readonly GameplayTag AxeTag = GameplayTag.RequestTag("Weapon.Axe");
        public static readonly GameplayTag HonkTag = GameplayTag.RequestTag("Event.Honk");

        /// <summary>
        /// 在内存里建一套默认配置（含全部子资产并接好交叉引用）。
        /// 给 PlayableDemo 的"未拖资产"回退用；Editor 生成器也用它来烘 .asset（确保默认值单一来源）。
        /// 返回的 DemoConfig 与其全部子资产都是 CreateInstance 的纯实例（未落盘）。
        /// </summary>
        public static DemoConfig CreateDefault()
        {
            var cfg = CreateInstance<DemoConfig>();
            cfg.name = "DemoConfig (default)";

            // 伤害效果（近战/远程共用）：扣血 + 削韧（SetByCaller）+ 命中 cue
            var damage = MakeGE("GE_Damage", ge =>
            {
                ge.DurationType = EGameplayEffectDurationType.Instant;
                AddMod(ge, GameplayAttribute.From<AS_Health>("IncomingDamage"), EAttributeModifierOp.Add, GameplayModifierMagnitude.SetByCaller(DataDamage));
                AddMod(ge, GameplayAttribute.From<AS_Poise>("IncomingPoiseDamage"), EAttributeModifierOp.Add, GameplayModifierMagnitude.SetByCaller(DataPoiseDamage));
                ge.GameplayCues.Add(HitCue);
            });

            cfg.LightAttack = MakeAttack("Attack_Light", damage, 20f, 1.5f);
            cfg.HeavyAttack = MakeAttack("Attack_Heavy", damage, 36f, 3f);
            var rangedAttack = MakeAttack("Attack_Ranged", damage, 12f, 1f);

            cfg.MeleeAbility = MakeAbility<DemoMeleeAbility>("GA_Melee", MeleeTag, a => { a.TraceEntryIndex = 0; a.CostEffect = MakeStaminaCost("GE_Cost_Melee", -8f); });
            cfg.HeavyAbility = MakeAbility<DemoMeleeAbility>("GA_Heavy", HeavyTag, a => { a.TraceEntryIndex = 1; a.CostEffect = MakeStaminaCost("GE_Cost_Heavy", -14f); });
            cfg.RangedAbility = MakeAbility<DemoRangedAbility>("GA_Ranged", RangedTag, a => { a.CostEffect = MakeStaminaCost("GE_Cost_Ranged", -5f); });
            cfg.FocusAbility = MakeAbility<DemoFocusAbility>("GA_Focus", FocusTag, a => a.ActivationOwnedLooseTags.Add(new GameplayTagCount(FocusingTag, 1)));

            cfg.Bullet = CreateInstance<BulletDefinition>();
            cfg.Bullet.name = "Bullet";
            cfg.Bullet.InitialSpeed = 22f; cfg.Bullet.HitRadius = 0.3f; cfg.Bullet.Duration = 3f; cfg.Bullet.GravityScale = 0f;
            cfg.Bullet.Attack = rangedAttack;

            cfg.PowerBuff = MakeGE("GE_PowerBuff", ge =>
            {
                ge.DurationType = EGameplayEffectDurationType.HasDuration;
                ge.Duration = 5f;
                ge.StackingType = EGameplayEffectStackingType.AggregateByTarget;
                ge.StackLimitCount = 5;
                AddMod(ge, GameplayAttribute.From<AS_Health>("MaxHealth"), EAttributeModifierOp.Add, GameplayModifierMagnitude.ScalableFloat(10f));
            });

            // 战斗控制集（FirstOnly = 输入互斥/多态）：近战键按武器多态成轻击/重击
            cfg.CombatInput = CreateInstance<InputControlSetup>();
            cfg.CombatInput.name = "Input_Combat";
            cfg.CombatInput.ExecutionType = EInputProcessorExecutionType.FirstOnly;
            cfg.CombatInput.AddProcessor(new InputProcessor_ActivateAbilityByTag { InputTags = TagSet(InputMelee), StateQuery = GameplayTagQuery.MakeQuery_MatchAllTags(SwordTag), AbilityTag = MeleeTag });
            cfg.CombatInput.AddProcessor(new InputProcessor_ActivateAbilityByTag { InputTags = TagSet(InputMelee), StateQuery = GameplayTagQuery.MakeQuery_MatchAllTags(AxeTag), AbilityTag = HeavyTag });
            cfg.CombatInput.AddProcessor(new InputProcessor_ActivateAbilityByTag { InputTags = TagSet(InputRanged), AbilityTag = RangedTag });
            cfg.CombatInput.AddProcessor(new InputProcessor_ActivateAbilityByTag { InputTags = TagSet(InputFocus), AbilityTag = FocusTag });

            // 载具控制集：近战键改广播鸣笛
            cfg.VehicleInput = CreateInstance<InputControlSetup>();
            cfg.VehicleInput.name = "Input_Vehicle";
            cfg.VehicleInput.ExecutionType = EInputProcessorExecutionType.FirstOnly;
            cfg.VehicleInput.AddProcessor(new InputProcessor_SendGameplayEvent { InputTags = TagSet(InputMelee), EventTag = HonkTag });

            // 技能互斥规则：专注 block 近战；远程 cancel 专注
            cfg.InteractionRules = CreateInstance<AbilityInteractionRules>();
            cfg.InteractionRules.name = "AbilityRules";
            cfg.InteractionRules.AddBaseRule(new AbilityTagRule { AbilityTag = FocusTag, AbilityTagsToBlock = new List<GameplayTag> { MeleeTag, HeavyTag } });
            cfg.InteractionRules.AddBaseRule(new AbilityTagRule { AbilityTag = RangedTag, AbilityTagsToCancel = new List<GameplayTag> { FocusTag } });

            return cfg;
        }

        /// <summary>
        /// 用本配置的技能 + 属性集组一个"玩家装载"（对齐 §6.1：Config 定义、Loadout 装载引用）。
        /// 属性集 AS_Health/AS_Stamina；技能=近战/重击/远程/专注。供 ASC.initialLoadouts 在 prefab 上配，
        /// 替代代码 AddAttributeSet/GiveAbility。引用本配置已有的技能资产，不重复造。
        /// </summary>
        public AbilityLoadout BuildPlayerLoadout()
        {
            var lo = CreateInstance<AbilityLoadout>();
            lo.name = "PlayerLoadout";
            lo.GrantedAttributeSets.Add(new AS_Health());
            lo.GrantedAttributeSets.Add(new AS_Stamina());
            AddGranted(lo, MeleeAbility);
            AddGranted(lo, HeavyAbility);
            AddGranted(lo, RangedAbility);
            AddGranted(lo, FocusAbility);
            return lo;
        }

        /// <summary>敌人装载：AS_Health + AS_Poise（削韧），无技能（敌人是受击靶/锁定候选）。</summary>
        public AbilityLoadout BuildEnemyLoadout()
        {
            var lo = CreateInstance<AbilityLoadout>();
            lo.name = "EnemyLoadout";
            lo.GrantedAttributeSets.Add(new AS_Health());
            lo.GrantedAttributeSets.Add(new AS_Poise());
            return lo;
        }

        private static void AddGranted(AbilityLoadout lo, GameplayAbility ability)
        {
            if (ability != null)
                lo.GrantedAbilities.Add(new AbilityLoadout.GrantedAbility { Ability = ability, Level = 1 });
        }

        /// <summary>枚举本配置引用到的全部子 ScriptableObject（含间接的 cost/damage GE），供回退路径清理或生成器落盘。</summary>
        public IEnumerable<Object> EnumerateSubAssets()
        {
            var seen = new HashSet<Object>();
            void Y(Object o, List<Object> acc) { if (o != null && seen.Add(o)) acc.Add(o); }
            var list = new List<Object>();
            Y(CombatInput, list); Y(VehicleInput, list); Y(InteractionRules, list);
            Y(MeleeAbility, list); Y(HeavyAbility, list); Y(RangedAbility, list); Y(FocusAbility, list);
            Y(LightAttack, list); Y(HeavyAttack, list); Y(Bullet, list); Y(PowerBuff, list);
            if (MeleeAbility != null) Y(MeleeAbility.CostEffect, list);
            if (HeavyAbility != null) Y(HeavyAbility.CostEffect, list);
            if (RangedAbility != null) Y(RangedAbility.CostEffect, list);
            if (LightAttack != null) Y(LightAttack.TargetEffect, list);
            if (Bullet != null && Bullet.Attack != null) { Y(Bullet.Attack, list); Y(Bullet.Attack.TargetEffect, list); }
            return list;
        }

        // ---------- 构造小工具 ----------
        private static GameplayEffect MakeGE(string name, System.Action<GameplayEffect> cfg)
        {
            var ge = CreateInstance<GameplayEffect>(); ge.name = name; cfg(ge); return ge;
        }

        private static void AddMod(GameplayEffect ge, GameplayAttribute attr, EAttributeModifierOp op, GameplayModifierMagnitude mag)
            => ge.Modifiers.Add(new GameplayModifierInfo { Attribute = attr, Operation = op, Magnitude = mag });

        private static AttackDefinition MakeAttack(string name, GameplayEffect damage, float dmg, float poise)
        {
            var a = CreateInstance<AttackDefinition>(); a.name = name;
            a.TargetEffect = damage;
            a.SetByCallerMagnitudes.Add(new SetByCallerMagnitude { Tag = DataDamage, Value = dmg });
            a.SetByCallerMagnitudes.Add(new SetByCallerMagnitude { Tag = DataPoiseDamage, Value = poise });
            return a;
        }

        private static GameplayEffect MakeStaminaCost(string name, float amount) => MakeGE(name, ge =>
        {
            ge.DurationType = EGameplayEffectDurationType.Instant;
            AddMod(ge, GameplayAttribute.From<AS_Stamina>("Stamina"), EAttributeModifierOp.Add, GameplayModifierMagnitude.ScalableFloat(amount));
        });

        private static T MakeAbility<T>(string name, GameplayTag tag, System.Action<T> cfg) where T : GameplayAbility
        {
            var a = CreateInstance<T>(); a.name = name; a.AbilityTags.Add(tag); cfg(a); return a;
        }

        private static GameplayTagContainer TagSet(GameplayTag t) { var c = new GameplayTagContainer(); c.AddTag(t); return c; }
    }
}
