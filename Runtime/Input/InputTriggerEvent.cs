// Copyright 2026 Likeon All Rights Reserved.

namespace Likeon.GAS
{
    /// <summary>
    /// 输入触发事件类型。
    /// Unity Input System 的回调映射：started→Started, performed→Triggered, canceled→Canceled，
    /// 长按/释放等由适配器派生 Ongoing/Completed。
    /// </summary>
    public enum InputTriggerEvent
    {
        None,
        /// <summary>触发器已触发（持续满足），对应 Unity performed。</summary>
        Triggered,
        /// <summary>本帧刚开始按下，对应 Unity started。</summary>
        Started,
        /// <summary>正在进行中（尚未触发，如蓄力中）。</summary>
        Ongoing,
        /// <summary>被取消（中途松开）。对应 Unity canceled。</summary>
        Canceled,
        /// <summary>完成（从触发态结束）。</summary>
        Completed
    }
}
