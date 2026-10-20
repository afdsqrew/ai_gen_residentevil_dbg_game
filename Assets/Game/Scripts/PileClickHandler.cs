using UnityEngine;
using UnityEngine.EventSystems;

namespace Game
{
    /// <summary>资源区牌堆的点击（2026-10-18 用户定义）：点击牌堆弹出购买确认框。</summary>
    public class PileClickHandler : MonoBehaviour, IPointerClickHandler
    {
        private StoryModeUI story;
        private int pileIndex;

        public void Initialize(StoryModeUI story, int pileIndex)
        {
            this.story = story;
            this.pileIndex = pileIndex;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (story != null) story.OnResourcePileClicked(pileIndex);
        }
    }
}
