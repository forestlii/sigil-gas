// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 阵营/敌我判定。

using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>敌我关系。（Friendly/Hostile/Neutral）。</summary>
    public enum ETeamAttitude
    {
        Friendly,
        Hostile,
        Neutral
    }

    /// <summary>提供阵营信息的接口。</summary>
    public interface ITeamAgent
    {
        int TeamId { get; }
    }

    /// <summary>
    /// 阵营组件。挂在角色上标明队伍，用于命中过滤（只打敌人）。
    /// </summary>
    [AddComponentMenu("Sigil/GAS/Combat Team Agent")]
    public class CombatTeamAgent : MonoBehaviour, ITeamAgent
    {
        [Tooltip("队伍编号。相同=友方，不同=敌方，-1=中立")]
        [SerializeField] private int teamId = 0;

        public int TeamId => teamId;
        public void SetTeamId(int id) => teamId = id;

        /// <summary>计算与另一个对象的敌我关系。</summary>
        public ETeamAttitude GetAttitudeTowards(GameObject other)
        {
            if (other == null) return ETeamAttitude.Neutral;
            var otherAgent = other.GetComponentInParent<ITeamAgent>();
            if (otherAgent == null) return ETeamAttitude.Neutral;
            if (teamId < 0 || otherAgent.TeamId < 0) return ETeamAttitude.Neutral;
            return otherAgent.TeamId == teamId ? ETeamAttitude.Friendly : ETeamAttitude.Hostile;
        }

        /// <summary>静态便捷：source 是否应攻击 target（敌对）。</summary>
        public static bool IsHostile(GameObject source, GameObject target)
        {
            if (source == null || target == null) return false;
            // 调试开关：禁用归属检查 → 跨队伍均可命中（对齐 UE 跨队伍伤害调试开关）
            if (CombatSettings.Active.DisableAffiliationCheck) return true;
            var agent = source.GetComponentInParent<CombatTeamAgent>();
            if (agent == null) return true; // 无阵营组件默认都可命中
            return agent.GetAttitudeTowards(target) == ETeamAttitude.Hostile;
        }
    }
}
