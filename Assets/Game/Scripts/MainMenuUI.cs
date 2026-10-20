using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 主界面入口：管理主界面/玩家人数选择/角色选取/游戏模式选择/剧本选择/场地布局六个面板的切换，
    /// 记录玩家人数（2/3/4）、各玩家已确认的角色、游戏模式与所选剧本。
    /// 开局流程：主界面（开始游戏）→ 玩家人数选择（2/3/4名玩家）→ 角色选取（从玩家1起轮流随机抽角色）
    /// → 游戏模式选择（故事/雇佣兵/对抗）→ 剧本选择（三选一）→ 场地布局
    /// （以所选模式为大标题、按所选剧本生成资源区）。
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        public enum GameMode
        {
            None = 0,
            Story = 1,
            Mercenary = 2,
            Battle = 3,
        }

        [Header("面板（由场景搭建脚本赋值）")]
        public GameObject mainMenuPanel;
        public GameObject playerCountPanel;
        public GameObject characterSelectPanel;
        public GameObject gameModePanel;
        public GameObject scenarioPanel;
        public GameObject storyModePanel;

        /// <summary>按钮点击反馈音，由挂载在同一物体上的 AudioSource 承载。</summary>
        public AudioSource clickAudio;

        /// <summary>最近一次选择的游戏模式；未选择或已返回主界面时为 None。</summary>
        public GameMode SelectedMode { get; private set; } = GameMode.None;

        /// <summary>已选的玩家人数（2/3/4）；未选择或已返回主界面时为 0。</summary>
        public int SelectedPlayerCount { get; private set; }

        /// <summary>各玩家已确认的角色（下标 i 即玩家 i+1 的角色）；流程重开或返回主界面时清空。</summary>
        public IReadOnlyList<CharacterCard> SelectedCharacters => selectedCharacters;
        private readonly List<CharacterCard> selectedCharacters = new List<CharacterCard>();

        /// <summary>已选剧本下标；-1 = 未选择（流程重开或返回主界面时复位）。</summary>
        public int SelectedScenarioIndex { get; private set; } = -1;

        /// <summary>已选剧本；未选择或下标越界时为 null。</summary>
        public ScenarioData SelectedScenario
        {
            get
            {
                var scenarios = ScenarioDatabase.Scenarios;
                if (SelectedScenarioIndex < 0 || SelectedScenarioIndex >= scenarios.Length) return null;
                return scenarios[SelectedScenarioIndex];
            }
        }

        /// <summary>游戏模式中文名（用于模式选择按钮与场地大标题）。</summary>
        public static string ModeName(GameMode mode)
        {
            switch (mode)
            {
                case GameMode.Story: return "故事模式";
                case GameMode.Mercenary: return "雇佣兵模式";
                case GameMode.Battle: return "对抗模式";
                default: return string.Empty;
            }
        }

        /// <summary>主界面「开始游戏」：开始一局新的开局流程，先进入玩家人数选择。</summary>
        public void StartGame()
        {
            ResetStoryFlowState();
            ShowPanel(playerCountPanel);
            PlayClick();
            Debug.Log("[MainMenuUI] 开始游戏：进入玩家人数选择");
        }

        /// <summary>玩家人数选择按钮（2/3/4）：选定人数后进入角色选取。</summary>
        public void SelectPlayerCount(int count)
        {
            if (count != 2 && count != 3 && count != 4)
            {
                Debug.LogWarning($"[MainMenuUI] 无效的玩家人数：{count}（仅支持 2/3/4）");
                return;
            }
            SelectedPlayerCount = count;
            selectedCharacters.Clear();
            ShowPanel(characterSelectPanel);
            PlayClick();
            Debug.Log($"[MainMenuUI] 已选择 {count} 名玩家，进入角色选取");
        }

        /// <summary>角色选取完成（由 CharacterSelectUI 在最后一位玩家确认后调用）：
        /// 记录各玩家角色，进入游戏模式选择。</summary>
        public void OnAllCharactersConfirmed(List<CharacterCard> characters)
        {
            selectedCharacters.Clear();
            if (characters != null) selectedCharacters.AddRange(characters);
            ShowPanel(gameModePanel);
            PlayClick();
            var names = new System.Text.StringBuilder();
            for (int i = 0; i < selectedCharacters.Count; i++)
            {
                if (i > 0) names.Append("、");
                names.Append("玩家").Append(i + 1).Append("·").Append(selectedCharacters[i].cardName);
            }
            Debug.Log($"[MainMenuUI] 角色选取完成：{names}，进入游戏模式选择");
        }

        /// <summary>游戏模式选择按钮（1=故事 2=雇佣兵 3=对抗）：选定模式后进入剧本选择。</summary>
        public void SelectGameMode(int modeValue)
        {
            if (modeValue < 1 || modeValue > 3)
            {
                Debug.LogWarning($"[MainMenuUI] 无效的游戏模式值：{modeValue}（仅支持 1=故事 2=雇佣兵 3=对抗）");
                return;
            }
            SelectedMode = (GameMode)modeValue;
            ShowPanel(scenarioPanel);
            PlayClick();
            Debug.Log($"[MainMenuUI] 已选择游戏模式：{ModeName(SelectedMode)}，进入剧本选择");
        }

        /// <summary>剧本选择按钮：选定剧本后进入场地布局界面。</summary>
        public void SelectScenario(int index)
        {
            var scenarios = ScenarioDatabase.Scenarios;
            if (index < 0 || index >= scenarios.Length)
            {
                Debug.LogWarning($"[MainMenuUI] 无效的剧本下标：{index}（共 {scenarios.Length} 个剧本）");
                return;
            }
            SelectedScenarioIndex = index;
            ShowPanel(storyModePanel);
            PlayClick();
            Debug.Log($"[MainMenuUI] 已选择剧本：{scenarios[index].scenarioName}，进入场地布局");
        }

        /// <summary>任意中间步骤的返回按钮：随时回到主界面并复位整个开局流程。</summary>
        public void BackToMainMenu()
        {
            ResetStoryFlowState();
            ShowPanel(mainMenuPanel);
            PlayClick();
            Debug.Log("[MainMenuUI] 返回主界面");
        }

        /// <summary>重置开局流程状态（模式/人数/角色/剧本）。</summary>
        private void ResetStoryFlowState()
        {
            SelectedMode = GameMode.None;
            SelectedPlayerCount = 0;
            SelectedScenarioIndex = -1;
            selectedCharacters.Clear();
        }

        private void ShowPanel(GameObject panel)
        {
            if (mainMenuPanel != null)
                mainMenuPanel.SetActive(panel == mainMenuPanel);
            if (playerCountPanel != null)
                playerCountPanel.SetActive(panel == playerCountPanel);
            if (characterSelectPanel != null)
                characterSelectPanel.SetActive(panel == characterSelectPanel);
            if (gameModePanel != null)
                gameModePanel.SetActive(panel == gameModePanel);
            if (scenarioPanel != null)
                scenarioPanel.SetActive(panel == scenarioPanel);
            if (storyModePanel != null)
                storyModePanel.SetActive(panel == storyModePanel);
        }

        private void PlayClick()
        {
            if (clickAudio != null && clickAudio.clip != null)
            {
                clickAudio.Play();
            }
        }
    }
}
