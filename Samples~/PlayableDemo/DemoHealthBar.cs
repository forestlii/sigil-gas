// Demo 血条：角色头顶的世界空间血条（用两个缩放方块实现，免 UGUI 样板），始终朝向相机。
using Likeon.GAS;
using UnityEngine;

namespace Likeon.GAS.Sample.PlayableDemo
{
    public class DemoHealthBar : MonoBehaviour
    {
        private AS_Health _health;
        private Transform _fill;
        private float _maxHealth = 100f;

        public void Init(AbilitySystemComponent asc, float yOffset = 2.2f)
        {
            _health = asc.GetAttributeSet<AS_Health>();
            if (_health != null) _maxHealth = Mathf.Max(1f, _health.MaxHealth.CurrentValue);

            transform.SetParent(asc.transform, false);
            transform.localPosition = new Vector3(0, yOffset, 0);

            // 背景
            var bg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(bg.GetComponent<Collider>());
            bg.transform.SetParent(transform, false);
            bg.transform.localScale = new Vector3(1.2f, 0.18f, 0.05f);
            SetColor(bg, new Color(0.1f, 0.1f, 0.1f));

            // 填充
            var fill = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(fill.GetComponent<Collider>());
            fill.transform.SetParent(transform, false);
            fill.transform.localScale = new Vector3(1.1f, 0.12f, 0.08f);
            SetColor(fill, new Color(0.2f, 0.85f, 0.25f));
            _fill = fill.transform;
        }

        private static void SetColor(GameObject go, Color c)
        {
            var r = go.GetComponent<Renderer>();
            r.material.color = c;
        }

        private void LateUpdate()
        {
            if (_health == null || _fill == null) return;

            float pct = Mathf.Clamp01(_health.Health.CurrentValue / _maxHealth);
            // 从左侧收缩：缩 x 并左移
            var s = _fill.localScale; s.x = 1.1f * pct; _fill.localScale = s;
            _fill.localPosition = new Vector3(-(1.1f * (1f - pct)) * 0.5f, 0, 0);
            _fill.GetComponent<Renderer>().material.color = Color.Lerp(new Color(0.85f, 0.2f, 0.2f), new Color(0.2f, 0.85f, 0.25f), pct);

            // 朝向相机
            if (Camera.main != null)
                transform.rotation = Quaternion.LookRotation(transform.position - Camera.main.transform.position);
        }
    }
}
