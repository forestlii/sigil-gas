// Copyright 2026 Likeon All Rights Reserved.
// 全局技能/效果系统：把一个技能或效果一次性施加到所有已注册的 ASC（全场 buff/debuff、环境效果、阶段技能）。
// 用纯 C# 单例（同 GameplayCueManager 风格）。
// 注册：调 RegisterASC/UnregisterASC，或给角色挂可选的 GlobalAbilitySystemRegistrant 组件自动注册。
//
// 语义：同一技能/效果只全局应用一次（重复 ApplyXToAll 幂等）；新 ASC 注册时自动补上所有已全局应用的项。

using System.Collections.Generic;

namespace Likeon.GAS
{
    /// <summary>
    /// 全局技能/效果单例。<see cref="ApplyAbilityToAll"/> / <see cref="ApplyEffectToAll"/> 施加到所有已注册 ASC，
    /// <see cref="RemoveAbilityFromAll"/> / <see cref="RemoveEffectFromAll"/> 撤销；ASC 经 <see cref="RegisterASC"/> 加入。
    /// </summary>
    public sealed class GlobalAbilitySystem
    {
        private static GlobalAbilitySystem _instance;
        public static GlobalAbilitySystem Instance => _instance ??= new GlobalAbilitySystem();

        private readonly List<AbilitySystemComponent> _registered = new List<AbilitySystemComponent>();
        // 技能/效果模板 → (ASC → 句柄)，撤销时按 ASC 找回各自句柄。
        private readonly Dictionary<GameplayAbility, Dictionary<AbilitySystemComponent, GameplayAbilitySpecHandle>> _abilities
            = new Dictionary<GameplayAbility, Dictionary<AbilitySystemComponent, GameplayAbilitySpecHandle>>();
        private readonly Dictionary<GameplayEffect, Dictionary<AbilitySystemComponent, ActiveGameplayEffectHandle>> _effects
            = new Dictionary<GameplayEffect, Dictionary<AbilitySystemComponent, ActiveGameplayEffectHandle>>();

        /// <summary>当前已注册的 ASC 数量。</summary>
        public int RegisteredCount => _registered.Count;

        /// <summary>注册一个 ASC，并把所有已全局应用的技能/效果补到它身上。</summary>
        public void RegisterASC(AbilitySystemComponent asc)
        {
            if (asc == null || _registered.Contains(asc)) return;

            foreach (var kv in _abilities)
                kv.Value[asc] = asc.GiveAbility(kv.Key);
            foreach (var kv in _effects)
                kv.Value[asc] = asc.ApplyGameplayEffectToSelf(kv.Key);

            _registered.Add(asc);
        }

        /// <summary>注销一个 ASC，并移除它身上所有全局施加的技能/效果。</summary>
        public void UnregisterASC(AbilitySystemComponent asc)
        {
            if (asc == null) return;

            foreach (var kv in _abilities)
                if (kv.Value.TryGetValue(asc, out var h)) { asc.ClearAbility(h); kv.Value.Remove(asc); }
            foreach (var kv in _effects)
                if (kv.Value.TryGetValue(asc, out var h)) { asc.RemoveActiveGameplayEffect(h); kv.Value.Remove(asc); }

            _registered.Remove(asc);
        }

        /// <summary>把技能授予所有已注册 ASC（同一技能幂等，已应用则忽略）。</summary>
        public void ApplyAbilityToAll(GameplayAbility ability)
        {
            if (ability == null || _abilities.ContainsKey(ability)) return;

            var map = new Dictionary<AbilitySystemComponent, GameplayAbilitySpecHandle>();
            foreach (var asc in _registered)
                if (asc != null) map[asc] = asc.GiveAbility(ability);
            _abilities[ability] = map;
        }

        /// <summary>把效果施加到所有已注册 ASC（同一效果幂等，已应用则忽略）。</summary>
        public void ApplyEffectToAll(GameplayEffect effect, int level = 1)
        {
            if (effect == null || _effects.ContainsKey(effect)) return;

            var map = new Dictionary<AbilitySystemComponent, ActiveGameplayEffectHandle>();
            foreach (var asc in _registered)
                if (asc != null) map[asc] = asc.ApplyGameplayEffectToSelf(effect, level);
            _effects[effect] = map;
        }

        /// <summary>从所有 ASC 移除该全局技能。</summary>
        public void RemoveAbilityFromAll(GameplayAbility ability)
        {
            if (ability == null || !_abilities.TryGetValue(ability, out var map)) return;
            foreach (var kv in map)
                if (kv.Key != null) kv.Key.ClearAbility(kv.Value);
            _abilities.Remove(ability);
        }

        /// <summary>从所有 ASC 移除该全局效果。</summary>
        public void RemoveEffectFromAll(GameplayEffect effect)
        {
            if (effect == null || !_effects.TryGetValue(effect, out var map)) return;
            foreach (var kv in map)
                if (kv.Key != null) kv.Key.RemoveActiveGameplayEffect(kv.Value);
            _effects.Remove(effect);
        }

        /// <summary>清空所有注册与全局应用记录（场景卸载 / 测试用，不主动撤销已施加项）。</summary>
        public void Clear()
        {
            _abilities.Clear();
            _effects.Clear();
            _registered.Clear();
        }
    }
}
