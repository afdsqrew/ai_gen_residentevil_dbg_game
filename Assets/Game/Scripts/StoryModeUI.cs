using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// 场地布局（开局流程终点：人数选择 → 角色选取 → 游戏模式选择 → 剧本选择 → 本面板）：
    /// 大标题显示所选游戏模式（故事模式/雇佣兵模式/对抗模式）；
    /// 画面中央左侧「大厦区」+ 画面右侧「资源区」（基础资源 6 堆·全剧本共享 + 剧本牌堆·按所选剧本且带数量——如第一时间剧本 13 堆，卡底显示「共 N 张」；卡牌全部面朝上）；
    /// 大厦区以面朝下的「大厦牌堆」占位，洗切成分固定为 35 感染牌 + 2 标记牌、共 37 张（CardDatabase.BuildingDeck）；
    /// 顶部剧本条显示所选剧本名（剧本在进场地前的剧本选择界面已选定，场内不再切换）；
    /// 底部玩家区域条——玩家1·手牌区 / 游戏区 / 弃牌区 / 牌库区（原「库存区」与「牌库区」为同一堆，2026-10-18 确认合并）
    /// （打出的牌面朝上进入游戏区；回合结束：游戏区非附着牌+手牌弃入弃牌堆，再从牌库摸 5 张——牌库不足时弃牌堆洗切面朝下成为新牌库）；
    /// 顶栏左中显示玩家人数与各玩家已确认的角色（PlayerCountLabel，由 MainMenuUI.SelectedPlayerCount/SelectedCharacters 驱动）。
    /// 开局流程（2026-10-18，GameState.Setup）：进场时按所选剧本设置资源区后，每人从基础资源暗抽
    /// 7×弹药x10 + 手枪堆 1 张 + 刀堆 2 张 共 10 张，洗切后面朝下放入自己的牌库（原「库存区」——同一堆，2026-10-18 确认合并），再从牌库顶摸 5 张为初始手牌
    /// （基础资源剩余张数、玩家1 手牌与牌库堆实时渲染；初始玩家为玩家1）。
    /// 回合结构（2026-10-18）：回合开始时当前玩家五项点数各 1（行动/购买/探索/物品/角色——对应动作各耗 1 点，耗尽即不能再做）；
    /// 「结束回合」按钮或五点全耗尽都会结束回合——游戏区非附着牌+手中牌弃入弃牌堆，再从牌库摸 5 张
    /// （牌库不足时先摸空，弃牌堆洗切面朝下成为新牌库后摸剩余），循环轮换到下一位玩家并重置点数。
    /// 打牌（2026-10-18 用户定义）：手牌区迷你卡可拖拽（DraggableCard）——落在游戏区即打出：牌面朝上进入游戏区并执行效果
    /// （行动牌耗行动点/物品牌耗物品点，为 0 时不能打；效果执行见 GameState.ExecutePlayEffects）。
    /// 手牌区/游戏区卡牌与资源区一致显示详细内容（类型/名称/消耗/奖励效果——2026-10-18 用户要求）。
    /// 收回（2026-10-18 用户定义）：游戏区所出牌可拖回手牌区=收回，按打出记录完整回退（弹药/黄金/奖章/体力/行动点加成与回合战斗加成回退、
    /// 摸到的牌放回牌库顶、消耗点数与武器填装弹药返还）；探索结算后使用过的武器锁定不可收回。
    /// 购买（2026-10-18 用户定义）：点击资源区牌堆弹出购买确认框——确认后扣 1 购买点与等量黄金，买到的牌进入弃牌堆（暗抽）。
    /// 探索（2026-10-18 用户定义）：点击大厦牌堆弹出二次确认框——确认后扣 1 探索点进入战斗准备（只能打出武器牌、按弹药需求填装、每件一次、期间禁买/禁结束回合），
    /// 再点击牌堆翻开牌顶结算：伤害总值 ≥ 感染牌体力=打败（附着角色牌+奖章），否则战败受伤、感染牌放回牌堆底部；标记牌占位翻回；回合不因探索结束。
    /// 主区与玩家区域条之间有回合条（TurnBar）：当前回合玩家+角色名、五项点数筹码（耗尽变暗）、当前玩家状态（体力/弹药/黄金/奖章）与结束回合按钮。
    /// 挂载于 StoryModePanel，面板每次显示时按当前所选模式/剧本重建全部内容并重新开局。
    /// </summary>
    public class StoryModeUI : MonoBehaviour
    {
        [Header("区域容器（由场景搭建脚本赋值）")]
        public RectTransform buildingZoneRoot;
        public RectTransform resourceZoneRoot;
        public RectTransform scenarioBarRoot;   // 顶部剧本条
        public RectTransform playerAreaRoot;     // 底部玩家区域条

        // —— 武器弹药检查反馈（2026-10-18 用户定义：拖武器入游戏区——不足=报错音+暗红边框闪烁；满足=确认音+绿色边框环绕的「弹药满足状态」）——
        private static AudioClip ammoOkClip;        // Assets/Game/Resources/Audio/SFX/ammo_ok.wav（MCP 生成，运行时加载）
        private static AudioClip ammoNotEnoughClip; // Assets/Game/Resources/Audio/SFX/ammo_not_enough.wav
        private static readonly Color AmmoOkGreen = new Color(0.36f, 0.95f, 0.45f, 1f);
        private static readonly Color AmmoNotEnoughDarkRed = new Color(0.62f, 0.13f, 0.10f, 1f);

        /// <summary>当前剧本：来自 MainMenuUI.SelectedScenario（剧本选择界面已选定）；未选择时为 null。</summary>
        public ScenarioData CurrentScenario
        {
            get
            {
                var menu = GetComponentInParent<MainMenuUI>();
                return menu != null ? menu.SelectedScenario : null;
            }
        }

        /// <summary>本局对局状态（进场时创建并执行开局流程；详见 GameState）。</summary>
        public GameState State { get; private set; }

        private void OnEnable()
        {
            // 开局步骤1+2（2026-10-18）：设置资源区牌堆 → 分发各玩家初始牌库与手牌（基础资源剩余随拿取减少）
            var menu = GetComponentInParent<MainMenuUI>();
            int playerCount = menu != null ? menu.SelectedPlayerCount : 0;
            State = new GameState();
            State.Setup(Mathf.Clamp(playerCount, 0, 4), menu != null ? menu.SelectedCharacters : null, CurrentScenario); // 0 = 未选人数（防御）；体力按所选角色初始化；剧本牌堆进入运行时状态

            lastSeenTurnIndex = -1; // 新对局：首次回合条刷新即触发「轮到x的回合」漂浮提示
            UpdatePlayerCountLabel();
            UpdateModeTitle();
            BuildAll();
            StartAiTurnWatcher();
        }

        /// <summary>玩家区域条当前展示的玩家=当前回合玩家（2026-10-18 用户定义：玩家1结束回合后四区标题与内容同步切到下一位玩家）；未开局时为 null。</summary>
        private PlayerState DisplayPlayer => State != null && State.players.Count > 0 ? State.CurrentPlayer : null;

        private int lastSeenTurnIndex = -1; // 上次回合条刷新时见到的玩家下标——变化时弹「轮到x的回合」漂浮提示（2026-10-18 用户要求；OnEnable 重置 -1）

        // —— 非真人玩家（AI）回合自动过（2026-10-18 用户定义：玩家2等目前非真人控制——回合展示片刻后自动消耗五项点数，
        //    由既有「五项点数全部耗尽自动结束回合」机制触发结束回合并轮换到下一位；AI 的正式行为待定义）——

        private Coroutine aiTurnWatcher;
        private const float AiTurnVisibleSeconds = 2f; // AI 回合展示时长（秒，可调）

        private void StartAiTurnWatcher()
        {
            if (aiTurnWatcher != null) StopCoroutine(aiTurnWatcher);
            aiTurnWatcher = StartCoroutine(RunAiTurnWatcher());
        }

        private void OnDisable()
        {
            if (aiTurnWatcher != null) { StopCoroutine(aiTurnWatcher); aiTurnWatcher = null; }
        }

        private System.Collections.IEnumerator RunAiTurnWatcher()
        {
            var poll = new WaitForSeconds(0.25f);
            var display = new WaitForSeconds(AiTurnVisibleSeconds);
            while (true)
            {
                yield return poll;
                if (State == null || State.players.Count == 0 || State.Exploring) continue;
                if (State.CurrentPlayerIndex == 0) continue; // 玩家1=真人操作
                var aiPlayer = State.CurrentPlayer;
                yield return display; // AI 回合展示窗口（区域条显示该玩家的手牌/游戏区/弃牌区/牌库区）
                if (State.CurrentPlayer != aiPlayer) continue; // 展示期间回合已被提前结束——勿重复触发
                for (int i = 0; i < 5 && State.CurrentPlayer == aiPlayer; i++)
                    State.TryConsume((TurnPoint)i); // 自动消耗五点——第五点耗尽经既有机制自动 EndTurn 轮换
                BuildAll(); // 界面跟随轮换后的新当前玩家
            }
        }

        /// <summary>按所选玩家人数刷新顶栏标签（“N名玩家”），人数未选择（0）时清空；
        /// 已完成角色选取时，第二行列出各玩家确认的角色。</summary>
        private void UpdatePlayerCountLabel()
        {
            var labelTransform = transform.Find("PlayerCountLabel");
            if (labelTransform == null) return;
            var label = labelTransform.GetComponent<TextMeshProUGUI>();
            if (label == null) return;
            var menu = GetComponentInParent<MainMenuUI>();
            int count = menu != null ? menu.SelectedPlayerCount : 0;
            if (count <= 0)
            {
                label.text = string.Empty;
                return;
            }
            var text = new StringBuilder();
            text.Append(count).Append("名玩家");
            var characters = menu.SelectedCharacters;
            for (int i = 0; i < characters.Count; i++)
            {
                text.Append(i == 0 ? "\n" : "  ");
                text.Append("玩家").Append(i + 1).Append("·").Append(characters[i].cardName);
            }
            label.text = text.ToString();
        }

        /// <summary>按已选游戏模式刷新场地大标题（故事模式/雇佣兵模式/对抗模式）；未选择模式时不改动。</summary>
        private void UpdateModeTitle()
        {
            var titleTransform = transform.Find("StoryTitleLabel");
            if (titleTransform == null) return;
            var label = titleTransform.GetComponent<TextMeshProUGUI>();
            if (label == null) return;
            var menu = GetComponentInParent<MainMenuUI>();
            if (menu == null || menu.SelectedMode == MainMenuUI.GameMode.None) return;
            label.text = MainMenuUI.ModeName(menu.SelectedMode);
        }

        /// <summary>卡牌/剧本数据调整后调用可重建整个布局；面板当前显示时立即生效。</summary>
        public void Rebuild()
        {
            if (isActiveAndEnabled) BuildAll();
        }

        private void BuildAll()
        {
            if (buildingZoneRoot == null || resourceZoneRoot == null)
            {
                Debug.LogError("[StoryModeUI] 区域容器未赋值，无法生成场地布局");
                return;
            }
            ClearChildren(buildingZoneRoot);
            ClearChildren(resourceZoneRoot);
            ClearChildren(scenarioBarRoot);
            ClearChildren(playerAreaRoot);
            BuildBuildingZone();
            BuildResourceZone();
            BuildScenarioBar();
            BuildPlayerArea();
            BuildTurnBar();
            var scenario = CurrentScenario;
            int scenarioPileCount = scenario != null && scenario.resourcePiles != null ? scenario.resourcePiles.Length : 0;
            Debug.Log($"[StoryModeUI] 场地布局已生成：剧本「{(scenario != null ? scenario.scenarioName : "无")}」，资源区 {CardDatabase.BasicResources.Length + scenarioPileCount} 堆（基础资源 {CardDatabase.BasicResources.Length} + 剧本牌堆 {scenarioPileCount}，全部面朝上），玩家区域 ×4（手牌/游戏/弃牌/牌库）");
        }

        // —— 顶部剧本条：显示所选剧本名（剧本在进场地前的剧本选择界面已选定，场内不再切换）——

        private void BuildScenarioBar()
        {
            if (scenarioBarRoot == null) return;
            ClearChildren(scenarioBarRoot);
            var scenario = CurrentScenario;
            if (scenario == null) return;
            var label = NewText("ScenarioName", scenarioBarRoot, scenario.scenarioName, 26f, new Color(1f, 0.93f, 0.78f), TextAlignmentOptions.Center, FontStyles.Bold);
            PlaceCenter(label.rectTransform, Vector2.zero, new Vector2(400f, 56f));
        }

        // —— 大厦区（画面中央左侧）——

        private void BuildBuildingZone()
        {
            var zone = buildingZoneRoot;
            CreateZoneFrame(zone, new Color(0.09f, 0.11f, 0.16f, 0.92f), new Color(0.36f, 0.44f, 0.58f, 0.9f));
            CreateZoneTitle(zone, "大厦区", new Color(0.83f, 0.89f, 1f));

            bool exploring = State != null && State.Exploring;
            int deckCount = State != null ? State.buildingDeck.Count : BuildingDeck.TotalCount;

            // 大厦牌堆：面朝下的牌背占位；可点击——弹出探索二次确认框（探索中点击=翻开牌顶结算）
            var deck = NewRect("大厦牌堆", zone);
            var deckSize = new Vector2(280f, 380f);
            PlaceCenter(deck, new Vector2(0f, -40f), deckSize);

            var back1 = NewImage("CardBack_1", deck, new Color(0.16f, 0.18f, 0.24f, 1f));
            PlaceCenter(back1.rectTransform, new Vector2(14f, -14f), deckSize);
            var back2 = NewImage("CardBack_2", deck, new Color(0.18f, 0.20f, 0.27f, 1f));
            PlaceCenter(back2.rectTransform, new Vector2(7f, -7f), deckSize);

            var border = NewImage("Back", deck, new Color(0.33f, 0.38f, 0.50f, 1f));
            border.raycastTarget = true; // 牌堆可点击（点击事件冒泡至牌堆根的 BuildingDeckClickHandler）
            PlaceCenter(border.rectTransform, Vector2.zero, deckSize);
            var inner = NewImage("BackInner", deck, new Color(0.17f, 0.19f, 0.26f, 1f));
            StretchFull(inner.rectTransform, 5f);
            deck.gameObject.AddComponent<BuildingDeckClickHandler>().Initialize(this);

            var deckName = NewText("DeckName", deck, "大厦牌堆", 40f, new Color(0.92f, 0.95f, 1f), TextAlignmentOptions.Center, FontStyles.Bold);
            PlaceCenter(deckName.rectTransform, new Vector2(0f, 26f), new Vector2(240f, 56f));
            var deckHint = NewText("DeckHint", deck,
                exploring ? "（探索中 · 点击翻开牌顶）" : $"（面朝下 · 共 {deckCount} 张）",
                18f, exploring ? new Color(1f, 0.85f, 0.35f) : new Color(1f, 1f, 1f, 0.55f), TextAlignmentOptions.Center);
            PlaceCenter(deckHint.rectTransform, new Vector2(0f, -34f), new Vector2(240f, 30f));

            var caption = NewText("ZoneCaption", zone,
                exploring
                    ? $"战斗准备：拖拽手牌中的武器打出（按弹药需求填装弹药，每件只使用一次）\n就绪后点击大厦牌堆翻开牌顶结算 · 当前伤害总值 {State.CalculateExploreDamage()}"
                    : $"洗切入牌堆：{BuildingDeck.InfectionCount} 张感染牌 · {BuildingDeck.MarkerCount} 张标记牌\n（物品牌只能在大厦中被发现）",
                24f, exploring ? new Color(1f, 0.85f, 0.35f) : new Color(0.80f, 0.82f, 0.86f), TextAlignmentOptions.Center);
            PlaceCenter(caption.rectTransform, new Vector2(0f, -290f), new Vector2(zone.rect.width - 60f, 80f));
        }

        /// <summary>单独重建大厦区（打出参与探索的武器后刷新当前伤害提示；幂等清理）。</summary>
        private void RefreshBuildingZone()
        {
            if (buildingZoneRoot == null) return;
            ClearChildren(buildingZoneRoot);
            BuildBuildingZone();
        }

        // —— 资源区（画面右侧：基础资源 6 堆·全剧本共享 + 剧本牌堆·按所选剧本且带数量（第一时间剧本 13 堆），全部面朝上）——

        private void BuildResourceZone()
        {
            var zone = resourceZoneRoot;
            CreateZoneFrame(zone, new Color(0.11f, 0.10f, 0.09f, 0.92f), new Color(0.55f, 0.45f, 0.28f, 0.9f));
            CreateZoneTitle(zone, "资源区", new Color(1f, 0.93f, 0.78f));

            const float pad = 14f, gap = 10f;
            const int cols = 7; // 7 列：13 堆剧本排 2 行（7+6），卡面保持较宽——信息文字不折行不截断；基础资源 6 堆占 6 列、末列留空
            const float top0 = 76f;   // 区标题之下开始
            const float labelH = 20f; // 小节标题高
            const float gapLabelRow = 4f, gapRowLabel = 6f, gapRows = 10f, bottom = 28f; // 底部 28 = 原留白 10 + 剧本牌堆卡底「共 N 张」标签下垂约 18px
            float zoneW = zone.rect.width, zoneH = zone.rect.height;
            float cellW = (zoneW - pad * 2f - gap * (cols - 1)) / cols;

            // 剧本牌堆数量随剧本而定（如第一时间剧本 14 堆 → 2 行）——行数决定整区卡高（基础资源固定 1 行）
            var scenarioPileStates = State != null ? State.scenarioPiles : null;
            int pileCount = scenarioPileStates != null ? scenarioPileStates.Count : 0;
            int rows = pileCount > 0 ? Mathf.CeilToInt(pileCount / (float)cols) : 2; // 无牌堆时维持原两行高度
            float cardH = (zoneH - top0 - bottom - 2f * labelH - 2f * gapLabelRow - gapRowLabel - (rows - 1) * gapRows) / (1f + rows);
            float cardW = Mathf.Min(cellW, cardH / 1.45f);
            cardH = cardW * 1.45f;

            // 小节一：基础资源（全剧本共享，6 堆一行；混合堆以数量最多的卡为面，卡底显示剩余张数——随开局拿取减少）
            var basicLabel = NewText("BasicLabel", zone, "基础资源", 20f, new Color(1f, 0.93f, 0.78f, 0.92f), TextAlignmentOptions.Center, FontStyles.Bold);
            PlaceFromTop(basicLabel.rectTransform, 0f, top0, new Vector2(zoneW - 40f, labelH));

            var basics = State != null ? State.basicPiles : null;
            float basicRowTop = top0 + labelH + gapLabelRow;
            for (int col = 0; col < cols && col < basics.Count; col++)
            {
                float cx = -zoneW / 2f + pad + col * (cellW + gap) + cellW / 2f;
                float cy = zoneH / 2f - basicRowTop - cardH / 2f;
                var pileRoot = CreateBasicPile(basics[col], col, zone, new Vector2(cx, cy), new Vector2(cardW, cardH));
                if (pileRoot != null) pileRoot.gameObject.AddComponent<PileClickHandler>().Initialize(this, col); // 点击弹出购买确认框
            }

            // 小节二：剧本牌堆（按所选剧本生成；剩余随购买减少）
            float scenarioLabelTop = basicRowTop + cardH + gapRowLabel;
            var scenarioLabel = NewText("ScenarioPilesLabel", zone, "剧本牌堆", 20f, new Color(1f, 0.93f, 0.78f, 0.92f), TextAlignmentOptions.Center, FontStyles.Bold);
            PlaceFromTop(scenarioLabel.rectTransform, 0f, scenarioLabelTop, new Vector2(zoneW - 40f, labelH));

            float gridTop = scenarioLabelTop + labelH + gapLabelRow;
            if (scenarioPileStates == null || scenarioPileStates.Count == 0)
            {
                var hint = NewText("EmptyHint", zone, "当前剧本未定义剧本牌堆", 20f, new Color(1f, 0.55f, 0.5f), TextAlignmentOptions.Center);
                PlaceFromTop(hint.rectTransform, 0f, gridTop + 60f, new Vector2(zoneW - 80f, 40f));
                return;
            }

            int index = 0;
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols && index < scenarioPileStates.Count; col++, index++)
                {
                    float cx = -zoneW / 2f + pad + col * (cellW + gap) + cellW / 2f;
                    float cy = zoneH / 2f - gridTop - row * (cardH + gapRows) - cardH / 2f;
                    // 剧本牌堆带数量（2026-10-18）：卡面为该堆卡牌，卡底显示「共 N 张」（剩余随购买减少）
                    var pileRoot = CreatePile(scenarioPileStates[index].definition.stacks[0].card, zone, new Vector2(cx, cy), new Vector2(cardW, cardH), $"ScenarioPile_{index}", $"共 {scenarioPileStates[index].Count} 张");
                    pileRoot.gameObject.AddComponent<PileClickHandler>().Initialize(this, State.basicPiles.Count + index); // 点击弹出购买确认框
                }
            }
        }

        /// <summary>基础资源牌堆：以数量最多的卡为面（混合堆的稀有卡保持暗藏），卡底显示剩余张数（随开局拿取减少）。</summary>
        private RectTransform CreateBasicPile(GameState.BasicPileState pile, int pileIndex, RectTransform zone, Vector2 center, Vector2 size)
        {
            var definition = pile.definition;
            if (definition == null || definition.stacks == null || definition.stacks.Length == 0 || definition.stacks[0].card == null) return null;
            return CreatePile(definition.stacks[0].card, zone, center, size, $"BasicPile_{pileIndex + 1}", $"共 {pile.Count} 张");
        }

        /// <summary>一个牌堆：底部压着同种卡牌的重复张（同样面朝上），顶部卡面展示完整信息；
        /// rootName 指定牌堆根名（缺省为 Pile_卡名），countText 非空时在卡底显示张数标签（基础资源用）。</summary>
        private RectTransform CreatePile(CardData card, RectTransform zone, Vector2 center, Vector2 size, string rootName = null, string countText = null)
        {
            var faceColor = CardDatabase.TypeColor(card.type);
            var root = NewRect(string.IsNullOrEmpty(rootName) ? $"Pile_{card.cardName}" : rootName, zone);
            PlaceCenter(root, center, size);

            // 牌堆里重复的同种卡（压在下方，稍暗以体现层次）
            var copy1 = NewImage("Copy_1", root, faceColor * 0.72f);
            PlaceCenter(copy1.rectTransform, new Vector2(5f, -5f), size);
            var copy2 = NewImage("Copy_2", root, faceColor * 0.80f);
            PlaceCenter(copy2.rectTransform, new Vector2(10f, -10f), size);

            // 面朝上的卡面（raycastTarget=true——点击牌堆弹出购买确认框）
            var border = NewImage("Face", root, faceColor);
            border.raycastTarget = true;
            PlaceCenter(border.rectTransform, Vector2.zero, size);
            var inner = NewImage("FaceInner", root, faceColor * 0.55f);
            StretchFull(inner.rectTransform, 3f);

            var typeLabel = NewText("Type", root, CardDatabase.TypeName(card.type), 16f, new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.Center);
            PlaceFromTop(typeLabel.rectTransform, 0f, 6f, new Vector2(size.x - 16f, 20f));

            var nameLabel = NewText("Name", root, card.cardName, 24f, new Color(0.98f, 0.98f, 1f), TextAlignmentOptions.Center, FontStyles.Bold);
            nameLabel.enableWordWrapping = false; // 牌名单行显示
            nameLabel.enableAutoSizing = true;    // 超宽牌名自动缩字号（如「战斗匕首刀」5 字）
            nameLabel.fontSizeMin = 14f;          // 剧本牌堆 3 行布局时卡面较窄（约 90px），下限取 14 保证 5 字名完整
            nameLabel.fontSizeMax = 24f;
            nameLabel.overflowMode = TextOverflowModes.Ellipsis; // 极端超长才截断
            PlaceFromTop(nameLabel.rectTransform, 0f, 28f, new Vector2(size.x - 12f, 34f));

            var costLabel = NewText("Cost", root, $"消耗 {card.buyCost}", 17f, new Color(1f, 0.85f, 0.35f), TextAlignmentOptions.Center, FontStyles.Bold);
            PlaceFromTop(costLabel.rectTransform, 0f, 66f, new Vector2(size.x - 16f, 20f));

            var infoLabel = NewText("Info", root, card.info, 12f, new Color(0.92f, 0.93f, 0.96f), TextAlignmentOptions.TopLeft);
            infoLabel.margin = new Vector4(2f, 2f, 2f, 0f); // 左右边距 4→2：7 列卡面文本区 ≈101px，保证 11 字/行（9px 时 ≈99px）不折行
            infoLabel.lineSpacing = 0.85f;     // 压紧行距
            infoLabel.enableAutoSizing = true; // 信息过长时自动缩字号（9-12px），尽量避免省略号截断
            infoLabel.fontSizeMin = 9f;
            infoLabel.fontSizeMax = 12f;
            infoLabel.overflowMode = TextOverflowModes.Ellipsis; // 缩到下限仍超长才截断
            PlaceFromTop(infoLabel.rectTransform, 0f, 90f, new Vector2(size.x - 14f, size.y - 112f));

            if (!string.IsNullOrEmpty(countText))
            {
                var countLabel = NewText("Count", root, countText, 12f, new Color(1f, 1f, 1f, 0.62f), TextAlignmentOptions.Center);
                countLabel.rectTransform.anchorMin = countLabel.rectTransform.anchorMax = new Vector2(0.5f, 0f);
                countLabel.rectTransform.pivot = new Vector2(0.5f, 0f);
                countLabel.rectTransform.anchoredPosition = new Vector2(0f, 3f);
                countLabel.rectTransform.sizeDelta = new Vector2(size.x - 10f, 16f);
            }
            return root;
        }

        // —— 购买确认框（点击资源区牌堆弹出：可购买→确认/取消；不可购买→原因+知道了）——

        /// <summary>点击资源区牌堆：弹出购买确认框（不可购买时显示原因，仅有关闭按钮）。</summary>
        public void OnResourcePileClicked(int pileIndex)
        {
            if (State == null || State.players.Count == 0) return;
            if (State.Exploring) // 战斗准备阶段禁止购买——先完成探索结算
            {
                Debug.Log("[StoryModeUI] 探索进行中——请先点击大厦牌堆翻开牌顶完成结算，再购买");
                return;
            }
            var pile = State.GetResourcePile(pileIndex);
            if (pile == null) return;
            var faceCard = pile.definition != null && pile.definition.stacks != null && pile.definition.stacks.Length > 0 ? pile.definition.stacks[0].card : null;
            if (faceCard == null) return;
            State.CanPurchasePile(pileIndex, out var reason);
            ShowPurchaseDialog(pileIndex, faceCard, pile.Count, reason);
        }

        /// <summary>购买确认框：全屏遮罩模态——可购买时显示「确认购买/取消」；不可购买时显示原因与「知道了」。</summary>
        private void ShowPurchaseDialog(int pileIndex, CardData faceCard, int pileRemaining, string reason)
        {
            // 幂等清理旧对话框
            var old = transform.Find("PurchaseDialog");
            if (old != null) Destroy(old.gameObject);

            var root = NewRect("PurchaseDialog", transform);
            PlaceCenter(root, Vector2.zero, new Vector2(1920f, 1080f)); // 全屏遮罩
            var backdrop = NewImage("Backdrop", root, new Color(0f, 0f, 0f, 0.55f));
            backdrop.raycastTarget = true; // 挡住下层点击
            StretchFull(backdrop.rectTransform, 0f); // 铺满全屏遮罩（2026-10-18 修复：此前未设尺寸仅 100×100，点击穿透）

            var panel = NewRect("Panel", root);
            PlaceCenter(panel, Vector2.zero, new Vector2(560f, 340f));
            CreateZoneFrame(panel, new Color(0.13f, 0.15f, 0.19f, 0.97f), new Color(0.45f, 0.52f, 0.62f, 0.95f));

            var title = NewText("Title", panel, "购买确认", 34f, new Color(1f, 0.93f, 0.78f), TextAlignmentOptions.Center, FontStyles.Bold);
            PlaceCenter(title.rectTransform, new Vector2(0f, 118f), new Vector2(400f, 46f));

            var cardLine = NewText("CardInfo", panel, $"购买「{faceCard.cardName}」（{CardDatabase.TypeName(faceCard.type)}）", 24f, new Color(0.95f, 0.96f, 1f), TextAlignmentOptions.Center);
            PlaceCenter(cardLine.rectTransform, new Vector2(0f, 62f), new Vector2(500f, 36f));

            var costLine = NewText("CostInfo", panel, $"购买消耗：{faceCard.buyCost} 黄金 · 消耗 1 购买点", 20f, new Color(1f, 0.85f, 0.35f), TextAlignmentOptions.Center);
            PlaceCenter(costLine.rectTransform, new Vector2(0f, 24f), new Vector2(500f, 30f));

            var pileLine = NewText("PileInfo", panel, $"该牌堆剩余 {pileRemaining} 张（混合堆暗抽）", 16f, new Color(0.78f, 0.80f, 0.84f), TextAlignmentOptions.Center);
            PlaceCenter(pileLine.rectTransform, new Vector2(0f, -8f), new Vector2(500f, 24f));

            if (reason == null)
            {
                CreateDialogButton(panel, "BtnConfirmPurchase", "确认购买", new Vector2(-140f, -102f), new Color(0.28f, 0.50f, 0.32f, 1f))
                    .onClick.AddListener(() => OnPurchaseConfirmed(pileIndex));
                CreateDialogButton(panel, "BtnCancelPurchase", "取消", new Vector2(140f, -102f), new Color(0.30f, 0.32f, 0.38f, 1f))
                    .onClick.AddListener(ClosePurchaseDialog);
            }
            else
            {
                var reasonText = NewText("Reason", panel, $"无法购买：{reason}", 20f, new Color(1f, 0.55f, 0.5f), TextAlignmentOptions.Center);
                PlaceCenter(reasonText.rectTransform, new Vector2(0f, -70f), new Vector2(480f, 30f));
                CreateDialogButton(panel, "BtnClosePurchase", "知道了", new Vector2(0f, -122f), new Color(0.30f, 0.32f, 0.38f, 1f))
                    .onClick.AddListener(ClosePurchaseDialog);
            }
        }

        private UnityEngine.UI.Button CreateDialogButton(RectTransform panel, string name, string label, Vector2 center, Color bg)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.SetParent(panel, false);
            PlaceCenter(rt, center, new Vector2(220f, 52f));
            var image = go.GetComponent<Image>();
            image.color = bg;
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            var text = NewText("Label", go.transform, label, 20f, new Color(0.95f, 0.96f, 1f), TextAlignmentOptions.Center);
            StretchFull(text.rectTransform, 2f);
            return button;
        }

        /// <summary>确认购买：执行购买并重建界面（牌堆剩余/弃牌堆/黄金/购买点实时可见）。</summary>
        private void OnPurchaseConfirmed(int pileIndex)
        {
            if (State == null) { ClosePurchaseDialog(); return; }
            State.PurchasePile(pileIndex);
            PlayClick();
            BuildAll();
            ClosePurchaseDialog();
        }

        /// <summary>关闭购买确认框。</summary>
        public void ClosePurchaseDialog()
        {
            var dialog = transform.Find("PurchaseDialog");
            if (dialog != null) Destroy(dialog.gameObject);
        }

        // —— 探索大厦（2026-10-18 用户定义：点击大厦牌堆→二次确认→战斗准备（只打武器）→点击牌堆翻开结算→结果弹窗；回合不因探索结束）——

        /// <summary>点击大厦牌堆：探索中=翻开牌顶结算；否则弹出探索二次确认框（不可探索时显示原因）。</summary>
        public void OnBuildingDeckClicked()
        {
            if (State == null || State.players.Count == 0) return;
            if (State.CurrentPlayerIndex != 0) // 手牌区当前显示玩家1的牌（多玩家布局待实现）——仅玩家1回合允许探索
            {
                Debug.Log("[StoryModeUI] 当前非玩家1回合，暂不能探索（多玩家布局待实现）");
                return;
            }
            if (State.Exploring)
            {
                var result = State.ResolveExplore();
                PlayClick();
                if (result != null) ShowExploreResultDialog(result);
                else BuildAll();
                return;
            }
            State.CanExplore(out var reason);
            ShowExploreConfirmDialog(reason);
        }

        /// <summary>探索二次确认框（全屏遮罩模态）：可探索时显示「探索/取消」；不可探索时显示原因与「知道了」。</summary>
        private void ShowExploreConfirmDialog(string reason)
        {
            var old = transform.Find("ExploreDialog");
            if (old != null) Destroy(old.gameObject);

            var root = NewRect("ExploreDialog", transform);
            PlaceCenter(root, Vector2.zero, new Vector2(1920f, 1080f)); // 全屏遮罩
            var backdrop = NewImage("Backdrop", root, new Color(0f, 0f, 0f, 0.55f));
            backdrop.raycastTarget = true; // 挡住下层点击
            StretchFull(backdrop.rectTransform, 0f); // 铺满全屏遮罩（2026-10-18 修复：此前未设尺寸仅 100×100，点击穿透）

            var panel = NewRect("Panel", root);
            PlaceCenter(panel, Vector2.zero, new Vector2(640f, 400f));
            CreateZoneFrame(panel, new Color(0.13f, 0.15f, 0.19f, 0.97f), new Color(0.45f, 0.52f, 0.62f, 0.95f));

            var title = NewText("Title", panel, "探索大厦", 34f, new Color(1f, 0.93f, 0.78f), TextAlignmentOptions.Center, FontStyles.Bold);
            PlaceCenter(title.rectTransform, new Vector2(0f, 142f), new Vector2(400f, 46f));

            if (reason == null)
            {
                var question = NewText("Question", panel, "是否消耗 1 个探索点探索大厦？", 24f, new Color(0.95f, 0.96f, 1f), TextAlignmentOptions.Center);
                PlaceCenter(question.rectTransform, new Vector2(0f, 80f), new Vector2(560f, 36f));

                var detail = NewText("Detail", panel, "探索开始后：打出要使用的武器（按弹药需求填装弹药，每件只使用一次），再点击大厦牌堆翻开牌顶结算战斗。", 17f, new Color(0.78f, 0.80f, 0.84f), TextAlignmentOptions.Center);
                PlaceCenter(detail.rectTransform, new Vector2(0f, 8f), new Vector2(560f, 96f));

                CreateDialogButton(panel, "BtnConfirmExplore", "探索", new Vector2(-150f, -112f), new Color(0.28f, 0.50f, 0.32f, 1f))
                    .onClick.AddListener(OnExploreConfirmed);
                CreateDialogButton(panel, "BtnCancelExplore", "取消", new Vector2(150f, -112f), new Color(0.30f, 0.32f, 0.38f, 1f))
                    .onClick.AddListener(CloseExploreDialog);
            }
            else
            {
                var reasonText = NewText("Reason", panel, $"无法探索：{reason}", 20f, new Color(1f, 0.55f, 0.5f), TextAlignmentOptions.Center);
                PlaceCenter(reasonText.rectTransform, new Vector2(0f, 40f), new Vector2(560f, 40f));
                CreateDialogButton(panel, "BtnCloseExplore", "知道了", new Vector2(0f, -112f), new Color(0.30f, 0.32f, 0.38f, 1f))
                    .onClick.AddListener(CloseExploreDialog);
            }
        }

        /// <summary>关闭探索确认框。</summary>
        public void CloseExploreDialog()
        {
            var dialog = transform.Find("ExploreDialog");
            if (dialog != null) Destroy(dialog.gameObject);
        }

        /// <summary>确认探索：消耗 1 探索点进入战斗准备阶段（期间禁止购买与结束回合、只能打出武器牌）。</summary>
        private void OnExploreConfirmed()
        {
            CloseExploreDialog();
            if (State == null) return;
            if (!State.BeginExplore(out var reason))
            {
                Debug.LogWarning($"[StoryModeUI] 开始探索失败：{reason}");
                return;
            }
            PlayClick();
            BuildAll(); // 进入战斗准备阶段：牌堆提示/区说明/回合条（探索点 ×0）刷新
        }

        /// <summary>探索结算结果弹窗（全屏遮罩模态）：翻开的牌+伤害总值+胜负明细；「确定」后刷新界面。
        /// 回合不因探索结束（2026-10-18 用户确认）——若五点全耗尽，GameState.ResolveExplore 已自动结束回合，BuildAll 呈现新回合。</summary>
        private void ShowExploreResultDialog(GameState.ExploreResult result)
        {
            var old = transform.Find("ExploreResultDialog");
            if (old != null) Destroy(old.gameObject);

            var root = NewRect("ExploreResultDialog", transform);
            PlaceCenter(root, Vector2.zero, new Vector2(1920f, 1080f));
            var backdrop = NewImage("Backdrop", root, new Color(0f, 0f, 0f, 0.55f));
            backdrop.raycastTarget = true;
            StretchFull(backdrop.rectTransform, 0f); // 铺满全屏遮罩（2026-10-18 修复：此前未设尺寸仅 100×100，点击穿透）

            var panel = NewRect("Panel", root);
            PlaceCenter(panel, Vector2.zero, new Vector2(760f, 480f));
            CreateZoneFrame(panel, new Color(0.13f, 0.15f, 0.19f, 0.97f), new Color(0.45f, 0.52f, 0.62f, 0.95f));

            var title = NewText("Title", panel, "探索结果", 34f, new Color(1f, 0.93f, 0.78f), TextAlignmentOptions.Center, FontStyles.Bold);
            PlaceCenter(title.rectTransform, new Vector2(0f, 196f), new Vector2(400f, 46f));

            // 左侧：翻开的牌；右侧：明细（翻开/伤害总值/胜负）
            CreateMiniCard(result.flippedCard, panel, new Vector2(-250f, 10f), new Vector2(130f, 188f));

            var flipLine = NewText("FlipInfo", panel, $"翻开：「{result.flippedCard.cardName}」（{CardDatabase.TypeName(result.flippedCard.type)}）", 22f, new Color(0.95f, 0.96f, 1f), TextAlignmentOptions.Left);
            PlaceCenter(flipLine.rectTransform, new Vector2(70f, 92f), new Vector2(430f, 32f));

            var damageLine = NewText("DamageInfo", panel, $"本次伤害总值：{result.totalDamage}", 22f, new Color(1f, 0.85f, 0.35f), TextAlignmentOptions.Left, FontStyles.Bold);
            PlaceCenter(damageLine.rectTransform, new Vector2(70f, 52f), new Vector2(430f, 32f));

            string outcome; Color outcomeColor;
            var infection = result.flippedCard as InfectionCard;
            if (result.isInfection && infection != null)
            {
                if (result.victory)
                {
                    outcome = $"≥ 体力 {infection.health} —— 打败感染物！\n奖章 +{result.medalsGained}，感染牌已附着于角色牌上";
                    outcomeColor = new Color(0.55f, 0.9f, 0.6f);
                }
                else
                {
                    outcome = $"< 体力 {infection.health} —— 被感染物打败\n受到 {result.damageTaken} 点伤害；感染牌放回大厦牌堆底部";
                    outcomeColor = new Color(1f, 0.55f, 0.5f);
                }
            }
            else
            {
                outcome = "（标记牌效果待定义·占位）\n已放回大厦牌堆底部";
                outcomeColor = new Color(0.85f, 0.78f, 1f);
            }
            var outcomeText = NewText("Outcome", panel, outcome, 20f, outcomeColor, TextAlignmentOptions.Left);
            PlaceCenter(outcomeText.rectTransform, new Vector2(70f, -18f), new Vector2(430f, 76f));

            CreateDialogButton(panel, "BtnCloseResult", "确定", new Vector2(0f, -170f), new Color(0.30f, 0.32f, 0.38f, 1f))
                .onClick.AddListener(OnExploreResultClosed);
        }

        /// <summary>结果弹窗「确定」：关闭并刷新界面（回合不因探索结束——2026-10-18 用户确认）。</summary>
        private void OnExploreResultClosed()
        {
            var dialog = transform.Find("ExploreResultDialog");
            if (dialog != null) Destroy(dialog.gameObject);
            PlayClick();
            BuildAll();
        }

        // —— 玩家区域条（画面底部：手牌区 | 游戏区 | 弃牌区 | 牌库区——原「库存区」与「牌库区」同堆合并，2026-10-18 确认）——

        private void BuildPlayerArea()
        {
            ClearChildren(playerAreaRoot); // 打出/摸牌后单独重建本条时幂等
            var area = playerAreaRoot;
            var frame = NewImage("AreaBorder", area, new Color(0.32f, 0.36f, 0.43f, 0.9f));
            StretchFull(frame.rectTransform, 0f);
            var bg = NewImage("AreaBg", area, new Color(0.08f, 0.09f, 0.11f, 0.92f));
            StretchFull(bg.rectTransform, 3f);

            const float pad = 16f, gap = 16f;
            // 2026-10-18 用户要求手牌/游戏区卡牌与资源区一致显示详细内容（类型/名称/消耗/奖励效果）——
            // 手牌与游戏区加宽以容纳详细卡面（94 宽卡 ×5 张不溢出），弃牌/牌库区相应收窄（各自只需一个牌堆）
            float contentW = area.rect.width - pad * 2f - gap * 3f;
            float handW = contentW * 0.31f;  // 手牌区
            float playW = contentW * 0.31f;  // 游戏区
            float stackW = contentW * 0.19f; // 弃牌/牌库区
            float zoneH = area.rect.height - 8f;
            float x0 = -area.rect.width / 2f + pad;

            // 区域条跟随当前回合玩家（2026-10-18 用户定义）：四区标题与内容随回合切换（先手标记随初始玩家）
            var display = DisplayPlayer;
            string playerPrefix = display != null ? $"玩家{State.CurrentPlayerIndex + 1}·" : "玩家1·";
            string handTitle = display != null && State.CurrentPlayerIndex == State.StartingPlayerIndex
                ? $"{playerPrefix}手牌区（先手）"
                : $"{playerPrefix}手牌区";
            CreatePlayerZone("PlayerHandZone", handTitle, x0, handW, zoneH, BuildHandZone);
            CreatePlayerZone("PlayerPlayZone", $"{playerPrefix}游戏区", x0 + handW + gap, playW, zoneH, BuildPlayZone);
            CreatePlayerZone("PlayerDiscardZone", $"{playerPrefix}弃牌区", x0 + handW + gap + playW + gap, stackW, zoneH, BuildDiscardZone);
            CreatePlayerZone("PlayerDeckZone", $"{playerPrefix}牌库区", x0 + handW + gap + playW + gap + stackW + gap, stackW, zoneH, BuildDeckZone);
        }

        private void CreatePlayerZone(string goName, string title, float x, float zoneW, float zoneH, System.Action<RectTransform> buildContent)
        {
            var zone = NewRect(goName, playerAreaRoot);
            PlaceCenter(zone, new Vector2(x + zoneW / 2f, 0f), new Vector2(zoneW, zoneH));
            CreateZoneFrame(zone, new Color(0.10f, 0.11f, 0.13f, 0.95f), new Color(0.30f, 0.34f, 0.40f, 0.9f));

            var label = NewText("ZoneTitle", zone, title, 24f, new Color(0.85f, 0.88f, 0.95f), TextAlignmentOptions.Center, FontStyles.Bold);
            PlaceFromTop(label.rectTransform, 0f, 6f, new Vector2(zoneW - 24f, 32f)); // 标题压扁为详细卡面腾出纵向空间（2026-10-18）
            buildContent(zone);
        }

        /// <summary>手牌区：渲染**当前回合玩家**的实际手牌（面朝上；区域条跟随回合切换——2026-10-18 用户定义）——
        /// 详细卡面与资源区一致（类型/名称/消耗/奖励效果，2026-10-18 用户要求）；未开局时占位 3 张。</summary>
        private void BuildHandZone(RectTransform zone)
        {
            var display = DisplayPlayer;
            if (display == null)
            {
                CreateMiniCard(CardDatabase.SearchSupplies, zone, new Vector2(-112f, -18f), new Vector2(94f, 132f));
                CreateMiniCard(CardDatabase.HandgunAmmo, zone, new Vector2(0f, -18f), new Vector2(94f, 132f));
                CreateMiniCard(CardDatabase.Pistol, zone, new Vector2(112f, -18f), new Vector2(94f, 132f));
                return;
            }

            var hand = display.hand; // 当前回合玩家的手牌（拖拽打出仅在玩家1回合允许——见 DraggableCard 守卫）
            const float cardW = 94f, cardH = 132f;
            // 间距自适应：5 张满手牌铺满全区；更多时收紧间距（轻微扇形重叠，后摸的牌在上层）
            float pitch = hand.Count > 1 ? Mathf.Min(112f, (zone.rect.width - 20f - cardW) / (hand.Count - 1)) : 112f;
            float x0 = -(hand.Count - 1) * pitch / 2f;
            for (int i = 0; i < hand.Count; i++)
            {
                var mini = CreateMiniCard(hand[i], zone, new Vector2(x0 + i * pitch, -18f), new Vector2(cardW, cardH));
                mini.gameObject.AddComponent<DraggableCard>().Initialize(this, hand[i]); // 手牌可拖拽到游戏区=打出
            }

            if (hand.Count == 0)
            {
                var emptyHint = NewText("EmptyHint", zone, "（回合结束，手牌已入弃牌区）", 14f, new Color(1f, 1f, 1f, 0.45f), TextAlignmentOptions.Center);
                PlaceCenter(emptyHint.rectTransform, new Vector2(0f, -18f), new Vector2(zone.rect.width - 30f, 24f));
            }
        }

        /// <summary>游戏区：打出（拖拽）的牌面朝上渲染于此——详细卡面与资源区一致（2026-10-18 用户要求）；
        /// 空时空卡槽占位+提示（提示覆于中间卡槽）；回合结束非附着牌弃入弃牌堆。</summary>
        private void BuildPlayZone(RectTransform zone)
        {
            var display = DisplayPlayer;
            var played = display != null ? display.playZone : null;
            if (played == null || played.Count == 0)
            {
                float[] slots = { -112f, 0f, 112f };
                foreach (float x in slots)
                {
                    var slotBorder = NewImage("Slot", zone, new Color(0.24f, 0.27f, 0.32f, 0.9f));
                    PlaceCenter(slotBorder.rectTransform, new Vector2(x, -18f), new Vector2(94f, 132f));
                    var slotInner = NewImage("SlotInner", slotBorder.rectTransform, new Color(0.13f, 0.14f, 0.17f, 0.95f));
                    StretchFull(slotInner.rectTransform, 2f);
                }
                var hint = NewText("Hint", zone, "打出的牌面朝上放于此\n（回合结束弃入弃牌堆）", 12f, new Color(1f, 1f, 1f, 0.5f), TextAlignmentOptions.Center);
                PlaceCenter(hint.rectTransform, new Vector2(0f, -18f), new Vector2(200f, 44f)); // 覆于中间空槽上
                return;
            }

            // 本回合打出的牌（面朝上，详细卡面）；牌多时收紧间距防溢出
            const float cardW = 94f, cardH = 132f;
            float pitch = played.Count > 1 ? Mathf.Min(112f, (zone.rect.width - 20f - cardW) / (played.Count - 1)) : 112f;
            float x0 = -(played.Count - 1) * pitch / 2f;
            var records = display.playRecords;
            for (int i = 0; i < played.Count; i++)
            {
                var mini = CreateMiniCard(played[i], zone, new Vector2(x0 + i * pitch, -18f), new Vector2(cardW, cardH));
                if (played[i].type == CardType.Weapon)
                {
                    // 武器卡边框粒子（2026-10-18）：弹药满足=绿色持续环绕；打出时弹药不足（弹药需求大于当前弹药仍可拖入游戏区）=红色持续环绕
                    bool insufficientAmmo = i < records.Count && records[i].ammoInsufficient;
                    CardBorderFx.Attach(mini, insufficientAmmo ? AmmoNotEnoughDarkRed : AmmoOkGreen, CardBorderFx.Mode.Loop);
                }
                if (i < records.Count)
                    mini.gameObject.AddComponent<DraggableCard>().InitializeForRetract(this, records[i]); // 游戏区卡可拖回手牌区=收回（2026-10-18） // 游戏区卡可拖回手牌区=收回（2026-10-18）
            }
        }

        /// <summary>弃牌区：面朝上的个人弃牌堆（回合结束时，本回合所出牌与手中牌全部进入此区）；未开局时占位。</summary>
        private void BuildDiscardZone(RectTransform zone)
        {
            var display = DisplayPlayer;
            bool live = display != null;
            int count = live ? display.discardPile.Count : 0;

            var stack = NewRect("DiscardStack", zone);
            var size = new Vector2(84f, 120f);
            PlaceCenter(stack, new Vector2(0f, -20f), size);

            var copy1 = NewImage("Copy_1", stack, new Color(0.20f, 0.22f, 0.25f, 1f));
            PlaceCenter(copy1.rectTransform, new Vector2(4f, -4f), size);
            var copy2 = NewImage("Copy_2", stack, new Color(0.22f, 0.24f, 0.27f, 1f));
            PlaceCenter(copy2.rectTransform, new Vector2(8f, -8f), size);
            var face = NewImage("Face", stack, new Color(0.36f, 0.40f, 0.45f, 1f));
            PlaceCenter(face.rectTransform, Vector2.zero, size);
            var inner = NewImage("FaceInner", stack, new Color(0.19f, 0.21f, 0.24f, 1f));
            StretchFull(inner.rectTransform, 4f);
            var label = NewText("StackLabel", stack, "弃牌", 22f, new Color(0.92f, 0.94f, 0.98f), TextAlignmentOptions.Center, FontStyles.Bold);
            if (live)
            {
                PlaceCenter(label.rectTransform, new Vector2(0f, 16f), new Vector2(76f, 40f));
                var countLabel = NewText("Count", stack, $"共 {count} 张", 14f, new Color(1f, 1f, 1f, 0.75f), TextAlignmentOptions.Center);
                PlaceCenter(countLabel.rectTransform, new Vector2(0f, -28f), new Vector2(80f, 22f));
            }
            else
            {
                PlaceCenter(label.rectTransform, Vector2.zero, new Vector2(76f, 40f));
            }
        }

        /// <summary>牌库区：玩家面朝下的个人牌堆（原「库存区」——同一堆，2026-10-18 确认合并）：
        /// 开局为洗切后的初始牌库；回合结束从这里摸 5 张，牌库不足时弃牌堆洗切面朝下成为新牌库；显示剩余张数；未开局时占位。</summary>
        private void BuildDeckZone(RectTransform zone)
        {
            if (DisplayPlayer == null)
            {
                // 未开局占位（棕色面朝下牌背）
                var placeholder = NewRect("DrawDeck", zone);
                var sizeP = new Vector2(84f, 120f);
                PlaceCenter(placeholder, new Vector2(0f, -20f), sizeP);
                var backP1 = NewImage("CardBack_1", placeholder, new Color(0.26f, 0.22f, 0.16f, 1f));
                PlaceCenter(backP1.rectTransform, new Vector2(6f, -6f), sizeP);
                var backP2 = NewImage("CardBack_2", placeholder, new Color(0.30f, 0.25f, 0.18f, 1f));
                PlaceCenter(backP2.rectTransform, new Vector2(3f, -3f), sizeP);
                var backP = NewImage("Back", placeholder, new Color(0.47f, 0.38f, 0.26f, 1f));
                PlaceCenter(backP.rectTransform, Vector2.zero, sizeP);
                var innerP = NewImage("BackInner", placeholder, new Color(0.22f, 0.19f, 0.14f, 1f));
                StretchFull(innerP.rectTransform, 4f);
                var labelP = NewText("StackLabel", placeholder, "牌库", 22f, new Color(0.95f, 0.92f, 0.86f), TextAlignmentOptions.Center, FontStyles.Bold);
                PlaceCenter(labelP.rectTransform, Vector2.zero, new Vector2(76f, 40f));
                return;
            }

            // 当前回合玩家面朝下的个人牌库（原库存区同堆），回合结束从堆顶摸 5 张；显示剩余张数
            int remaining = DisplayPlayer.stockPile.Count;
            var deck = NewRect("DrawDeck", zone);
            var size = new Vector2(84f, 120f);
            PlaceCenter(deck, new Vector2(0f, -20f), size);

            var back1 = NewImage("CardBack_1", deck, new Color(0.26f, 0.22f, 0.16f, 1f));
            PlaceCenter(back1.rectTransform, new Vector2(6f, -6f), size);
            var back2 = NewImage("CardBack_2", deck, new Color(0.30f, 0.25f, 0.18f, 1f));
            PlaceCenter(back2.rectTransform, new Vector2(3f, -3f), size);
            var back = NewImage("Back", deck, new Color(0.47f, 0.38f, 0.26f, 1f));
            PlaceCenter(back.rectTransform, Vector2.zero, size);
            var inner = NewImage("BackInner", deck, new Color(0.22f, 0.19f, 0.14f, 1f));
            StretchFull(inner.rectTransform, 4f);
            var label = NewText("StackLabel", deck, "牌库", 22f, new Color(0.95f, 0.92f, 0.86f), TextAlignmentOptions.Center, FontStyles.Bold);
            PlaceCenter(label.rectTransform, new Vector2(0f, 16f), new Vector2(76f, 40f));
            var countLabel = NewText("Count", deck, $"剩 {remaining} 张", 14f, new Color(1f, 1f, 1f, 0.75f), TextAlignmentOptions.Center);
            PlaceCenter(countLabel.rectTransform, new Vector2(0f, -28f), new Vector2(80f, 22f));
        }

        // —— 回合条（主区与玩家区域条之间：当前回合玩家 + 五项点数筹码 + 结束回合按钮）——

        private void BuildTurnBar()
        {
            // 幂等清理旧回合条（Destroy 延迟到帧末，刷新一律使用本次构建的引用）
            var old = transform.Find("TurnBar");
            if (old != null) Destroy(old.gameObject);

            var bar = NewRect("TurnBar", transform);
            PlaceCenter(bar, new Vector2(0f, -320f), new Vector2(1880f, 32f));
            var barBg = NewImage("BarBg", bar, new Color(0.10f, 0.11f, 0.14f, 0.85f));
            StretchFull(barBg.rectTransform, 0f);

            var turnLabel = NewText("TurnLabel", bar, string.Empty, 20f, new Color(1f, 0.93f, 0.78f), TextAlignmentOptions.Center, FontStyles.Bold);
            PlaceCenter(turnLabel.rectTransform, new Vector2(-730f, 0f), new Vector2(420f, 30f));

            for (int i = 0; i < 5; i++)
            {
                var chip = NewRect($"PointChip_{i}", bar);
                PlaceCenter(chip, new Vector2(-450f + i * 122f, 0f), new Vector2(110f, 26f));
                var chipBg = NewImage("Bg", chip, new Color(0.16f, 0.18f, 0.22f, 0.9f));
                StretchFull(chipBg.rectTransform, 0f);
                var chipLabel = NewText("Label", chip, string.Empty, 17f, new Color(0.95f, 0.96f, 1f), TextAlignmentOptions.Center);
                StretchFull(chipLabel.rectTransform, 2f);
            }

            var statusLabel = NewText("StatusLabel", bar, string.Empty, 18f, new Color(0.88f, 0.90f, 0.94f), TextAlignmentOptions.Center);
            PlaceCenter(statusLabel.rectTransform, new Vector2(440f, 0f), new Vector2(620f, 28f));

            var endGo = new GameObject("BtnEndTurn", typeof(RectTransform), typeof(Image), typeof(Button));
            var endRt = (RectTransform)endGo.transform;
            endRt.SetParent(bar, false);
            PlaceCenter(endRt, new Vector2(865f, 0f), new Vector2(120f, 32f));
            var endImage = endGo.GetComponent<Image>();
            endImage.color = new Color(0.30f, 0.32f, 0.38f, 1f);
            var endButton = endGo.GetComponent<Button>();
            endButton.targetGraphic = endImage;
            endButton.onClick.AddListener(OnEndTurnClicked);
            var endLabel = NewText("Label", endGo.transform, "结束回合", 16f, new Color(0.95f, 0.96f, 1f), TextAlignmentOptions.Center);
            StretchFull(endLabel.rectTransform, 2f);

            RefreshTurnBar(bar);
        }

        /// <summary>「结束回合」按钮：手中牌入弃牌区并轮换到下一位玩家，随后重建场地界面。</summary>
        private void OnEndTurnClicked()
        {
            if (State == null || State.players.Count == 0) return;
            if (State.Exploring) // 战斗准备阶段禁止结束回合——先翻开牌顶完成探索结算
            {
                Debug.Log("[StoryModeUI] 探索进行中——请先点击大厦牌堆翻开牌顶完成结算，再结束回合");
                return;
            }
            State.EndTurn();
            PlayClick();
            BuildAll();
        }

        /// <summary>拖拽打出（DraggableCard 调用）：打出成功后重建玩家区域条并刷新回合条（资源/点数变化实时可见）。
        /// 探索战斗准备阶段（2026-10-18 用户定义）：只能打出武器牌参与探索（弹药不足也可参战——修正②），其余牌拒绝。
        /// 武器弹药反馈（2026-10-18 用户定义+修正③）：弹药不足时仍可拖入游戏区——不播音效（静音），
        /// 打出的武器卡随重建在游戏区以红色边框粒子持续环绕（「弹药不足状态」）；弹药满足=「ammo ok」轻柔确认音（音量 0.6）+绿色粒子环绕。
        /// draggingCard 传被拖卡根（打出失败弹回时红闪特效挂于其上）。</summary>
        public bool TryPlayDraggedCard(CardData card, RectTransform draggingCard)
        {
            if (State == null || State.players.Count == 0) return false;
            bool isWeapon = card != null && card.type == CardType.Weapon;
            if (State.Exploring)
            {
                if (!State.TryPlayExploreWeapon(card, out var reason))
                {
                    Debug.Log($"[StoryModeUI] 探索中打出失败：{reason}");
                    if (isWeapon && reason != null && reason.Contains("弹药不足")) PlayAmmoFeedback(false, draggingCard);
                    return false;
                }
                if (isWeapon) PlayWeaponAmmoFeedback(card); // 修正③：弹药不足=静音；满足=ammo ok（探索参战同规则——修正②）
                PlayClick();
                BuildPlayerArea();
                RefreshTurnBar();
                RefreshBuildingZone(); // 刷新牌堆提示中的当前伤害总值
                return true;
            }
            if (!State.PlayCard(card, out var playReason))
            {
                if (isWeapon && playReason != null && playReason.Contains("弹药不足")) PlayAmmoFeedback(false, draggingCard);
                return false;
            }
            if (isWeapon) PlayWeaponAmmoFeedback(card); // 修正③：弹药不足=静音；红色粒子随 BuildPlayerArea 重建挂到游戏区该卡上
            PlayClick();
            BuildPlayerArea();
            RefreshTurnBar();
            return true;
        }

        /// <summary>旧签名保留（不带被拖卡引用）：弹药不足时仅播报错音效、无红闪目标。</summary>
        public bool TryPlayDraggedCard(CardData card) => TryPlayDraggedCard(card, null);

        /// <summary>指针屏幕坐标是否落在玩家1的游戏区（拖拽落点判定）。</summary>
        public bool IsPointerOverPlayZone(Vector2 screenPoint)
        {
            var playZoneTransform = playerAreaRoot != null ? playerAreaRoot.Find("PlayerPlayZone") : null;
            if (playZoneTransform == null) return false;
            return RectTransformUtility.RectangleContainsScreenPoint((RectTransform)playZoneTransform, screenPoint, null);
        }

        /// <summary>指针屏幕坐标是否落在玩家1的手牌区（收回拖拽落点判定，2026-10-18）。</summary>
        public bool IsPointerOverHandZone(Vector2 screenPoint)
        {
            var handZoneTransform = playerAreaRoot != null ? playerAreaRoot.Find("PlayerHandZone") : null;
            if (handZoneTransform == null) return false;
            return RectTransformUtility.RectangleContainsScreenPoint((RectTransform)handZoneTransform, screenPoint, null);
        }

        /// <summary>拖回收回（DraggableCard 调用，2026-10-18 用户定义：游戏区→手牌区）：收回并完整回退效果后
        /// 重建玩家区域条、刷新回合条；探索准备中收回武器时同步刷新大厦区当前伤害总值。失败返回 false（弹回原位）。
        /// 本回合消耗过黄金后收回被拒时，浮现文字提示「本回合已购买过资源，无法再撤销」（ToastTip，2026-10-18 用户要求）。</summary>
        public bool TryRetractDraggedCard(GameState.PlayedCardRecord record)
        {
            if (State == null || State.players.Count == 0) return false;
            if (!State.RetractPlayedCard(record, out var reason))
            {
                Debug.Log($"[StoryModeUI] 收回失败：{reason}");
                if (reason != null && reason.Contains("已购买过资源"))
                    ToastTip.Show(this, reason); // 本回合买过资源→收回被拒的文字提示（2026-10-18 用户要求）
                return false;
            }
            PlayClick();
            BuildPlayerArea();
            RefreshTurnBar();
            if (State.Exploring) RefreshBuildingZone(); // 收回探索武器后当前伤害总值实时下降
            return true;
        }

        /// <summary>按对局状态刷新回合条（点数经外部改动后可调用；内部构建后走引用重载避免 Destroy 延迟误刷旧条）。</summary>
        public void RefreshTurnBar()
        {
            var barTransform = transform.Find("TurnBar");
            if (barTransform != null) RefreshTurnBar((RectTransform)barTransform);
        }

        private void RefreshTurnBar(RectTransform bar)
        {
            if (State == null) return;
            var player = State.CurrentPlayer;

            var turnLabelTransform = bar.Find("TurnLabel");
            var turnLabel = turnLabelTransform != null ? turnLabelTransform.GetComponent<TextMeshProUGUI>() : null;
            var statusTransform = bar.Find("StatusLabel");
            var statusLabel = statusTransform != null ? statusTransform.GetComponent<TextMeshProUGUI>() : null;

            if (player == null)
            {
                if (turnLabel != null) turnLabel.text = string.Empty;
                if (statusLabel != null) statusLabel.text = string.Empty;
                return;
            }

            int playerNumber = State.CurrentPlayerIndex + 1;
            var menu = GetComponentInParent<MainMenuUI>();
            string characterName = menu != null && playerNumber <= menu.SelectedCharacters.Count && menu.SelectedCharacters[playerNumber - 1] != null
                ? menu.SelectedCharacters[playerNumber - 1].cardName
                : string.Empty;
            if (turnLabel != null)
                turnLabel.text = characterName.Length > 0 ? $"回合：玩家{playerNumber}·{characterName}" : $"回合：玩家{playerNumber}";

            // 轮换漂浮提示（2026-10-18 用户要求）：轮到任一玩家的回合时浮现「轮到x的回合」（x=该玩家角色名，缺省回退「玩家N」；暖金色区别于警告橙红）
            if (State.CurrentPlayerIndex != lastSeenTurnIndex)
            {
                lastSeenTurnIndex = State.CurrentPlayerIndex;
                ToastTip.Show(this, characterName.Length > 0 ? $"轮到{characterName}的回合" : $"轮到玩家{playerNumber}的回合",
                    new Color(1f, 0.93f, 0.66f, 1f));
            }

            for (int i = 0; i < 5; i++)
            {
                var chipTransform = bar.Find($"PointChip_{i}");
                if (chipTransform == null) continue;
                var labelTransform = chipTransform.Find("Label");
                var chipLabel = labelTransform != null ? labelTransform.GetComponent<TextMeshProUGUI>() : null;
                var bgTransform = chipTransform.Find("Bg");
                var chipBg = bgTransform != null ? bgTransform.GetComponent<Image>() : null;
                if (chipLabel == null) continue;

                var type = (TurnPoint)i;
                int p = player.turnPoints[i];
                chipLabel.text = $"{GameState.TurnPointShortName(type)} ×{p}";
                chipLabel.color = p > 0 ? new Color(0.95f, 0.96f, 1f) : new Color(1f, 1f, 1f, 0.4f);
                if (chipBg != null)
                    chipBg.color = p > 0 ? new Color(0.16f, 0.18f, 0.22f, 0.9f) : new Color(0.09f, 0.10f, 0.12f, 0.75f);
            }

            if (statusLabel != null)
                statusLabel.text = $"体力 {player.health}/{player.maxHealth} · 弹药 {player.ammo} · 黄金 {player.gold} · 奖章 {player.medals}";
        }

        private void PlayClick()
        {
            var source = GetComponentInParent<AudioSource>();
            if (source != null && source.clip != null) source.Play();
        }

        /// <summary>武器拖入成功后的弹药反馈音（2026-10-18 最终版）：弹药满足=播「ammo ok」轻柔确认音（音量 0.6）；
        /// 弹药不足=静音（修正③——不足仍可进场，不再播失败音效），红色粒子由 BuildPlayZone 依 record.ammoInsufficient 挂载。
        /// 刚打出的记录为打出记录末位（普通打出与探索参战同规则——修正②）。</summary>
        private void PlayWeaponAmmoFeedback(CardData card)
        {
            var player = State != null ? State.CurrentPlayer : null;
            var lastRecord = player != null && player.playRecords.Count > 0 ? player.playRecords[player.playRecords.Count - 1] : null;
            bool insufficient = lastRecord != null && lastRecord.card == card && lastRecord.ammoInsufficient;
            if (!insufficient) PlayAmmoFeedback(true, null);
        }

        /// <summary>武器弹药检查反馈（2026-10-18 用户定义）：ok=true 播「ammo ok」确认音（绿色边框特效随 BuildPlayerArea
        /// 重建挂到游戏区武器卡上——见 BuildPlayZone）；ok=false 播「not enough ammo」音——
        /// 打出失败弹回时并在被拖卡牌（draggingCard）上挂暗红边框单次闪烁（该失败分支现仅备用——弹药不足照常进场已不触发；空仓干火音 CDN 版暂不采用）。</summary>
        private void PlayAmmoFeedback(bool ok, RectTransform draggingCard)
        {
            if (ok)
            {
                PlaySfx(LoadSfxClip(ref ammoOkClip, "ammo_ok"));
            }
            else
            {
                PlaySfx(LoadSfxClip(ref ammoNotEnoughClip, "ammo_not_enough"));
                if (draggingCard != null) CardBorderFx.Attach(draggingCard, AmmoNotEnoughDarkRed, CardBorderFx.Mode.FlashOnce);
            }
        }

        /// <summary>运行时按名加载 Resources/Audio/SFX 下的音效（首次加载后缓存）。</summary>
        private static AudioClip LoadSfxClip(ref AudioClip cache, string name)
        {
            if (cache == null) cache = Resources.Load<AudioClip>($"Audio/SFX/{name}");
            return cache;
        }

        /// <summary>以临时 2D AudioSource 播一段一次性音效（播完自动销毁；不依赖场景内既有 AudioSource）。</summary>
        private void PlaySfx(AudioClip clip)
        {
            if (clip == null) return;
            var go = new GameObject("TempSfxSource", typeof(AudioSource));
            var source = go.GetComponent<AudioSource>();
            source.clip = clip;
            source.playOnAwake = false;
            source.spatialBlend = 0f; // UI 2D
            source.volume = 0.6f; // 2026-10-18 最终版：弹药反馈音播放音量 0.6
            source.Play();
            Destroy(go, clip.length + 0.3f);
        }

        /// <summary>迷你卡（手牌区/游戏区/探索结果弹窗等小尺寸展示，2026-10-18 用户要求与资源区卡牌一致显示详细内容）：
        /// 边框 + 底色 + 类型 + 名称 + 购买消耗 + 奖励/效果信息（名称与信息自动缩字防截断）；
        /// 角色/感染/标记牌为非购买项，不显示消耗行（信息区相应上移）。面图 raycastTarget=true 使手牌可被拖拽命中。返回卡根节点。</summary>
        private RectTransform CreateMiniCard(CardData card, RectTransform parent, Vector2 center, Vector2? size = null)
        {
            var cardSize = size ?? new Vector2(84f, 122f);
            var faceColor = CardDatabase.TypeColor(card.type);
            var root = NewRect($"Mini_{card.cardName}", parent);
            PlaceCenter(root, center, cardSize);

            var border = NewImage("Face", root, faceColor);
            border.raycastTarget = true; // 手牌拖拽需要可命中
            PlaceCenter(border.rectTransform, Vector2.zero, cardSize);
            var inner = NewImage("FaceInner", root, faceColor * 0.55f);
            StretchFull(inner.rectTransform, 2f);

            var typeLabel = NewText("Type", root, CardDatabase.TypeName(card.type), 9f, new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.Center);
            PlaceFromTop(typeLabel.rectTransform, 0f, 4f, new Vector2(cardSize.x - 8f, 12f));

            var nameLabel = NewText("Name", root, card.cardName, 12f, new Color(0.98f, 0.98f, 1f), TextAlignmentOptions.Center, FontStyles.Bold);
            nameLabel.enableWordWrapping = false; // 牌名单行显示
            nameLabel.enableAutoSizing = true;    // 长名自动缩字（如「战斗匕首刀」）
            nameLabel.fontSizeMin = 8f;
            nameLabel.fontSizeMax = 14f;
            nameLabel.overflowMode = TextOverflowModes.Ellipsis; // 极端超长才截断
            PlaceFromTop(nameLabel.rectTransform, 0f, 17f, new Vector2(cardSize.x - 6f, 16f));

            // 购买消耗（仅可购买的卡种：弹药/武器/物品/行动——角色/感染/标记为非购买项，无消耗行）
            bool purchasable = card.type == CardType.Ammunition || card.type == CardType.Weapon || card.type == CardType.Item || card.type == CardType.Action;
            float infoTop = 33f;
            if (purchasable)
            {
                var costLabel = NewText("Cost", root, $"消耗 {card.buyCost}", 9f, new Color(1f, 0.85f, 0.35f), TextAlignmentOptions.Center, FontStyles.Bold);
                PlaceFromTop(costLabel.rectTransform, 0f, 33f, new Vector2(cardSize.x - 8f, 12f));
                infoTop = 46f;
            }

            var infoLabel = NewText("Info", root, card.info, 8f, new Color(0.92f, 0.93f, 0.96f), TextAlignmentOptions.TopLeft);
            infoLabel.margin = new Vector4(2f, 0f, 2f, 0f);
            infoLabel.lineSpacing = 0.85f;     // 压紧行距
            infoLabel.enableAutoSizing = true; // 信息过长时自动缩字号（6-11px），尽量避免省略号截断
            infoLabel.fontSizeMin = 6f;
            infoLabel.fontSizeMax = 11f;
            infoLabel.overflowMode = TextOverflowModes.Ellipsis; // 缩到下限仍超长才截断
            PlaceFromTop(infoLabel.rectTransform, 0f, infoTop, new Vector2(cardSize.x - 6f, cardSize.y - infoTop - 4f));

            return root;
        }

        // —— 通用 UI 构建辅助（全部使用 TextMeshPro，禁止 legacy Text）——

        private static void ClearChildren(RectTransform root)
        {
            if (root == null) return;
            for (int i = root.childCount - 1; i >= 0; i--)
                Destroy(root.GetChild(i).gameObject);
        }

        private static void CreateZoneFrame(RectTransform zone, Color bg, Color border)
        {
            var frame = NewImage("ZoneBorder", zone, border);
            StretchFull(frame.rectTransform, 0f);
            var background = NewImage("ZoneBg", zone, bg);
            StretchFull(background.rectTransform, 3f);
        }

        private static void CreateZoneTitle(RectTransform zone, string title, Color color)
        {
            var label = NewText("ZoneTitle", zone, title, 46f, color, TextAlignmentOptions.Center, FontStyles.Bold);
            PlaceFromTop(label.rectTransform, 0f, 14f, new Vector2(zone.rect.width - 40f, 58f));
        }

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
