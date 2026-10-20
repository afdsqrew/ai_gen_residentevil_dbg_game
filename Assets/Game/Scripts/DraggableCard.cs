using UnityEngine;
using UnityEngine.EventSystems;

namespace Game
{
    /// <summary>卡牌拖拽（2026-10-18 用户定义）：手牌区迷你卡拖到游戏区=打出并执行效果（拖拽不落游戏区则弹回）；
    /// 游戏区所出牌拖回手牌区=收回并完整回退效果（重算弹药与黄金，2026-10-18）；
    /// 非玩家1回合禁止拖拽（多玩家区域布局待实现，手牌区/游戏区只显示玩家1的牌）。</summary>
    public class DraggableCard : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private StoryModeUI story;
        private RectTransform dragRoot;
        private Transform originalParent;
        private Vector2 originalPosition;
        private bool dragging;

        /// <summary>本迷你卡对应的手牌（同一 CardData 实例可能在手中有多份副本，打出一份即可）。</summary>
        public CardData Card { get; private set; }

        /// <summary>游戏区所出牌的打出记录（非空=本卡为游戏区卡：拖到手牌区=收回并按记录回退效果）。</summary>
        public GameState.PlayedCardRecord Record { get; private set; }

        public void Initialize(StoryModeUI story, CardData card)
        {
            this.story = story;
            Card = card;
            Record = null;
        }

        /// <summary>游戏区卡初始化（携带打出记录——收回时按记录完整回退）。</summary>
        public void InitializeForRetract(StoryModeUI story, GameState.PlayedCardRecord record)
        {
            this.story = story;
            Record = record;
            Card = record != null ? record.card : null;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            // 手牌区/游戏区当前显示玩家1的牌（多玩家布局待实现）——仅玩家1回合允许拖拽
            if (story == null || story.State == null || story.State.players.Count == 0 || story.State.CurrentPlayerIndex != 0)
            {
                if (story != null && story.State != null && story.State.players.Count > 0)
                    Debug.Log("[DraggableCard] 当前非玩家1回合，暂不能拖拽（多玩家布局待实现）");
                dragging = false;
                return;
            }

            dragging = true;
            originalParent = transform.parent;
            originalPosition = ((RectTransform)transform).anchoredPosition;
            dragRoot = GetComponentInParent<Canvas>().transform as RectTransform;
            transform.SetParent(dragRoot, false); // 提到画布顶层，避免被其他区域遮挡
            transform.SetAsLastSibling();
            ((RectTransform)transform).anchoredPosition = ToCanvasLocal(eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!dragging) return;
            ((RectTransform)transform).anchoredPosition = ToCanvasLocal(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!dragging) return;
            dragging = false;
            if (Record != null)
            {
                // 游戏区 → 手牌区 = 收回（2026-10-18 用户定义：拖回重算弹药与黄金，完整回退效果）
                if (story.IsPointerOverHandZone(eventData.position) && story.TryRetractDraggedCard(Record))
                {
                    Destroy(gameObject); // 玩家区域条已按最新状态重建，本拖拽对象移除
                    return;
                }
            }
            else if (story.IsPointerOverPlayZone(eventData.position) && story.TryPlayDraggedCard(Card, (RectTransform)transform))
            {
                // 手牌 → 游戏区 = 打出：打出成功后玩家区域条已按最新状态重建，本拖拽对象移除
                Destroy(gameObject);
                return;
            }
            // 未落在目标区域或不可打出/收回：弹回原位
            transform.SetParent(originalParent, false);
            transform.SetAsLastSibling();
            ((RectTransform)transform).anchoredPosition = originalPosition;
        }

        private Vector2 ToCanvasLocal(Vector2 screenPoint)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(dragRoot, screenPoint, null, out var local);
            return local;
        }
    }
}
