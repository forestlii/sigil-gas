// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 子弹/投射物的数据定义。Unity 用 ScriptableObject。
// 单机取舍：保留 速度/重力/命中半径/数量/散射/穿透/生命/攻击定义/子弹链；

using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>子弹定义资产。</summary>
    [CreateAssetMenu(fileName = "Bullet_New", menuName = "Sigil/Combat/Bullet Definition")]
    public class BulletDefinition : ScriptableObject
    {
        [Header("生命")]
        [Tooltip("子弹存在秒数（<=0 表示无限，直到命中）")]
        public float Duration = 3f;

        [Header("发射配置（散射）")]
        [Min(1)] public int BulletCount = 1;
        [Tooltip("基础水平偏角（度）")]
        public float LaunchAngle = 0f;
        [Tooltip("相邻子弹的水平角间隔（度）")]
        public float LaunchAngleInterval = 10f;
        [Tooltip("发射仰角（度，正=朝上）")]
        public float LaunchElevationAngle = 0f;

        [Header("运动")]
        [Tooltip("初速（米/秒）")]
        public float InitialSpeed = 15f;
        [Tooltip("重力系数（0=无重力直线，1=正常重力抛物线）")]
        public float GravityScale = 0f;
        [Tooltip("命中检测球半径（米）")]
        public float HitRadius = 0.2f;

        [Header("命中")]
        [Tooltip("可命中层")]
        public LayerMask HitLayers = ~0;
        [Tooltip("命中角色后是否穿透继续飞")]
        public bool PenetrateCharacter = false;
        [Tooltip("命中地图几何后是否穿透继续飞")]
        public bool PenetrateMap = false;
        [Tooltip("命中目标时施加的攻击定义（伤害）")]
        public AttackDefinition Attack;

        [Header("子弹链")]
        [Tooltip("命中/失效时生成的后续子弹（如爆裂/分裂），不可与自身相同")]
        public BulletDefinition HitBulletDefinition;
    }
}
