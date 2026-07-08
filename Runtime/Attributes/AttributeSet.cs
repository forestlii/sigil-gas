// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 属性集合基类，提供 GameplayEffect 结算前后的钩子。

using System.Collections.Generic;

namespace Likeon.GAS
{
    /// <summary>
    /// 属性变更通知载荷（对齐 UE 属性变更事件：带来源信息）。
    /// <see cref="Source"/> 携带"谁打的/哪个效果"（Instigator/EffectCauser/SourceASC/触发技能），
    /// 由聚合重算的来源效果填充；无单一来源的变更（移除/抑制翻转）时为 null。
    /// </summary>
    public readonly struct AttributeChangeData
    {
        public readonly GameplayAttribute Attribute;
        public readonly float OldValue;
        public readonly float NewValue;
        /// <summary>触发本次变更的效果上下文（可空——来源未知时为 null，对齐 UE"部分参数可能为 null"）。</summary>
        public readonly GameplayEffectContext Source;

        public AttributeChangeData(GameplayAttribute attribute, float oldValue, float newValue, GameplayEffectContext source)
        {
            Attribute = attribute;
            OldValue = oldValue;
            NewValue = newValue;
            Source = source;
        }
    }

    /// <summary>
    /// 属性集基类。子类（如 AS_Health）声明若干 <see cref="GameplayAttributeData"/> 字段，
    /// 在 <see cref="RegisterAttributes"/> 里登记名字，并可重写 GameplayEffect 结算钩子。
    /// 由 <see cref="AbilitySystemComponent"/> 持有（一个 ASC 可有多个集）。
    /// </summary>
    [System.Serializable] // SerializeReference 落盘需要：AbilityLoadout.GrantedAttributeSets 才能把所选属性集类型持久化进资产（否则保存丢失）
    public abstract class AttributeSet
    {
        /// <summary>拥有此属性集的 ASC。由 ASC 在添加时回填。</summary>
        public AbilitySystemComponent Owner { get; internal set; }

        private readonly Dictionary<string, GameplayAttributeData> _attributes = new Dictionary<string, GameplayAttributeData>();
        private bool _registered;

        /// <summary>属性名 -> 数据。</summary>
        public IReadOnlyDictionary<string, GameplayAttributeData> Attributes
        {
            get { EnsureRegistered(); return _attributes; }
        }

        internal void EnsureRegistered()
        {
            if (_registered) return;
            _registered = true;
            RegisterAttributes();
        }

        /// <summary>子类在此调用 <see cref="Register"/> 登记每个属性。</summary>
        protected abstract void RegisterAttributes();

        /// <summary>登记一个属性：名字 -> 数据字段。</summary>
        protected void Register(string name, GameplayAttributeData data)
        {
            _attributes[name] = data;
        }

        private HashSet<string> _metaAttributes;

        /// <summary>
        /// 标记一个属性为 meta（瞬时/中转属性，如 IncomingDamage）。子类/codegen 在
        /// <see cref="RegisterAttributes"/> 里调。meta 属性只该被 Instant/Execution 写并清零，
        /// 不该被 Duration/Infinite 效果的 modifier 直接挂——对齐 UE HideFromModifiers 的防误用意图。
        /// </summary>
        protected void MarkMeta(string name)
        {
            (_metaAttributes ??= new HashSet<string>()).Add(name);
        }

        /// <summary>该属性是否被标记为 meta。</summary>
        public bool IsMeta(string name)
        {
            EnsureRegistered();
            return _metaAttributes != null && _metaAttributes.Contains(name);
        }

        /// <summary>按名取属性数据。</summary>
        public GameplayAttributeData GetAttributeData(string name)
        {
            EnsureRegistered();
            return _attributes.TryGetValue(name, out var d) ? d : null;
        }

        /// <summary>本集对应的 GameplayAttribute 句柄。</summary>
        public GameplayAttribute GetAttribute(string name) => new GameplayAttribute(GetType(), name);

        // ---- 结算钩子----

        /// <summary>
        /// CurrentValue 即将改变前调用，可在此 clamp（如把 Health 夹到 [0, MaxHealth]）。
        /// </summary>
        public virtual void PreAttributeChange(GameplayAttribute attribute, ref float newValue) { }

        /// <summary>CurrentValue 改变后调用。</summary>
        public virtual void PostAttributeChange(GameplayAttribute attribute, float oldValue, float newValue) { }

        /// <summary>BaseValue 即将被 Instant/Periodic 效果改变前调用。</summary>
        public virtual void PreAttributeBaseChange(GameplayAttribute attribute, ref float newValue) { }

        /// <summary>
        /// BaseValue 被 Instant/Periodic 效果改变后调用（对齐 UE PostAttributeBaseChange）。
        /// 用于响应"永久值"变化（如上限提升后按比例补当前值、记账、触发成就）。
        /// </summary>
        public virtual void PostAttributeBaseChange(GameplayAttribute attribute, float oldValue, float newValue) { }

        /// <summary>
        /// GameplayEffect 结算前调用，返回 false 可阻止本次结算。
        /// </summary>
        public virtual bool PreGameplayEffectExecute(GameplayEffectModCallbackData data) => true;

        /// <summary>
        /// GameplayEffect 结算后调用——这是 Meta Attribute 伤害管线的落点：
        /// 在此把 IncomingDamage 清零并映射成 -Health。
        /// </summary>
        public virtual void PostGameplayEffectExecute(GameplayEffectModCallbackData data) { }
    }
}
