// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 按定义批量生成子弹（含散射）。
// 单机取舍：这里用静态工具直接生成 GameObject+BulletInstance；
// 子弹失效时自销毁（对象池可作为后续优化，不影响 API）。

using System.Collections.Generic;
using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>子弹发射器：按 <see cref="BulletDefinition"/> 生成 N 发带散射的子弹。</summary>
    public static class BulletLauncher
    {
        /// <summary>
        /// 以 origin 为起点、baseRotation 为基准朝向，按定义的数量/散射角发射子弹。返回生成的实例。
        ///（散射：每发在基准朝向上叠加 yaw 偏角 + 统一仰角）。
        /// </summary>
        public static List<BulletInstance> Fire(BulletDefinition def, GameObject owner,
            AbilitySystemComponent sourceASC, Vector3 origin, Quaternion baseRotation)
        {
            var result = new List<BulletInstance>();
            if (def == null) return result;

            int count = Mathf.Max(1, def.BulletCount);
            for (int i = 0; i < count; i++)
            {
                // 以中心对称分布：i 相对中心的偏移 * 间隔 + 基础偏角
                float yaw = def.LaunchAngle + (i - (count - 1) * 0.5f) * def.LaunchAngleInterval;
                Quaternion rot = baseRotation * Quaternion.Euler(-def.LaunchElevationAngle, yaw, 0f);
                Vector3 dir = rot * Vector3.forward;

                var go = new GameObject($"Bullet_{def.name}_{i}");
                go.transform.position = origin;
                var bullet = go.AddComponent<BulletInstance>();
                bullet.Launch(def, owner, sourceASC, origin, dir);
                result.Add(bullet);
            }
            return result;
        }

        /// <summary>便捷重载：用一个方向向量作为基准朝向。</summary>
        public static List<BulletInstance> Fire(BulletDefinition def, GameObject owner,
            AbilitySystemComponent sourceASC, Vector3 origin, Vector3 direction)
            => Fire(def, owner, sourceASC, origin,
                direction.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(direction.normalized) : Quaternion.identity);
    }
}
