using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

namespace Game.Editor
{
    /// <summary>
    /// 场景搭建脚本：为 MainScene 搭建完整开局流程骨架——
    /// 0) MainMenuPanel：主界面单一「开始游戏」入口（原三模式按钮已移除，模式在角色选取后统一选择）；
    /// 1) PlayerCountPanel：玩家人数选择（2/3/4名玩家三按钮 + 返回主界面），按钮持久化绑定 MainMenuUI；
    /// 2) CharacterSelectPanel：角色选取（当前玩家提示 + 角色卡容器 + 左侧已选列表 + 更换/确认角色按钮，
    ///    按钮持久化绑定 CharacterSelectUI；卡面与列表内容运行时生成）；
    /// 3) GameModePanel：游戏模式选择（故事/雇佣兵/对抗三按钮，沿用原主界面三按钮配色，持久化绑定 MainMenuUI.SelectGameMode）；
    /// 4) ScenarioPanel：剧本选择（按 ScenarioDirectory 生成剧本按钮 + 占位简介，持久化绑定 MainMenuUI.SelectScenario）；
    /// 5) StoryModePanel 场地布局骨架：中央左侧 BuildingZone（大厦区）、右侧 ResourceZone（资源区）、
    ///    顶部 ScenarioBar（所选剧本名显示，场内不再切换）、底部 PlayerArea（玩家区域条：
    ///    手牌区/游戏区/弃牌区/牌库区），并挂载 StoryModeUI、赋值区域引用与顶栏人数标签。
    /// 内容物由 StoryModeUI/CharacterSelectUI 在运行时按 CardDatabase/ScenarioDatabase 生成。
    /// 可重复执行（幂等）。
    /// </summary>
    public static class StorySceneSetup
    {
        private const string ScenePath = "Assets/Game/Scenes/MainScene.scene";

        // 主界面按钮同款风格基准
        private static readonly Color ButtonBg = new Color(0.17f, 0.29f, 0.43f, 1f);
        private static readonly Color ButtonFg = new Color(0.94f, 0.95f, 0.97f, 1f);
        private static readonly Color TitleFg = new Color(0.91f, 0.93f, 0.96f, 1f);
        // 游戏模式按钮配色（沿用原主界面三模式按钮）
        private static readonly Color ModeMercenaryBg = new Color(0.43f, 0.33f, 0.16f, 1f);
        private static readonly Color ModeBattleBg = new Color(0.43f, 0.19f, 0.22f, 1f);
        // 剧本简介文字色（弱化说明文字）
        private static readonly Color DescFg = new Color(0.78f, 0.80f, 0.84f, 1f);

        [MenuItem("Tools/Game 场景搭建/重搭故事模式界面")]
        public static void Build()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (scene.isDirty) EditorSceneManager.SaveScene(scene);
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var canvas = UnityEngine.Object.FindObjectOfType<UnityEngine.Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[StorySceneSetup] 场景中未找到 Canvas，无法搭建");
                return;
            }
            var menuUI = canvas.GetComponent<MainMenuUI>();
            if (menuUI == null)
            {
                Debug.LogError("[StorySceneSetup] Canvas 根未挂载 MainMenuUI，无法搭建");
                return;
            }

