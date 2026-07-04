// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 游戏阶段技能：带一个层级 GamePhaseTag，激活=进入该阶段、结束=离开。激活/结束自动 hook GamePhaseSubsystem。
// 阶段持续激活，直到被兄弟阶段取代或被手动 EndAbility。
// 买家可子类化加阶段专属逻辑（如进入战斗阶段时全局施加效果——配合 GlobalAbilitySystem）。

using UnityEngine;

namespace Likeon.GAS
{
    [CreateAssetMenu(menuName = "Sigil/GAS/Game Phase Ability")]
    public class GamePhaseAbility : GameplayAbility
    {
        [Tooltip("本阶段的层级标签，如 Game.Playing / Game.Playing.WarmUp（父子可共存、兄弟互斥）")]
        public GameplayTag GamePhaseTag;

        protected override void OnActivateAbility(GameplayEventData triggerData)
        {
            GamePhaseSubsystem.Instance.OnBeginPhase(this);
            // 不调 EndAbility：阶段保持激活，直到被新阶段取代或手动结束
        }

        protected override void OnEndAbility(bool wasCancelled)
        {
            GamePhaseSubsystem.Instance.OnEndPhase(this);
        }
    }
}
