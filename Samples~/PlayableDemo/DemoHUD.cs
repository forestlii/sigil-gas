// Demo 屏幕 HUD（OnGUI 即时模式，免 UGUI 样板）：让 demo 自解释。
// 左上：操作键说明 + 玩家 HP/体力 + 激活 buff(名×层数 + 剩余秒)。
// 中下：当前锁定目标名 + 其削韧条 / 破防提示。
// 注：HUD 全靠订阅/读取框架对外暴露的可观测性 API（GetActiveGameplayEffects / 属性 / 标签 / 锁定状态）——
//     这正是"GAS 只广播数据、UI 自行渲染"责任边界的活演示。
using System.Text;
using Likeon.GAS;
using UnityEngine;

namespace Likeon.GAS.Sample.PlayableDemo
{
    public class DemoHUD : MonoBehaviour
    {
        [HideInInspector] public AbilitySystemComponent PlayerASC;
        [HideInInspector] public TargetingSystemComponent Targeting;
        [HideInInspector] public DemoPlayerController Controller;

        private static readonly GameplayTag StaggeredTag = GameplayTag.RequestTag("State.Staggered");
        private static readonly GameplayTag FocusingTag = GameplayTag.RequestTag("State.Focusing");
        private static readonly GameplayTag HonkTag = GameplayTag.RequestTag("Event.Honk");

        private AS_Health _hp;
        private AS_Stamina _stamina;
        private GUIStyle _box, _label, _small, _center, _banner;
        private readonly StringBuilder _sb = new StringBuilder();
        private float _honkUntil;

        private void Start()
        {
            if (PlayerASC != null)
            {
                _hp = PlayerASC.GetAttributeSet<AS_Health>();
                _stamina = PlayerASC.GetAttributeSet<AS_Stamina>();
                PlayerASC.OnGameplayEvent += OnGameplayEvent; // 在 Start 订阅：此时 PlayerASC 已被 PlayableDemo 赋值
            }
        }

        private void OnDestroy()
        {
            if (PlayerASC != null) PlayerASC.OnGameplayEvent -= OnGameplayEvent;
        }

        // 载具模式下近战键广播 Event.Honk → 闪一下 "HONK!"
        private void OnGameplayEvent(GameplayTag eventTag, GameplayEventData data)
        {
            if (eventTag.MatchesTag(HonkTag)) _honkUntil = Time.time + 0.6f;
        }

