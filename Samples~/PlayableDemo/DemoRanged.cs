// Demo 远程射击组件：从枪口用 BulletLauncher 发射投射物。
// 演示框架已实现的 Bullet 系统（BulletDefinition + BulletLauncher + BulletInstance）；
// 锁定时朝当前锁定目标射击，否则沿枪口前方。给每发子弹挂一个可视小球（框架的 BulletInstance 本身只有逻辑、无网格）。
using Likeon.GAS;
using UnityEngine;

namespace Likeon.GAS.Sample.PlayableDemo
{
    public class DemoRanged : MonoBehaviour
    {
        // prefab 内部引用（ASC/Muzzle/Targeting 同 prefab）/ 资产引用（Bullet）—— 可见，prefab 上看得到接线
        public AbilitySystemComponent ASC;
        public Transform Muzzle;
        public BulletDefinition Bullet;
        public TargetingSystemComponent Targeting;

        public Color BulletColor = new Color(1f, 0.85f, 0.2f);
        public float BulletVisualScale = 0.25f;

        /// <summary>发射一发：方向=锁定目标(若有)否则枪口前方。供技能/控制器调用。</summary>
        public void Fire()
        {
            if (Bullet == null || ASC == null) return;

            Transform origin = Muzzle != null ? Muzzle : transform;
            Vector3 dir = origin.forward;

            // 锁定时朝目标（瞄准目标胸口高度）
            if (Targeting != null && Targeting.IsLockedOn)
            {
                Vector3 aim = Targeting.TargetedActor.transform.position + Vector3.up * 1f;
                dir = (aim - origin.position).normalized;
            }

            var bullets = BulletLauncher.Fire(Bullet, gameObject, ASC, origin.position, dir);
            foreach (var b in bullets) AttachVisual(b);
        }

        // 给逻辑子弹挂一个可视小球（随子弹一起销毁）
        private void AttachVisual(BulletInstance bullet)
        {
            if (bullet == null) return;
            var vis = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(vis.GetComponent<Collider>()); // 可视用，不参与命中
            vis.transform.SetParent(bullet.transform, false);
            vis.transform.localScale = Vector3.one * BulletVisualScale;
            var r = vis.GetComponent<Renderer>();
            if (r != null)
            {
                var m = r.material;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", BulletColor);
                if (m.HasProperty("_Color")) m.color = BulletColor;
            }
        }
    }
}
