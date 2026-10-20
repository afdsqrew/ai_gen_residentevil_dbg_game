using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    /// <summary>轻量浮层文字提示（Toast，2026-10-18 用户要求——收回被拒警告与「轮到x的回合」轮换提示共用）：
    /// 在面板内居中偏下（(0,-190)）浮现一条带底色的 TMP 文字，停留约 1.2 秒后 0.8 秒渐隐自毁；
    /// 全部图形 raycastTarget=false 且 CanvasGroup 关闭射线——不阻挡任何点击，纯视觉提示。</summary>
    public class ToastTip : MonoBehaviour
    {
        private const float HoldSeconds = 1.2f;
        private const float FadeSeconds = 0.8f;
        private const float BottomOffset = -190f;
        private static readonly Vector2 Size = new Vector2(760f, 56f);

        private CanvasGroup group;
        private float age;

        /// <summary>在 context 的面板下显示一条提示（幂等：旧 Toast 先移除再显示新的；textColor 缺省为警告橙红）。</summary>
        public static ToastTip Show(Component context, string message, Color? textColor = null)
        {
            var parent = context != null ? context.transform : null;
            if (parent == null) return null;
            var old = parent.Find("ToastTip");
            if (old != null) Destroy(old.gameObject);

            var root = new GameObject("ToastTip", typeof(RectTransform), typeof(CanvasGroup));
            var rt = (RectTransform)root.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, BottomOffset);
            rt.sizeDelta = Size;

            var bgGo = new GameObject("Bg", typeof(RectTransform), typeof(Image));
            var bgRt = (RectTransform)bgGo.transform;
            bgRt.SetParent(root.transform, false);
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;
            var bg = bgGo.GetComponent<Image>();
            bg.color = new Color(0.09f, 0.10f, 0.13f, 0.94f);
            bg.raycastTarget = false;

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
            var textRt = (RectTransform)textGo.transform;
            textRt.SetParent(root.transform, false);
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = textRt.offsetMax = Vector2.zero;
            var label = textGo.GetComponent<TMPro.TextMeshProUGUI>();
            label.text = message;
            label.fontSize = 22f;
            label.color = textColor ?? new Color(1f, 0.62f, 0.5f);
            label.alignment = TMPro.TextAlignmentOptions.Center;
            label.fontStyle = TMPro.FontStyles.Bold;
            label.enableWordWrapping = true;
            label.raycastTarget = false;

            var tip = root.AddComponent<ToastTip>();
            tip.group = root.GetComponent<CanvasGroup>();
            tip.group.blocksRaycasts = false; // 纯视觉提示——不拦截任何点击
            tip.group.interactable = false;
            return tip;
        }

        private void Update()
        {
            age += Mathf.Min(Time.unscaledDeltaTime, 0.25f); // 单帧长停顿只计 0.25s——编辑器工具帧停顿/WebGL 加载卡顿不会瞬间烧掉提示寿命
            if (group != null)
                group.alpha = age <= HoldSeconds ? 1f : Mathf.Clamp01(1f - (age - HoldSeconds) / FadeSeconds);
            if (age >= HoldSeconds + FadeSeconds) Destroy(gameObject);
        }
    }
}