            // 交互保障（2026-10-18 打牌拖拽需要）：画布 GraphicRaycaster + 场景 EventSystem（幂等）
            if (canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
                canvas.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            if (UnityEngine.Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
                Debug.Log("[StorySceneSetup] 已创建 EventSystem（按钮点击/手牌拖拽交互需要）");
            }

            BuildMainMenuEntry(canvas.transform, menuUI);
            BuildPlayerCountPanel(canvas.transform, menuUI);
            BuildCharacterSelectPanel(canvas.transform, menuUI);
            BuildGameModePanel(canvas.transform, menuUI);
            BuildScenarioPanel(canvas.transform, menuUI);
            BuildStoryFieldLayout(canvas.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[StorySceneSetup] 开局流程界面已搭建并保存：主菜单开始游戏入口 + 人数选择 + 角色选取 + 游戏模式选择 + 剧本选择 + 场地布局骨架");
        }

        // —— 主界面入口（单一「开始游戏」；游戏模式在角色选取后统一选择）——

        private static void BuildMainMenuEntry(Transform canvasRoot, MainMenuUI menuUI)
        {
            var panel = FindDeep(canvasRoot, "MainMenuPanel");
            if (panel == null)
            {
                Debug.LogError("[StorySceneSetup] 未找到 MainMenuPanel，无法改造主菜单入口");
                return;
            }

            // 移除旧的模式按钮（模式选择已移至角色选取之后的 GameModePanel）
            foreach (var buttonName in new[] { "BtnMercenaryMode", "BtnBattleMode" })
            {
                var old = FindDeep(panel, buttonName);
                if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            }

            // BtnStoryMode 改造为「开始游戏」；已改造过（BtnStartGame）则仅刷新绑定
            var buttonTransform = FindDeep(panel, "BtnStartGame");
            if (buttonTransform == null) buttonTransform = FindDeep(panel, "BtnStoryMode");
            if (buttonTransform == null)
            {
                Debug.LogError("[StorySceneSetup] 未找到主菜单开始按钮（BtnStoryMode/BtnStartGame），无法改造主菜单入口");
                return;
            }
            buttonTransform.name = "BtnStartGame";
            ((RectTransform)buttonTransform).anchoredPosition = new Vector2(0f, 0f);

            var button = buttonTransform.GetComponent<UnityEngine.UI.Button>();
            var label = FindDeep(buttonTransform, "Label");
            if (label != null)
            {
                var labelText = label.GetComponent<TextMeshProUGUI>();
                if (labelText != null) labelText.text = "开始游戏";
            }
            while (button.onClick.GetPersistentEventCount() > 0)
                UnityEventTools.RemovePersistentListener(button.onClick, 0);
            UnityEventTools.AddVoidPersistentListener(button.onClick, menuUI.StartGame);

            Debug.Log("[StorySceneSetup] MainMenuPanel 已改造：单一「开始游戏」入口（原三模式按钮已移除，持久化绑定 StartGame）");
        }

        // —— 玩家人数选择面板（「开始游戏」→ 此面板 → 角色选取）——

        private static void BuildPlayerCountPanel(Transform canvasRoot, MainMenuUI menuUI)
        {
            // 幂等清理旧面板
            var old = FindDeep(canvasRoot, "PlayerCountPanel");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);

            var panelGo = new GameObject("PlayerCountPanel", typeof(RectTransform));
            var panel = (RectTransform)panelGo.transform;
            panel.SetParent(canvasRoot, false);
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
            panelGo.SetActive(false); // 与 StoryModePanel 一致：默认隐藏，由 MainMenuUI 切换

            // 标题（与主界面标题同款布局：顶部居中）
            var titleRt = CreateLabel(panel, "TitleLabel", "选择玩家人数", 72f, TitleFg).rectTransform;
            titleRt.anchorMin = titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 0.5f);
            titleRt.anchoredPosition = new Vector2(0f, -150f);
            titleRt.sizeDelta = new Vector2(900f, 110f);

            // 左上返回主界面按钮（与 StoryModePanel 的 BtnBack 同款）
            var back = CreateButton(panel, "BtnBack", "返回主界面", 34f);
            back.anchorMin = back.anchorMax = new Vector2(0f, 1f);
            back.pivot = new Vector2(0.5f, 0.5f);
            back.anchoredPosition = new Vector2(150f, -100f);
            back.sizeDelta = new Vector2(260f, 84f);
            UnityEventTools.AddVoidPersistentListener(back.GetComponent<UnityEngine.UI.Button>().onClick, menuUI.BackToMainMenu);