        private void EnsureStyles()
        {
            if (_box != null) return;
            _box = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, padding = new RectOffset(10, 10, 8, 8) };
            _label = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 12, richText = true, normal = { textColor = new Color(0.8f, 0.8f, 0.8f) } };
            _center = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true, alignment = TextAnchor.MiddleCenter };
            _banner = new GUIStyle(GUI.skin.label) { fontSize = 18, richText = true, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        }

        private void OnGUI()
        {
            EnsureStyles();
            DrawControls();
            DrawPlayerStatus();
            DrawLockTarget();
            DrawModeBanners();
        }

        // ---------- 操作说明 ----------
        private void DrawControls()
        {
            const float w = 250f;
            GUILayout.BeginArea(new Rect(10, 10, w, 230), GUIContent.none);
            GUILayout.BeginVertical(_box);
            GUILayout.Label("<b>Sigil · 功能展示 Demo</b>", _label);
            GUILayout.Label("WASD 移动 / Shift 冲刺 / 鼠标看", _small);
            GUILayout.Label("空格·左键：近战    右键·F：远程", _small);
            GUILayout.Label("Tab：锁定/解锁    Q/E：切换目标", _small);
            GUILayout.Label("R：叠加 Power buff（演示 stacking）", _small);
            GUILayout.Label("<b>1/2</b>：切换 剑/斧（近战键多态）", _small);
            GUILayout.Label("<b>G</b>：专注（近战被挡，远程取消）", _small);
            GUILayout.Label("<b>V</b>：载具模式（近战键改鸣笛）", _small);
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        // ---------- 玩家状态 + buff ----------
        private void DrawPlayerStatus()
        {
            if (PlayerASC == null) return;
            const float w = 250f;
            GUILayout.BeginArea(new Rect(10, 250, w, 280), GUIContent.none);
            GUILayout.BeginVertical(_box);

            if (_hp != null)
                GUILayout.Label($"HP  <b>{_hp.Health.CurrentValue:0}</b> / {_hp.MaxHealth.CurrentValue:0}", _label);
            if (_stamina != null)
                GUILayout.Label($"体力  <b>{_stamina.Stamina.CurrentValue:0}</b> / {_stamina.MaxStamina.CurrentValue:0}", _label);

            if (Controller != null)
                GUILayout.Label($"武器 Weapon：<b>{Controller.EquippedWeaponLabel}</b>", _small);
            if (PlayerASC.HasMatchingGameplayTag(FocusingTag))
                GUILayout.Label("<color=#fd6>● 专注中 FOCUSING — 近战被挡</color>", _small);

            GUILayout.Space(4);
            GUILayout.Label("<b>激活效果 Active Effects</b>", _label);
            var effects = PlayerASC.GetActiveGameplayEffects();
            if (effects.Count == 0)
            {
                GUILayout.Label("（无 / none）", _small);
            }
            else
            {
                foreach (var e in effects)
                {
                    string name = e.Def != null ? e.Def.name : "Effect";
                    string stack = e.StackCount > 1 ? $" ×{e.StackCount}" : "";
                    string time = float.IsPositiveInfinity(e.TimeRemaining) ? "∞" : $"{e.TimeRemaining:0.0}s";
                    GUILayout.Label($"• {name}<b>{stack}</b>  <color=#9fd>{time}</color>", _small);
                }
            }

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        // ---------- 锁定目标 ----------
        private void DrawLockTarget()
        {
            if (Targeting == null || !Targeting.IsLockedOn) return;
            var target = Targeting.TargetedActor;
            if (target == null) return;

            var asc = target.GetComponent<AbilitySystemComponent>();
            float cx = Screen.width * 0.5f;
            float y = Screen.height - 90f;

            _sb.Clear();
            _sb.Append("<b>锁定 ").Append(target.name).Append("</b>");
            bool staggered = asc != null && asc.HasMatchingGameplayTag(StaggeredTag);
            if (staggered) _sb.Append("   <color=#fd6>★ 破防 STAGGERED</color>");
            GUI.Label(new Rect(cx - 150, y, 300, 22), _sb.ToString(), _center);

            // 削韧条
            var poise = asc != null ? asc.GetAttributeSet<AS_Poise>() : null;
            if (poise != null)
            {
                float pct = poise.MaxPoise.CurrentValue > 0
                    ? Mathf.Clamp01(poise.Poise.CurrentValue / poise.MaxPoise.CurrentValue) : 0f;
                var barRect = new Rect(cx - 100, y + 24, 200, 14);
                GUI.color = new Color(0, 0, 0, 0.5f);
                GUI.DrawTexture(barRect, Texture2D.whiteTexture);
                GUI.color = staggered ? new Color(1f, 0.8f, 0.3f) : new Color(0.5f, 0.7f, 1f);
                GUI.DrawTexture(new Rect(barRect.x, barRect.y, barRect.width * pct, barRect.height), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(cx - 100, y + 38, 200, 18), $"削韧 Poise {poise.Poise.CurrentValue:0.0}/{poise.MaxPoise.CurrentValue:0}", _center);
            }
        }

        // ---------- 载具模式横幅 + 鸣笛闪现 ----------
        private void DrawModeBanners()
        {
            float cx = Screen.width * 0.5f;
            if (Controller != null && Controller.InVehicle)
            {
                GUI.color = new Color(1f, 0.6f, 0.2f);
                GUI.Label(new Rect(cx - 200, 12, 400, 26), "🚗 载具模式 VEHICLE — 近战键=鸣笛 HORN", _banner);
                GUI.color = Color.white;
            }
            if (Time.time < _honkUntil)
            {
                GUI.color = new Color(1f, 0.9f, 0.3f);
                GUI.Label(new Rect(cx - 100, 42, 200, 26), "📣 HONK!", _banner);
                GUI.color = Color.white;
            }
        }
    }
}
