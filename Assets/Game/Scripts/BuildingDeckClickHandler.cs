using UnityEngine;
using UnityEngine.EventSystems;

namespace Game
{
    /// <summary>大厦牌堆的点击（2026-10-18 用户定义）：点击弹出探索二次确认框；探索进行中点击=翻开牌顶结算。</summary>
    public class BuildingDeckClickHandler : MonoBehaviour, IPointerClickHandler
    {
        private StoryModeUI story;

        public void Initialize(StoryModeUI story)
        {
            this.story = story;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (story != null) story.OnBuildingDeckClicked();
        }
    }
}