            // 人数按钮组（与主界面按钮组同款：520×100，纵向三档）
            var group = new GameObject("ButtonGroup", typeof(RectTransform)).transform;
            var groupRt = (RectTransform)group;
            groupRt.SetParent(panel, false);
            groupRt.anchorMin = groupRt.anchorMax = groupRt.pivot = new Vector2(0.5f, 0.5f);
            groupRt.anchoredPosition = new Vector2(0f, -40f);
            groupRt.sizeDelta = new Vector2(560f, 420f);

            BindCountButton(CreateButton(group, "Btn2Players", "2名玩家", 42f, 120f), menuUI, 2);
            BindCountButton(CreateButton(group, "Btn3Players", "3名玩家", 42f, 0f), menuUI, 3);
            BindCountButton(CreateButton(group, "Btn4Players", "4名玩家", 42f, -120f), menuUI, 4);

            menuUI.playerCountPanel = panelGo;
            Debug.Log("[StorySceneSetup] PlayerCountPanel 已搭建：2/3/4名玩家 + 返回主界面（均持久化绑定）");
        }

        private static void BindCountButton(RectTransform buttonRt, MainMenuUI menuUI, int count)
        {
            UnityEventTools.AddIntPersistentListener(buttonRt.GetComponent<UnityEngine.UI.Button>().onClick, menuUI.SelectPlayerCount, count);
        }

        // —— 角色选取面板（玩家人数选择 → 此面板轮流抽角色 → 游戏模式选择）——

        private static void BuildCharacterSelectPanel(Transform canvasRoot, MainMenuUI menuUI)
        {
            // 幂等清理旧面板
            var old = FindDeep(canvasRoot, "CharacterSelectPanel");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);

            var panelGo = new GameObject("CharacterSelectPanel", typeof(RectTransform));
            var panel = (RectTransform)panelGo.transform;
            panel.SetParent(canvasRoot, false);
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
            panelGo.SetActive(false); // 默认隐藏，由 MainMenuUI 切换

            // 标题（与人数选择面板同款：顶部居中）
            var titleRt = CreateLabel(panel, "TitleLabel", "选取角色", 72f, TitleFg).rectTransform;
            titleRt.anchorMin = titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 0.5f);
            titleRt.anchoredPosition = new Vector2(0f, -150f);
            titleRt.sizeDelta = new Vector2(900f, 110f);

            // 左上返回主界面按钮（与其他面板同款）
            var back = CreateButton(panel, "BtnBack", "返回主界面", 34f);
            back.anchorMin = back.anchorMax = new Vector2(0f, 1f);
            back.pivot = new Vector2(0.5f, 0.5f);
            back.anchoredPosition = new Vector2(150f, -100f);
            back.sizeDelta = new Vector2(260f, 84f);
            UnityEventTools.AddVoidPersistentListener(back.GetComponent<UnityEngine.UI.Button>().onClick, menuUI.BackToMainMenu);

            // 左侧已确认角色列表（运行时由 CharacterSelectUI 填充）
            var confirmedLabel = CreateLabel(panel, "ConfirmedList", string.Empty, 28f, TitleFg);
            confirmedLabel.alignment = TextAlignmentOptions.TopLeft;
            var confirmedRt = confirmedLabel.rectTransform;
            confirmedRt.anchorMin = confirmedRt.anchorMax = confirmedRt.pivot = new Vector2(0.5f, 0.5f);
            confirmedRt.anchoredPosition = new Vector2(-700f, -60f);
            confirmedRt.sizeDelta = new Vector2(380f, 560f);

            // 中央角色卡容器（运行时生成当前抽到的角色卡）
            var cardZone = CreateZone(panel, "CardZone", new Vector2(0f, -60f), new Vector2(360f, 520f));

            // 当前操作玩家提示（运行时刷新）
            var playerLabel = CreateLabel(panel, "PlayerLabel", string.Empty, 40f, TitleFg);
            var playerRt = playerLabel.rectTransform;
            playerRt.anchorMin = playerRt.anchorMax = playerRt.pivot = new Vector2(0.5f, 0.5f);
            playerRt.anchoredPosition = new Vector2(0f, -390f);
            playerRt.sizeDelta = new Vector2(800f, 60f);

            // 更换 / 确认角色按钮（与人数按钮同款 520×100）
            var swap = CreateButton(panel, "BtnSwapCharacter", "更换角色", 42f);
            swap.anchoredPosition = new Vector2(-310f, -480f);
            var confirm = CreateButton(panel, "BtnConfirmCharacter", "确认角色", 42f);
            confirm.anchoredPosition = new Vector2(310f, -480f);

            var selectUI = panelGo.AddComponent<CharacterSelectUI>();
            selectUI.cardRoot = cardZone;
            selectUI.playerLabel = playerLabel;
            selectUI.confirmedLabel = confirmedLabel;
            UnityEventTools.AddVoidPersistentListener(swap.GetComponent<UnityEngine.UI.Button>().onClick, selectUI.SwapCharacter);
            UnityEventTools.AddVoidPersistentListener(confirm.GetComponent<UnityEngine.UI.Button>().onClick, selectUI.ConfirmCharacter);

            menuUI.characterSelectPanel = panelGo;
            Debug.Log("[StorySceneSetup] CharacterSelectPanel 已搭建：更换/确认角色按钮（持久化绑定）+ 角色卡容器 + 已选列表");
        }

        // —— 游戏模式选择面板（角色选取完成后 → 此面板 → 剧本选择）——

        private static void BuildGameModePanel(Transform canvasRoot, MainMenuUI menuUI)
        {
            // 幂等清理旧面板
            var old = FindDeep(canvasRoot, "GameModePanel");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);

            var panelGo = new GameObject("GameModePanel", typeof(RectTransform));
            var panel = (RectTransform)panelGo.transform;
            panel.SetParent(canvasRoot, false);
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
            panelGo.SetActive(false); // 默认隐藏，由 MainMenuUI 切换

            // 标题（与人数选择面板同款：顶部居中）
            var titleRt = CreateLabel(panel, "TitleLabel", "选择游戏模式", 72f, TitleFg).rectTransform;
            titleRt.anchorMin = titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 0.5f);
            titleRt.anchoredPosition = new Vector2(0f, -150f);
            titleRt.sizeDelta = new Vector2(900f, 110f);

            // 左上返回主界面按钮（与其他面板同款）
            var back = CreateButton(panel, "BtnBack", "返回主界面", 34f);
            back.anchorMin = back.anchorMax = new Vector2(0f, 1f);
            back.pivot = new Vector2(0.5f, 0.5f);
            back.anchoredPosition = new Vector2(150f, -100f);
            back.sizeDelta = new Vector2(260f, 84f);
            UnityEventTools.AddVoidPersistentListener(back.GetComponent<UnityEngine.UI.Button>().onClick, menuUI.BackToMainMenu);

            // 模式按钮组（沿用原主界面三模式按钮配色：蓝/金棕/暗红）
            var group = new GameObject("ButtonGroup", typeof(RectTransform)).transform;
            var groupRt = (RectTransform)group;
            groupRt.SetParent(panel, false);
            groupRt.anchorMin = groupRt.anchorMax = groupRt.pivot = new Vector2(0.5f, 0.5f);
            groupRt.anchoredPosition = new Vector2(0f, -40f);
            groupRt.sizeDelta = new Vector2(560f, 420f);

            BindModeButton(CreateModeButton(group, "BtnGameModeStory", "故事模式", ButtonBg, 120f), menuUI, 1);
            BindModeButton(CreateModeButton(group, "BtnGameModeMercenary", "雇佣兵模式", ModeMercenaryBg, 0f), menuUI, 2);
            BindModeButton(CreateModeButton(group, "BtnGameModeBattle", "对抗模式", ModeBattleBg, -120f), menuUI, 3);

            menuUI.gameModePanel = panelGo;
            Debug.Log("[StorySceneSetup] GameModePanel 已搭建：故事/雇佣兵/对抗三模式（持久化绑定）+ 返回主界面");
        }

        private static RectTransform CreateModeButton(Transform parent, string name, string label, Color bg, float y)
        {
            var rt = CreateButton(parent, name, label, 42f, y);
            rt.GetComponent<UnityEngine.UI.Image>().color = bg;
            return rt;
        }

        private static void BindModeButton(RectTransform buttonRt, MainMenuUI menuUI, int modeValue)
        {
            UnityEventTools.AddIntPersistentListener(buttonRt.GetComponent<UnityEngine.UI.Button>().onClick, menuUI.SelectGameMode, modeValue);
        }

        // —— 剧本选择面板（游戏模式选定后 → 此面板 → 场地布局）——

        private static void BuildScenarioPanel(Transform canvasRoot, MainMenuUI menuUI)
        {
            // 幂等清理旧面板
            var old = FindDeep(canvasRoot, "ScenarioPanel");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);

            var panelGo = new GameObject("ScenarioPanel", typeof(RectTransform));
            var panel = (RectTransform)panelGo.transform;
            panel.SetParent(canvasRoot, false);
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
            panelGo.SetActive(false); // 默认隐藏，由 MainMenuUI 切换

            // 标题（与人数选择面板同款：顶部居中）
            var titleRt = CreateLabel(panel, "TitleLabel", "选择剧本", 72f, TitleFg).rectTransform;
            titleRt.anchorMin = titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 0.5f);
            titleRt.anchoredPosition = new Vector2(0f, -150f);
            titleRt.sizeDelta = new Vector2(900f, 110f);

            // 左上返回主界面按钮（与其他面板同款）
            var back = CreateButton(panel, "BtnBack", "返回主界面", 34f);
            back.anchorMin = back.anchorMax = new Vector2(0f, 1f);
            back.pivot = new Vector2(0.5f, 0.5f);
            back.anchoredPosition = new Vector2(150f, -100f);
            back.sizeDelta = new Vector2(260f, 84f);
            UnityEventTools.AddVoidPersistentListener(back.GetComponent<UnityEngine.UI.Button>().onClick, menuUI.BackToMainMenu);

            // 剧本按钮组：每个剧本一个按钮 + 简介一行（按钮持久化绑定 SelectScenario(i)）
            var group = new GameObject("ButtonGroup", typeof(RectTransform)).transform;
            var groupRt = (RectTransform)group;
            groupRt.SetParent(panel, false);
            groupRt.anchorMin = groupRt.anchorMax = groupRt.pivot = new Vector2(0.5f, 0.5f);
            groupRt.anchoredPosition = new Vector2(0f, -40f);
            groupRt.sizeDelta = new Vector2(560f, 620f);

            var scenarios = ScenarioDatabase.Scenarios;
            const float pitch = 200f;
            for (int i = 0; i < scenarios.Length; i++)
            {
                float y = 130f - i * pitch;
                var button = CreateButton(group, "BtnScenario" + i, scenarios[i].scenarioName, 42f, y);
                UnityEventTools.AddIntPersistentListener(button.GetComponent<UnityEngine.UI.Button>().onClick, menuUI.SelectScenario, i);

                var desc = CreateLabel(group, "Desc" + i, scenarios[i].description, 20f, DescFg);
                var descRt = desc.rectTransform;
                descRt.anchorMin = descRt.anchorMax = descRt.pivot = new Vector2(0.5f, 0.5f);
                descRt.anchoredPosition = new Vector2(0f, y - 78f);
                descRt.sizeDelta = new Vector2(560f, 44f);
            }

            menuUI.scenarioPanel = panelGo;
            Debug.Log($"[StorySceneSetup] ScenarioPanel 已搭建：{scenarios.Length} 个剧本（含简介，持久化绑定）+ 返回主界面");
        }

        // —— 场地布局骨架 ——

        private static void BuildStoryFieldLayout(Transform canvasRoot)
        {
            var panel = FindDeep(canvasRoot, "StoryModePanel");
            if (panel == null)
            {
                Debug.LogError("[StorySceneSetup] 未找到 StoryModePanel，无法搭建场地布局");
                return;
            }

            // 幂等清理：旧占位文本与区域容器
            for (int i = panel.childCount - 1; i >= 0; i--)
            {
                var child = panel.GetChild(i);
                if (child.name == "ContentLabel" || child.name == "BuildingZone" || child.name == "ResourceZone"
                    || child.name == "ScenarioBar" || child.name == "PlayerArea" || child.name == "PlayerCountLabel")
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
            }

            // 顶栏（1920x1080，画布中心为原点）：标题上移缩窄、返回按钮上移；
            // 顶栏左中放玩家人数与已选角色标签（由 StoryModeUI 按人数/角色刷新）——
            // 位置夹在左上返回按钮（x ≤ -680）与居中大标题（x ≥ -250）之间，避免长角色名被标题字形遮挡
            var title = FindDeep(panel, "StoryTitleLabel") as RectTransform;
            if (title != null)
            {
                title.anchoredPosition = new Vector2(0f, -70f);
                title.sizeDelta = new Vector2(500f, 110f);
            }
            var back = FindDeep(panel, "BtnBack") as RectTransform;
            if (back != null) back.anchoredPosition = new Vector2(150f, -46f);

            var countLabelRt = CreateLabel(panel, "PlayerCountLabel", string.Empty, 24f, TitleFg).rectTransform;
            countLabelRt.anchorMin = countLabelRt.anchorMax = countLabelRt.pivot = new Vector2(0.5f, 0.5f);
            countLabelRt.anchoredPosition = new Vector2(-465f, 460f);
            countLabelRt.sizeDelta = new Vector2(430f, 100f);
            var countLabelTmp = countLabelRt.GetComponent<TextMeshProUGUI>();
            countLabelTmp.enableAutoSizing = true; // 玩家多、角色名长时自动缩小，避免换行溢出
            countLabelTmp.fontSizeMin = 16f;
            countLabelTmp.fontSizeMax = 24f;

            // 场地布局分区：顶栏 y 410..540；主区 y -300..400；回合条 y -336..-304（StoryModeUI 运行时生成）；玩家区域条 y -530..-340
            var building = CreateZone(panel, "BuildingZone", new Vector2(-480f, 50f), new Vector2(920f, 700f));
            var resource = CreateZone(panel, "ResourceZone", new Vector2(480f, 50f), new Vector2(920f, 700f));
            var scenarioBar = CreateZone(panel, "ScenarioBar", new Vector2(650f, 482f), new Vector2(580f, 64f));
            var playerArea = CreateZone(panel, "PlayerArea", new Vector2(0f, -435f), new Vector2(1880f, 190f));

            var storyUI = panel.GetComponent<StoryModeUI>();
            if (storyUI == null) storyUI = panel.gameObject.AddComponent<StoryModeUI>();
            storyUI.buildingZoneRoot = building;
            storyUI.resourceZoneRoot = resource;
            storyUI.scenarioBarRoot = scenarioBar;
            storyUI.playerAreaRoot = playerArea;
        }

        // —— 构建辅助（全部使用 TextMeshPro，禁止 legacy Text）——

        /// <summary>创建与主界面按钮同款的菜单按钮（圆角底图 + 居中 TMP 文本），返回其 RectTransform。</summary>
        private static RectTransform CreateButton(Transform parent, string name, string label, float fontSize, float y = 0f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = new Vector2(520f, 100f);

            var image = go.GetComponent<UnityEngine.UI.Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = UnityEngine.UI.Image.Type.Sliced;
            image.color = ButtonBg;

            var button = go.GetComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;

            var text = CreateLabel(rt, "Label", label, fontSize, ButtonFg);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            return rt;
        }

        private static TextMeshProUGUI CreateLabel(Transform parent, string name, string content, float fontSize, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            var label = go.GetComponent<TextMeshProUGUI>();
            label.text = content;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            return label;
        }

        private static RectTransform CreateZone(Transform parent, string name, Vector2 center, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = center;
            rt.sizeDelta = size;
            return rt;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
