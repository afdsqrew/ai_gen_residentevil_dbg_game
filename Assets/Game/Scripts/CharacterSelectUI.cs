using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// 角色选取界面（开局流程：主界面开始游戏 → 人数选择 → 本界面 → 游戏模式选择）。
    /// 从玩家1开始轮流抽取角色牌（共 10 名，全队唯一——已确认的角色不再回到牌堆）：
    /// 「更换角色」把当前角色放回牌堆，再随机抽一张不相同的角色（弃换的角色后续仍可能被抽到）；
    /// 「确认角色」锁定该玩家的角色并轮到下一位；全部玩家确认后经 MainMenuUI 进入游戏模式选择。
    /// 挂载于 CharacterSelectPanel；面板每次显示（OnEnable）即开始一轮全新的选取，
    /// 更换/确认按钮由场景搭建脚本持久化绑定到本组件。
    /// </summary>
    public class CharacterSelectUI : MonoBehaviour
    {
        [Header("内容节点（由场景搭建脚本赋值）")]
        public RectTransform cardRoot;          // 当前抽到角色卡的容器
        public TextMeshProUGUI playerLabel;     // 当前操作玩家提示
        public TextMeshProUGUI confirmedLabel;  // 左侧已确认角色列表

        private MainMenuUI menu;
        private int playerCount;
        private int currentPlayerIndex; // 当前操作玩家（0 起）

        // 剩余可抽的角色牌堆：不含「当前手持」与「已确认」的角色
        private readonly List<CharacterCard> pool = new List<CharacterCard>();
        // 各玩家已确认的角色，下标 i 即玩家 i+1
        private readonly List<CharacterCard> confirmed = new List<CharacterCard>();
        // 当前玩家抽到、待确认的角色（不在牌堆中）
        private CharacterCard current;

        /// <summary>已确认的角色（下标 i 即玩家 i+1）。</summary>
        public IReadOnlyList<CharacterCard> Confirmed => confirmed;

        /// <summary>当前操作玩家序号（1 起）。</summary>
        public int CurrentPlayerNumber => currentPlayerIndex + 1;

        /// <summary>当前抽到、待确认的角色；未抽取/已全部确认时为 null。</summary>
        public CharacterCard CurrentCharacter => current;

        /// <summary>剩余牌堆张数（不含当前手持与已确认的角色）。</summary>
        public int PoolCount => pool.Count;

        private void OnEnable()
        {
            menu = GetComponentInParent<MainMenuUI>();
            playerCount = menu != null ? menu.SelectedPlayerCount : 0;
            if (playerCount < 2 || playerCount > 4)
            {
                Debug.LogError($"[CharacterSelectUI] 无效的玩家人数 {playerCount}，无法开始角色选取（本面板应经人数选择进入）");
                return;
            }
            BeginSelection();
        }

        /// <summary>开始一轮全新选取：重置牌堆与已确认列表，玩家1先抽。</summary>
        private void BeginSelection()
        {
            confirmed.Clear();
            pool.Clear();
            pool.AddRange(CardDatabase.CharacterCards);
            currentPlayerIndex = 0;
            Debug.Log($"[CharacterSelectUI] 开始角色选取：{playerCount} 名玩家轮流抽取，共 {pool.Count} 张角色牌（全队唯一）");
            DrawCharacter();
            RefreshAll();
        }

        /// <summary>「更换角色」按钮：当前角色放回牌堆，随机抽一张不相同的角色。
        /// （抽新的时旧角色尚不在牌堆中，故新角色必然与旧角色不相同；
        /// 已确认的角色不在牌堆中，任何玩家都不会换到他人已确认的角色。）</summary>
        public void SwapCharacter()
        {
            if (current == null) return;
            var previous = current;
            DrawCharacter();      // 先抽新的（牌堆中不含 previous，必不相同）
            pool.Add(previous);   // 再把旧角色放回牌堆，后续玩家仍可能抽到
            RefreshCard();
            PlayClick();
            Debug.Log($"[CharacterSelectUI] 玩家{currentPlayerIndex + 1} 更换角色：{previous.cardName} → {current.cardName}（剩余牌堆 {pool.Count} 张）");
        }

        /// <summary>「确认角色」按钮：锁定当前玩家的角色并轮到下一位；
        /// 全部玩家确认后交给 MainMenuUI 进入游戏模式选择。</summary>
        public void ConfirmCharacter()
        {
            if (current == null) return;
            confirmed.Add(current);
            Debug.Log($"[CharacterSelectUI] 玩家{currentPlayerIndex + 1} 确认角色：{current.cardName}（已确认 {confirmed.Count}/{playerCount}）");
            current = null;
            currentPlayerIndex++;
            if (currentPlayerIndex >= playerCount)
            {
                menu.OnAllCharactersConfirmed(confirmed);
                return;
            }
            DrawCharacter();
            RefreshAll();
            PlayClick();
        }

        private void DrawCharacter()
        {
            int index = Random.Range(0, pool.Count);
            current = pool[index];
            pool.RemoveAt(index);
        }

        // —— 界面刷新 ——

        private void RefreshAll()
        {
            RefreshPlayerLabel();
            RefreshCard();
            RefreshConfirmedList();
        }

        private void RefreshPlayerLabel()
        {
            if (playerLabel != null)
                playerLabel.text = $"玩家{currentPlayerIndex + 1} · 抽取你的角色";
        }

        private void RefreshCard()
        {
            if (cardRoot == null) return;
            for (int i = cardRoot.childCount - 1; i >= 0; i--)
                Destroy(cardRoot.GetChild(i).gameObject);
            if (current != null) BuildCharacterCard(cardRoot, current);
        }

        private void RefreshConfirmedList()
        {
            if (confirmedLabel == null) return;
            var text = new StringBuilder("已选角色");
            if (confirmed.Count == 0)
            {
                text.Append("\n（暂无）");
            }
            else
            {
                for (int i = 0; i < confirmed.Count; i++)
                    text.Append("\n玩家").Append(i + 1).Append("·").Append(confirmed[i].cardName);
            }
            confirmedLabel.text = text.ToString();
        }

        /// <summary>面朝上的角色卡：类型（角色牌）+ 角色名 + 体力值（初始/最大）+ 等级占位信息。</summary>
        private static void BuildCharacterCard(RectTransform parent, CharacterCard card)
        {
            const float cardW = 340f, cardH = 500f;
            var faceColor = CardDatabase.TypeColor(card.type);

            var root = NewRect("CharacterCard", parent);
            PlaceCenter(root, Vector2.zero, new Vector2(cardW, cardH));

            var border = NewImage("Face", root, faceColor);
            PlaceCenter(border.rectTransform, Vector2.zero, new Vector2(cardW, cardH));
            var inner = NewImage("FaceInner", root, faceColor * 0.55f);
            StretchFull(inner.rectTransform, 5f);

            var typeLabel = NewText("Type", root, CardDatabase.TypeName(card.type), 24f, new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.Center);
            PlaceFromTop(typeLabel.rectTransform, 0f, 14f, new Vector2(cardW - 40f, 32f));

            var nameLabel = NewText("Name", root, card.cardName, 44f, new Color(0.98f, 0.98f, 1f), TextAlignmentOptions.Center, FontStyles.Bold);
            PlaceFromTop(nameLabel.rectTransform, 0f, 56f, new Vector2(cardW - 30f, 64f));

            var healthLabel = NewText("Health", root, $"体力 {card.initialHealth}/{card.maxHealth}", 28f, new Color(1f, 0.85f, 0.35f), TextAlignmentOptions.Center, FontStyles.Bold);
            PlaceFromTop(healthLabel.rectTransform, 0f, 132f, new Vector2(cardW - 40f, 40f));

            var infoLabel = NewText("Info", root, card.info, 20f, new Color(0.92f, 0.93f, 0.96f), TextAlignmentOptions.TopLeft);
            infoLabel.margin = new Vector4(6f, 2f, 6f, 0f);
            PlaceFromTop(infoLabel.rectTransform, 0f, 188f, new Vector2(cardW - 36f, cardH - 204f));
        }

        private void PlayClick()
        {
            var source = GetComponentInParent<AudioSource>();
            if (source != null && source.clip != null) source.Play();
        }

        // —— 通用 UI 构建辅助（全部使用 TextMeshPro，禁止 legacy Text）——

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        private static Image NewImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI NewText(string name, Transform parent, string content, float fontSize, Color color, TextAlignmentOptions alignment, FontStyles style = FontStyles.Normal)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            var label = go.GetComponent<TextMeshProUGUI>();
            label.text = content;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = alignment;
            label.fontStyle = style;
            label.enableWordWrapping = true;
            label.raycastTarget = false;
            return label;
        }

        /// <summary>以父物体中心为原点居中放置。</summary>
        private static void PlaceCenter(RectTransform rt, Vector2 center, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = center;
            rt.sizeDelta = size;
        }

        /// <summary>锚定父物体上边缘放置（topOffset 为距上边缘的向内距离）。</summary>
        private static void PlaceFromTop(RectTransform rt, float x, float topOffset, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(x, -topOffset);
            rt.sizeDelta = size;
        }

        private static void StretchFull(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }
    }
}
