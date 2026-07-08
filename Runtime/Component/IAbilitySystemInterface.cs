// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 让一个对象声明"我的 ASC 是哪个"。

namespace Likeon.GAS
{
    /// <summary>
    /// 对齐 UE IAbilitySystemInterface：当 <see cref="AbilitySystemComponent"/> 不在
    /// 查询对象所在的 GameObject 上（如挂在子物体、坐骑/伙伴对象、或一个数据壳上）时，
    /// 由该对象实现本接口指出真正的 ASC。<see cref="AbilitySystemComponent.GetAbilitySystem"/>
    /// 会优先走这个接口，再退回 GetComponent。
    /// </summary>
    public interface IAbilitySystemInterface
    {
        /// <summary>返回本对象对应的 ASC（可能为 null）。</summary>
        AbilitySystemComponent GetAbilitySystemComponent();
    }
}
