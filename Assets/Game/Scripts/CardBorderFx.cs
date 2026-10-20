using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    /// <summary>卡牌边框粒子特效（2026-10-18 用户定义——武器拖入游戏区的弹药检查反馈）：
    /// 弹药满足（ammo ok）=绿色粒子持续环绕该武器卡（「弹药满足状态」，随卡牌重建/销毁而消失）；
    /// 弹药不足（not enough ammo）——打出被拒弹回时=暗红色粒子单次环绕闪烁约 1.2s 后自毁；
    /// 弹药不足但照常拖入游戏区（2026-10-18 用户定义）=暗红色粒子持续环绕该武器卡（「弹药不足状态」，随卡牌重建/销毁而消失）。
    /// ScreenSpaceOverlay 画布下以 UI 小圆点沿边框路径流动实现——无需粒子材质，UI 层级内可靠渲染。</summary>
    public class CardBorderFx : MonoBehaviour
    {
        public enum Mode { Loop, FlashOnce }

        private const int DotCount = 12;
        private const float DotSize = 7f;
        private const float RoundsPerSecond = 0.55f; // 每秒绕行圈数
        private const float FlashLifetime = 1.2f;

        private RectTransform card;
        private Color color;
        public Mode mode; // 公开供断言/外部读取当前模式
        private float age;
        private readonly Image[] dots = new Image[DotCount];

        /// <summary>在卡牌根节点上挂边框粒子：Loop=持续环绕（随卡牌销毁而消失），FlashOnce=闪烁后自毁。</summary>
        public static CardBorderFx Attach(RectTransform cardRoot, Color color, Mode mode)
        {
            if (cardRoot == null) return null;
            var fxRoot = new GameObject("CardBorderFx", typeof(RectTransform));
            var rt = (RectTransform)fxRoot.transform;
            rt.SetParent(cardRoot, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var fx = fxRoot.AddComponent<CardBorderFx>();
            fx.card = cardRoot;
            fx.color = color;
            fx.mode = mode;
            fx.CreateDots();
            return fx;
        }

        private void CreateDots()
        {
            for (int i = 0; i < DotCount; i++)
            {
                var dot = new GameObject("Dot", typeof(RectTransform), typeof(Image));
                var rt = (RectTransform)dot.transform;
                rt.SetParent(transform, false);
                rt.sizeDelta = new Vector2(DotSize, DotSize);
                var img = dot.GetComponent<Image>();
                img.color = color;
                img.raycastTarget = false;
                dots[i] = img;
            }
        }

        private void Update()
        {
            if (card == null) { Destroy(gameObject); return; }
            age += Time.unscaledDeltaTime;

            var rect = card.rect;
            float baseAlpha = mode == Mode.FlashOnce
                ? Mathf.Clamp01(1f - age / FlashLifetime)
                : 0.45f + 0.55f * Mathf.PingPong(age * 1.6f, 1f);
            float t0 = age * RoundsPerSecond;

            for (int i = 0; i < DotCount; i++)
            {
                float t = Mathf.Repeat(t0 + (float)i / DotCount, 1f);
                dots[i].rectTransform.anchoredPosition = PerimeterPoint(rect, t);
                float twinkle = 0.55f + 0.45f * Mathf.Sin(t0 * 12.566f + i * 2.4f);
                dots[i].color = new Color(color.r, color.g, color.b, baseAlpha * twinkle);
            }

            if (mode == Mode.FlashOnce && age >= FlashLifetime)
                Destroy(gameObject);
        }

        /// <summary>矩形边框路径上参数 t∈[0,1) 的点（从左上角起顺时针）。</summary>
        private static Vector2 PerimeterPoint(Rect rect, float t)
        {
            float halfW = rect.width / 2f, halfH = rect.height / 2f;
            float d = Mathf.Repeat(t, 1f) * 2f * (rect.width + rect.height);
            if (d < rect.width) return new Vector2(-halfW + d, halfH);
            d -= rect.width;
            if (d < rect.height) return new Vector2(halfW, halfH - d);
            d -= rect.height;
            if (d < rect.width) return new Vector2(halfW - d, -halfH);
            d -= rect.width;
            return new Vector2(-halfW, -halfH + d);
        }
    }
}
