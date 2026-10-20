using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>回合点数类型（2026-10-18 用户定义）：每回合开始时各 1 点；做对应的事情消耗对应 1 点，为 0 时将不能再做对应的事情。
    /// 名称对应：行动点=使用行动牌；购买点（用户亦称「资源点」）=购买一个资源；探索点=抽一张大厦牌堆的卡；
    /// 物品点（使用物品点）=使用一件物品；角色点（使用角色效果点）=使用角色效果。
    /// 点数可超过 1（行动牌「城市」效果=行动点数+2，2026-10-18；叠加/上限规则待述）。</summary>
    public enum TurnPoint
    {
        Action = 0,    // 行动点——使用行动牌消耗
        Purchase = 1,  // 购买点——购买一个资源消耗
        Explore = 2,   // 探索点——抽一张大厦牌堆的卡消耗
        Item = 3,      // 物品点——使用一件物品消耗
        Character = 4, // 角色点——使用角色效果消耗
    }

    /// <summary>一名玩家的对局状态。</summary>
    public class PlayerState
    {
        /// <summary>手牌（面朝上，仅本人可见；开局为从牌库顶摸取的 5 张初始手牌，回合结束时全部弃置后重摸 5 张）。</summary>
        public readonly List<CardData> hand = new List<CardData>();

        /// <summary>牌库（原「库存区」，同一堆——2026-10-18 用户确认合并）：面朝下的个人牌堆。
        /// 开局为洗切后的初始牌库；回合结束从这里摸 5 张，不足时弃牌堆洗切面朝下成为新牌库。</summary>
        public readonly List<CardData> stockPile = new List<CardData>();

        /// <summary>游戏区：本回合打出的牌（面朝上）；回合结束时其中非附着在角色牌上的卡牌弃置入弃牌堆。</summary>
        public readonly List<CardData> playZone = new List<CardData>();

        /// <summary>游戏区各牌的打出记录（与 playZone 同下标一一对应；收回手牌时按记录完整回退——2026-10-18 用户定义）。</summary>
        public readonly List<GameState.PlayedCardRecord> playRecords = new List<GameState.PlayedCardRecord>();

        /// <summary>弃牌区：面朝上的个人弃牌堆（回合结束时游戏区非附着牌+手中牌进入此区；摸牌不足时洗切成为新牌库）。</summary>
        public readonly List<CardData> discardPile = new List<CardData>();

        /// <summary>附着在角色牌上的卡牌（回合结束时不弃置、持续在场；附着机制待设计定义，暂无入口写入）。</summary>
        public readonly List<CardData> attachedCards = new List<CardData>();

        /// <summary>本回合点数（下标对应 TurnPoint；该玩家回合开始时全部重置为 1，可经卡牌效果增加超过 1）。</summary>
        public readonly int[] turnPoints = new int[5];

        /// <summary>当前体力（开局=所选角色的初始体力值；上限为最大体力值）。</summary>
        public int health;

        /// <summary>最大体力值（开局=所选角色的最大体力值）。</summary>
        public int maxHealth;

        /// <summary>弹药（资源，用于填装武器——获得/消耗细节待设计）。</summary>
        public int ammo;

        /// <summary>黄金（货币——购买消耗的结算货币，2026-10-18 确认）。</summary>
        public int gold;

        /// <summary>奖章（角色牌等级升级消耗；获得方式待设计）。</summary>
        public int medals;

        /// <summary>本回合探索伤害总加成（行动牌「快速射击」武器伤害+N → 探索总伤害+N；回合开始清零）。</summary>
        public int turnWeaponDamageBonus;

        /// <summary>本回合战败所受感染伤害减免（行动牌「强化防御」受到的伤害-N；回合开始清零）。</summary>
        public int turnDamageReduction;

        /// <summary>本回合战败是否免疫感染伤害（行动牌「优先撤离」本回合不受感染伤害；回合开始清零）。</summary>
        public bool turnImmuneToInfectionDamage;

        /// <summary>本回合已消耗的黄金（购买等支出累计；2026-10-18 用户定义——大于 0 时本回合禁用收回、免费购买消耗 0 不计入；回合开始清零）。</summary>
        public int goldSpentThisTurn;
    }

    /// <summary>
    /// 对局状态、开局流程、回合结构与打牌：
    /// 开局（2026-10-18 用户定义）——
    /// 步骤1：设置资源区牌堆（基础资源全剧本共享 + 剧本牌堆按所选剧本，由 StoryModeUI 渲染）；
    /// 步骤2：每人从基础资源暗抽 7×弹药x10 + 手枪堆 1 张 + 刀堆 2 张（共 10 张），洗切后面朝下放入自己的牌库
    /// （开局所称「库存区」与「牌库区」为同一堆，2026-10-18 确认合并），再从牌库顶摸 5 张作为初始手牌；之后由初始玩家（玩家1）开始。
    /// 回合结构（2026-10-18 用户定义）——
    /// 回合开始时当前玩家五项点数各 1（行动/购买/探索/物品/角色）；使用行动牌/购买资源/抽大厦牌/使用物品/使用角色效果
    /// 各消耗对应 1 点，点为 0 时将不能再做对应的事情；「结束回合」按钮或五项点数全部耗尽（2026-10-18 确认，两者都要）都会结束回合——
    /// ① 游戏区内所有非附着在角色牌上的卡牌与手中牌弃置进入弃牌堆（附着牌保留；附着机制待定义）；
    /// ② 从牌库摸取 5 张卡牌；摸取途中牌库不足 5 张时，先摸取牌库全部卡牌，再将弃牌堆的牌洗切后面朝下放置成为新牌库，然后摸取剩余卡牌；
    /// ③ 循环轮换到下一位玩家，其回合开始时点数重置。
    /// 打牌（2026-10-18 用户定义）——手牌可拖拽到游戏区即打出：牌面朝上进入游戏区并执行这种牌对应的效果；
    /// 使用行动牌消耗 1 行动点、使用物品消耗 1 物品点（为 0 时不能再打）；弹药牌/武器牌暂不耗点（武器附着机制待定义）。
    /// 武器牌弹药状态（2026-10-18 用户定义）：弹药需求大于当前获得的弹药数量时仍可拖入游戏区——不扣弹药，
    /// 界面以红色粒子环绕标记（「弹药不足状态」）；弹药满足则扣减填装（绿色粒子环绕，收回时返还）。
    /// 收回（2026-10-18 用户定义）——游戏区所出牌可拖回手牌区：按打出记录完整回退（弹药/黄金/奖章/体力/行动点加成与回合战斗加成回退、
    /// 摸到的牌放回牌库顶、消耗的行动/物品点与武器填装弹药返还）；探索结算后使用过的武器锁定不可收回（武器只能使用一次）；
    /// 本回合消耗过黄金（付费购买）后整体禁用收回（提示「本回合已购买过资源，无法再撤销」——免费购买不计，2026-10-18 用户定义）；
    /// 收回弹药牌使本回合弹药变化后，复检游戏区各武器的弹药需求——当前弹药不足者转「弹药不足状态」（2026-10-18 用户定义）。
    /// 购买（2026-10-18 用户定义）——点击资源区牌堆弹出确认框：扣 1 购买点与该牌「购买消耗」等量黄金（黄金不足不能买），
    /// 从堆中暗抽一张加入弃牌堆（下回合重洗后可用）。
    /// 暗抽（2026-10-18 确认）：混合牌堆随机摸取，可能抽到稀有卡（连发手枪/生存匕首刀）；
    /// 基础资源各堆剩余张数随拿取减少；弹药x10 堆已按 4 人各拿 7 张配置为 28 张（2026-10-18 确认）。
    /// </summary>
    public class GameState
    {
        /// <summary>一个基础资源牌堆的运行时状态：按定义展开的实际卡序列（暗抽即从中随机移除一张）。</summary>
        public class BasicPileState
        {
            public CardPile definition;                                 // 牌堆定义（面卡=数量最多的卡）
            public readonly List<CardData> cards = new List<CardData>(); // 剩余的实际卡（含稀有卡）

            public int Count => cards.Count;

            public BasicPileState(CardPile definition)
            {
                this.definition = definition;
                if (definition == null || definition.stacks == null) return;
                foreach (var stack in definition.stacks)
                    for (int i = 0; stack.card != null && i < stack.count; i++)
                        cards.Add(stack.card);
            }
        }

        /// <summary>基础资源各堆的剩余状态（下标与 CardDatabase.BasicResources 一致）。</summary>
        public readonly List<BasicPileState> basicPiles = new List<BasicPileState>();

        /// <summary>剧本牌堆的剩余状态（下标 0 起，与资源区显示顺序一致；剩余随购买减少）。</summary>
        public readonly List<BasicPileState> scenarioPiles = new List<BasicPileState>();

        /// <summary>大厦牌堆运行时状态（开局洗切；[0]=牌顶、加到末尾=牌底——翻开取 [0]、放回底部 Add）。
        /// 占位构成（2026-10-18 用户同意）：35×地狱犬 + 2×未知标记，正式感染牌逐张录入后在此调整。</summary>
        public readonly List<CardData> buildingDeck = new List<CardData>();

        /// <summary>探索进行中（2026-10-18 用户定义：确认探索消耗 1 探索点后、翻开牌顶结算前，为「战斗准备」阶段——只能打出武器牌）。</summary>
        public bool Exploring { get; private set; }

        /// <summary>本次探索打出的武器（以打出记录为元素——同名副本各自的装填状态按记录区分；弹药足够=已装填扣减，
        /// 不足=未装填参战、结算时伤害 0——2026-10-18 修正②；每件只使用一次；结算后清空，武器牌本体在游戏区随回合弃置）。</summary>
        public readonly List<PlayedCardRecord> exploreWeapons = new List<PlayedCardRecord>();

        /// <summary>各玩家状态（下标 i 即玩家 i+1）。</summary>
        public readonly List<PlayerState> players = new List<PlayerState>();

        /// <summary>初始玩家下标（0 = 玩家1；开局流程结束后由其开始游戏流程）。</summary>
        public int StartingPlayerIndex => 0;

        /// <summary>当前回合玩家下标（开局为初始玩家 0；EndTurn 后循环轮换）。</summary>
        public int CurrentPlayerIndex { get; private set; }

        /// <summary>当前回合玩家；无玩家时为 null。</summary>
        public PlayerState CurrentPlayer => players.Count > 0 ? players[CurrentPlayerIndex] : null;

        /// <summary>执行开局流程：展开基础资源与剧本牌堆，为各玩家分发初始牌库与手牌（体力按所选角色初始化），并进入初始玩家的回合。
        /// playerCount 为 0 时仅设置牌堆、不发牌（防御用）。</summary>
        public void Setup(int playerCount, IReadOnlyList<CharacterCard> characters, ScenarioData scenario)
        {
            basicPiles.Clear();
            foreach (var pile in CardDatabase.BasicResources)
                basicPiles.Add(new BasicPileState(pile));

            scenarioPiles.Clear();
            if (scenario != null && scenario.resourcePiles != null)
                foreach (var stack in scenario.resourcePiles)
                    scenarioPiles.Add(new BasicPileState(new CardPile(stack)));

            // 大厦牌堆（占位构成：地狱犬×35 + 未知标记×2——2026-10-18 用户同意，正式感染牌逐张录入后替换）
            buildingDeck.Clear();
            for (int i = 0; i < BuildingDeck.InfectionCount; i++) buildingDeck.Add(CardDatabase.HellHound);
            for (int i = 0; i < BuildingDeck.MarkerCount; i++) buildingDeck.Add(CardDatabase.MarkerPlaceholder);
            Shuffle(buildingDeck);

            players.Clear();
            for (int i = 0; i < playerCount; i++)
            {
                var player = new PlayerState();
                var character = characters != null && i < characters.Count ? characters[i] : null;
                player.maxHealth = character != null ? character.maxHealth : 0;
                player.health = character != null ? character.initialHealth : 0;
                players.Add(player);
            }

            for (int i = 0; i < players.Count; i++)
            {
                var player = players[i];
                var taken = new List<CardData>();
                for (int a = 0; a < 7; a++) taken.Add(DrawFromBasicPile(0));  // 7×弹药x10
                taken.Add(DrawFromBasicPile(3));                             // 手枪堆 ×1（暗抽，可能为连发手枪）
                for (int k = 0; k < 2; k++) taken.Add(DrawFromBasicPile(4)); // 刀堆 ×2（暗抽，可能含生存匕首刀）

                Shuffle(taken);
                player.stockPile.AddRange(taken); // 洗切后面朝下放入牌库
                for (int d = 0; d < 5 && player.stockPile.Count > 0; d++)
                    player.hand.Add(DrawFromStock(player)); // 从牌库顶摸 5 张初始手牌
            }

            CurrentPlayerIndex = StartingPlayerIndex;
            StartTurn();

            var remaining = new System.Text.StringBuilder();
            for (int i = 0; i < basicPiles.Count; i++)
            {
                if (i > 0) remaining.Append("、");
                remaining.Append(basicPiles[i].Count);
            }
            Debug.Log($"[GameState] 开局完成：{players.Count} 名玩家各拿 7弹药x10+1手枪堆+2刀堆 共10张，洗切入牌库并摸 5 张初始手牌（体力按所选角色初始化）；基础资源剩余（{remaining}）；初始玩家=玩家{(StartingPlayerIndex + 1)}，进入其回合");
        }

        /// <summary>回合开始：当前玩家的五项点数各重置为 1（2026-10-18 用户定义）。</summary>
        public void StartTurn()
        {
            var player = CurrentPlayer;
            if (player == null) return;
            for (int i = 0; i < player.turnPoints.Length; i++) player.turnPoints[i] = 1;
            player.turnWeaponDamageBonus = 0;        // 回合内战斗加成清零（快速射击/强化防御/优先撤离）
            player.turnDamageReduction = 0;
            player.turnImmuneToInfectionDamage = false;
            player.goldSpentThisTurn = 0;            // 本回合黄金支出清零（消耗过黄金→禁收回，2026-10-18 用户定义）
            Debug.Log($"[GameState] 玩家{CurrentPlayerIndex + 1} 回合开始：行动/购买/探索/物品/角色点各 ×1");
        }

        /// <summary>当前玩家五项点数是否全部耗尽（全耗尽自动结束回合——2026-10-18 用户确认）。</summary>
        public bool AllPointsExhausted
        {
            get
            {
                var player = CurrentPlayer;
                if (player == null || player.turnPoints.Length == 0) return false;
                for (int i = 0; i < player.turnPoints.Length; i++)
                    if (player.turnPoints[i] > 0) return false;
                return true;
            }
        }

        /// <summary>做对应的事情：尝试消耗 1 点；点为 0 时不能再做（返回 false）。五点全部耗尽时自动结束回合。</summary>
        public bool TryConsume(TurnPoint type)
        {
            var player = CurrentPlayer;
            if (player == null) return false;
            int index = (int)type;
            if (player.turnPoints[index] <= 0)
            {
                Debug.LogWarning($"[GameState] 玩家{CurrentPlayerIndex + 1} 的{TurnPointName(type)}已为 0，不能再做对应的事情");
                return false;
            }
            player.turnPoints[index]--;
            Debug.Log($"[GameState] 玩家{CurrentPlayerIndex + 1} 消耗 1 {TurnPointName(type)}（剩余 {player.turnPoints[index]}）");
            if (AllPointsExhausted)
            {
                Debug.Log("[GameState] 五项点数全部耗尽——自动结束回合");
                EndTurn();
            }
            return true;
        }

        /// <summary>结束当前回合（2026-10-18 用户定义；「结束回合」按钮与点耗尽自动结束共用本入口）：
        /// ① 游戏区内所有非附着在角色牌上的卡牌与手中牌，弃置进入弃牌堆（附着牌存于 attachedCards 保留——附着机制待定义）；
        /// ② 从牌库摸取 5 张卡牌；摸取途中牌库不足 5 张时，先摸取牌库全部卡牌，
        ///    再将弃牌堆的牌洗切后面朝下放置成为新牌库，然后摸取剩余卡牌；
        /// ③ 循环轮换到下一位玩家，其回合开始时点数重置。</summary>
        public void EndTurn()
        {
            if (players.Count == 0) return;
            var player = CurrentPlayer;

            if (player != null)
            {
                // ① 游戏区非附着牌 + 手牌 → 弃牌堆（附着牌在 attachedCards，不参与弃置）
                int discarded = player.playZone.Count + player.hand.Count;
                player.discardPile.AddRange(player.playZone);
                player.playZone.Clear();
                player.playRecords.Clear(); // 打出记录随回合清空（收回仅限本回合内）
                player.discardPile.AddRange(player.hand);
                player.hand.Clear();

                // ② 从牌库摸 5 张；牌库不足时：先摸空牌库 → 弃牌堆洗切面朝下成为新牌库 → 摸剩余
                int drawn = 0;
                while (drawn < 5 && player.stockPile.Count > 0)
                {
                    var card = DrawFromStock(player);
                    if (card == null) break;
                    player.hand.Add(card);
                    drawn++;
                }
                int reshuffled = 0;
                if (drawn < 5 && player.discardPile.Count > 0)
                {
                    Shuffle(player.discardPile);
                    reshuffled = player.discardPile.Count;
                    player.stockPile.AddRange(player.discardPile); // 面朝下放置成为新牌库
                    player.discardPile.Clear();
                    while (drawn < 5 && player.stockPile.Count > 0)
                    {
                        var card = DrawFromStock(player);
                        if (card == null) break;
                        player.hand.Add(card);
                        drawn++;
                    }
                }

                Debug.Log($"[GameState] 玩家{CurrentPlayerIndex + 1} 回合结束：弃置 {discarded} 张（游戏区非附着+手牌；附着 {player.attachedCards.Count} 张保留）→ 摸取 {drawn} 张手牌" +
                    (reshuffled > 0 ? $"（牌库不足，弃牌堆 {reshuffled} 张已洗切面朝下成为新牌库）" : string.Empty) +
                    $"；牌库剩 {player.stockPile.Count} 张、弃牌堆 {player.discardPile.Count} 张");
            }

            // ③ 循环轮换到下一位玩家
            CurrentPlayerIndex = (CurrentPlayerIndex + 1) % players.Count;
            StartTurn();
        }

        /// <summary>统一访问资源堆（0-5=基础资源；6 起=剧本牌堆，顺序与资源区显示一致）。</summary>
        public BasicPileState GetResourcePile(int pileIndex)
        {
            if (pileIndex < 0) return null;
            if (pileIndex < basicPiles.Count) return basicPiles[pileIndex];
            int scenarioIndex = pileIndex - basicPiles.Count;
            return scenarioIndex < scenarioPiles.Count ? scenarioPiles[scenarioIndex] : null;
        }

        /// <summary>能否购买指定资源堆（2026-10-18 用户定义）：需 1 购买点、黄金不少于该牌「购买消耗」、牌堆未空；
        /// reason 返回不可购买的原因（可购买时为 null）。</summary>
        public bool CanPurchasePile(int pileIndex, out string reason)
        {
            var player = CurrentPlayer;
            var pile = GetResourcePile(pileIndex);
            if (player == null || pile == null) { reason = "当前无法购买"; return false; }
            if (pile.Count == 0) { reason = "该牌堆已空"; return false; }
            var faceCard = pile.definition != null && pile.definition.stacks != null && pile.definition.stacks.Length > 0 ? pile.definition.stacks[0].card : null;
            if (faceCard == null) { reason = "牌堆数据异常"; return false; }
            if (player.turnPoints[(int)TurnPoint.Purchase] <= 0) { reason = "购买点已耗尽，本回合不能再购买"; return false; }
            if (player.gold < faceCard.buyCost) { reason = $"黄金不足（需要 {faceCard.buyCost}，当前 {player.gold}）"; return false; }
            reason = null;
            return true;
        }

        /// <summary>购买资源堆中的一张牌（2026-10-18 用户定义）：扣 1 购买点与黄金（=该牌「购买消耗」），
        /// 从堆中暗抽一张（混合堆可能抽到稀有卡）加入弃牌堆——下回合重洗后可用。返回买到的卡（失败为 null）。</summary>
        public CardData PurchasePile(int pileIndex)
        {
            if (!CanPurchasePile(pileIndex, out var reason))
            {
                Debug.LogWarning($"[GameState] 购买失败：{reason}");
                return null;
            }
            var player = CurrentPlayer;
            var pile = GetResourcePile(pileIndex);
            var faceCard = pile.definition.stacks[0].card;

            player.turnPoints[(int)TurnPoint.Purchase]--;
            player.gold -= faceCard.buyCost;
            player.goldSpentThisTurn += faceCard.buyCost; // 本回合黄金支出累计（2026-10-18 用户定义：>0 时禁用收回；免费购买加 0 不触发）

            int index = Random.Range(0, pile.cards.Count);
            var card = pile.cards[index];
            pile.cards.RemoveAt(index);

            player.discardPile.Add(card); // 进入弃牌堆——下回合重洗后可用
            Debug.Log($"[GameState] 玩家{CurrentPlayerIndex + 1} 购买「{faceCard.cardName}」（消耗 {faceCard.buyCost} 黄金、1 购买点）→ 弃牌堆（共 {player.discardPile.Count} 张）；牌堆剩 {pile.Count} 张");

            if (AllPointsExhausted)
            {
                Debug.Log("[GameState] 五项点数全部耗尽——自动结束回合");
                EndTurn();
            }
            return card;
        }

        // —— 探索与战斗（2026-10-18 用户定义并四项确认，详见设计文档「探索与战斗」节）——

        /// <summary>一次探索的结算结果（供界面结果弹窗展示）。</summary>
        public class ExploreResult
        {
            public CardData flippedCard; // 翻开的牌
            public bool isInfection;     // 是否感染牌
            public bool isMarker;       // 是否标记牌
            public bool victory;        // 感染牌战斗是否胜利（伤害总值 ≥ 体力值）
            public int totalDamage;     // 本次伤害总值
            public int damageTaken;     // 战败所受伤害（胜利/标记为 0）
            public int medalsGained;    // 获得奖章（胜利时=感染牌奖章奖励）
        }

        /// <summary>一张已打出（游戏区）卡牌的打出记录（2026-10-18 用户定义「拖回重算」：收回手牌时按此完整回退效果）。</summary>
        public class PlayedCardRecord
        {
            public CardData card;               // 打出的牌
            public TurnPoint? consumedPoint;   // 打出时消耗的点数（行动/物品；null=未耗点）——收回时返还
            public int ammoSpent;               // 打出时填装消耗的弹药（弹药满足的武器；收回时返还）
            public bool ammoInsufficient;       // 打出时弹药不足（2026-10-18 用户定义：弹药需求大于当前弹药仍可拖入游戏区——不扣弹药；界面红色粒子环绕标记）
            public int ammoGained;              // 效果给予的弹药（收回时回退）
            public int goldGained;              // 效果给予的黄金（收回时回退）
            public int medalsGained;            // 效果给予的奖章（收回时回退）
            public int healthGained;            // 实际恢复的体力（含最大体力上限截断；收回时回退）
            public int actionPointsGained;      // 行动点数+N 加成（收回时回退）
            public int weaponDamageBonusGained; // 武器伤害+N（本回合探索总伤害加成；收回时回退）
            public int damageReductionGained;   // 受到的伤害-N（战败减免；收回时回退）
            public bool immuneGranted;         // 本回合不受感染伤害（收回时移除）
            public readonly List<CardData> drawnCards = new List<CardData>(); // 打出时摸到的牌（收回时仍在手牌中的放回牌库顶；同名卡为共享引用、互为可互换副本——被再打出的副本会以手牌中另一张同名卡顶替放回，牌数守恒）
            public bool usedInExplore;         // 是否为探索战斗准备阶段打出的武器
            public bool locked;                // 探索结算后锁定（武器已使用一次，2026-10-18 用户确认不可收回）
        }

        /// <summary>能否开始探索（不消耗点数，2026-10-18 用户定义）：需未在探索中、牌堆未空、有探索点；reason 返回不可原因（可探索时为 null）。</summary>
        public bool CanExplore(out string reason)
        {
            var player = CurrentPlayer;
            if (player == null) { reason = "当前无玩家回合"; return false; }
            if (Exploring) { reason = "探索进行中"; return false; }
            if (buildingDeck.Count == 0) { reason = "大厦牌堆已空"; return false; }
            if (player.turnPoints[(int)TurnPoint.Explore] <= 0) { reason = "探索点已耗尽，本回合不能再探索"; return false; }
            reason = null;
            return true;
        }

        /// <summary>开始探索（点击大厦牌堆确认后，2026-10-18 用户定义）：消耗 1 探索点进入战斗准备阶段（只能打出武器牌）。
        /// 与普通 TryConsume 不同——此处耗尽探索点**不触发**五点全耗尽的自动结束（探索须结算完毕；
        /// ResolveExplore 结算后照常判定自动结束），避免战斗中途被截断。返回 false 及原因（同 CanExplore）。</summary>
        public bool BeginExplore(out string reason)
        {
            if (!CanExplore(out reason)) return false;
            var player = CurrentPlayer;
            player.turnPoints[(int)TurnPoint.Explore]--;
            Exploring = true;
            exploreWeapons.Clear();
            Debug.Log($"[GameState] 玩家{CurrentPlayerIndex + 1} 开始探索大厦（消耗 1 探索点，剩余 {player.turnPoints[(int)TurnPoint.Explore]}）——战斗准备：打出要使用的武器（按弹药需求填装弹药、每件只使用一次），就绪后翻开牌顶结算");
            return true;
        }

        /// <summary>战斗准备阶段打出一件武器（2026-10-18 用户定义+修正②）：需在手牌、为武器牌；弹药不少于「弹药需求」时扣减装填，
        /// 弹药不足也可打出参战（不扣弹药、结算时该武器伤害 0——明细标注「未装填0」）；
        /// 武器面朝上进入游戏区（回合结束随非附着牌弃置）并计入本次探索（每件只使用一次——打出后即离开手牌）。
        /// 返回 false 及原因（不在探索/不在手牌/非武器）。</summary>
        public bool TryPlayExploreWeapon(CardData card, out string reason)
        {
            var player = CurrentPlayer;
            if (player == null || !Exploring) { reason = "当前不在探索战斗准备阶段"; return false; }
            int handIndex = player.hand.IndexOf(card);
            if (handIndex < 0) { reason = "该牌不在当前玩家手牌中"; return false; }
            if (card.type != CardType.Weapon) { reason = "探索中只能打出武器牌（结算后再使用其他牌）"; return false; }
            var weapon = card as WeaponCard;
            int ammoRequirement = weapon != null ? weapon.ammoRequirement : 0;

            player.hand.RemoveAt(handIndex);
            var record = new PlayedCardRecord { card = card, usedInExplore = true }; // 打出记录：结算后锁定；收回时按记录返还填装弹药
            if (player.ammo >= ammoRequirement)
            {
                record.ammoSpent = ammoRequirement; // 已装填：扣减（收回时返还）
                player.ammo -= ammoRequirement;
            }
            else
            {
                record.ammoInsufficient = true; // 未装填参战（2026-10-18 修正②）：结算时伤害 0（明细「未装填0」），收回时无弹药返还
            }
            exploreWeapons.Add(record); // 以打出记录为元素——同名副本的装填状态按记录区分
            player.playZone.Add(card);
            player.playRecords.Add(record);
            Debug.Log($"[GameState] 玩家{CurrentPlayerIndex + 1} 打出武器「{card.cardName}」参与探索（弹药需求 {ammoRequirement}，"
                + (record.ammoInsufficient ? $"弹药不足·未装填（结算时伤害 0，当前弹药 {player.ammo}）" : $"已装填，剩余弹药 {player.ammo}")
                + $"；本次探索已打出 {exploreWeapons.Count} 件武器，当前伤害总值 {CalculateExploreDamage()})");
            reason = null;
            return true;
        }

        /// <summary>计算本次探索的伤害总值（2026-10-18 用户定义）：各使用武器伤害之和 + 特殊效果在此结算 + 回合加成。
        /// 弹药不足参战的武器（修正②）结算时伤害 0——明细标注「未装填0」，其特殊效果加成亦不适用；
        /// 特殊效果（2026-10-18 用户确认的解释）——生存匕首刀：本次探索中每打出一件其他刀类武器（名称含「刀」）其伤害+5；
        /// 连发手枪：本次探索打出 ≥2 件武器时其伤害+20；快速射击（回合加成）：总伤害+2/张。</summary>
        public int CalculateExploreDamage()
        {
            return CalculateExploreDamage(out _);
        }

        private int CalculateExploreDamage(out string breakdown)
        {
            var player = CurrentPlayer;
            if (player == null) { breakdown = string.Empty; return 0; }

            int bladeCount = 0; // 刀类武器数（名称含「刀」——生存匕首刀效果参照，成员清单待设计确认；未装填的刀也计入「打出」件数）
            for (int i = 0; i < exploreWeapons.Count; i++)
                if (exploreWeapons[i].card.cardName != null && exploreWeapons[i].card.cardName.Contains("刀")) bladeCount++;

            var parts = new System.Text.StringBuilder();
            int total = 0;
            for (int i = 0; i < exploreWeapons.Count; i++)
            {
                var record = exploreWeapons[i];
                var weapon = record.card as WeaponCard;
                int damage = 0;
                int bonus = 0;
                string effect = null;
                if (record.ammoInsufficient)
                {
                    effect = "未装填0"; // 修正②：弹药不足仍可参战——结算时伤害 0，只有装填满的武器造成伤害（明细标注「未装填0」）
                }
                else
                {
                    damage = weapon != null ? weapon.damage : 0;
                    if (record.card == CardDatabase.SurvivalDagger && bladeCount > 1) // 生存匕首刀：其他刀类每件+5
                    {
                        bonus = 5 * (bladeCount - 1);
                        effect = $"刀类协同+{bonus}";
                    }
                    else if (record.card == CardDatabase.MachinePistol && exploreWeapons.Count >= 2) // 连发手枪：≥2件武器+20
                    {
                        bonus = 20;
                        effect = "多武器+20";
                    }
                }
                total += damage + bonus;
                if (parts.Length > 0) parts.Append('+');
                parts.Append(record.card.cardName).Append(damage);
                if (effect != null) parts.Append('(').Append(effect).Append(')');
            }
            if (player.turnWeaponDamageBonus != 0)
            {
                total += player.turnWeaponDamageBonus;
                if (parts.Length > 0) parts.Append('+');
                parts.Append("回合加成").Append(player.turnWeaponDamageBonus);
            }
            breakdown = parts.ToString();
            return total;
        }

        /// <summary>翻开大厦牌顶并结算探索（2026-10-18 用户定义）：
        /// 感染牌——伤害总值 ≥ 体力值=打败：感染牌附着于角色牌上（attachedCards）、奖章奖励加到角色上；
        /// 否则战败：感染牌伤害值对角色造成伤害（可经「强化防御」-N、「优先撤离」免疫），处理完毕后感染牌放回大厦牌堆底部；
        /// 标记牌（效果待定义·占位）：翻开后放回牌堆底部。
        /// 无论胜负**先不结束回合**（2026-10-18 用户确认）——回合仍按「结束回合」按钮或五点全耗尽结束（结算后照常判定自动结束）。
        /// 返回结算结果；不在探索中或牌堆已空时返回 null（防御）。</summary>
        public ExploreResult ResolveExplore()
        {
            var player = CurrentPlayer;
            if (player == null || !Exploring) return null;
            if (buildingDeck.Count == 0)
            {
                Exploring = false;
                exploreWeapons.Clear();
                Debug.LogWarning("[GameState] 大厦牌堆已空，探索无法翻开牌顶（正常流程已在开始时拦截）");
                return null;
            }

            var result = new ExploreResult();
            result.totalDamage = CalculateExploreDamage(out var breakdown);

            var flipped = buildingDeck[0]; // 翻开牌顶
            buildingDeck.RemoveAt(0);
            result.flippedCard = flipped;

            if (flipped is InfectionCard infection)
            {
                result.isInfection = true;
                if (result.totalDamage >= infection.health)
                {
                    result.victory = true;
                    result.medalsGained = infection.medalReward;
                    player.attachedCards.Add(infection);    // 感染牌附着于角色牌上
                    player.medals += infection.medalReward;  // 奖章奖励加到角色上
                    Debug.Log($"[GameState] 玩家{CurrentPlayerIndex + 1} 探索结算：伤害 [{breakdown}] = {result.totalDamage} ≥ 「{infection.cardName}」体力 {infection.health}——打败感染物！奖章+{infection.medalReward}（共 {player.medals}），感染牌附着于角色牌上；大厦牌堆剩 {buildingDeck.Count} 张");
                }
                else
                {
                    int damage = player.turnImmuneToInfectionDamage
                        ? 0
                        : Mathf.Max(0, infection.damage - player.turnDamageReduction);
                    result.damageTaken = damage;
                    player.health = Mathf.Max(0, player.health - damage);
                    buildingDeck.Add(infection); // 感染牌放回大厦牌堆底部
                    string mitigation = player.turnImmuneToInfectionDamage ? "，本回合不受感染伤害" : player.turnDamageReduction > 0 ? $"，减免 {player.turnDamageReduction}" : string.Empty;
                    Debug.Log($"[GameState] 玩家{CurrentPlayerIndex + 1} 探索结算：伤害 [{breakdown}] = {result.totalDamage} < 「{infection.cardName}」体力 {infection.health}——被感染物打败，受到 {damage} 点伤害（感染牌伤害 {infection.damage}{mitigation}；体力 {player.health}/{player.maxHealth}）；感染牌放回大厦牌堆底部（剩 {buildingDeck.Count} 张）");
                }
            }
            else
            {
                result.isMarker = flipped.type == CardType.Marker;
                buildingDeck.Add(flipped); // 标记牌占位：翻开后放回牌堆底部（效果待定义）
                Debug.Log($"[GameState] 玩家{CurrentPlayerIndex + 1} 探索结算：翻开了「{flipped.cardName}」（{CardDatabase.TypeName(flipped.type)}，效果待定义·占位）——放回大厦牌堆底部，无战斗发生；大厦牌堆剩 {buildingDeck.Count} 张");
            }

            Exploring = false;
            exploreWeapons.Clear(); // 武器牌本体留在游戏区，回合结束随非附着牌弃置

            // 本次探索使用过的武器锁定（2026-10-18 用户确认：结算后不能收回——武器只能使用一次，避免战斗后收回白拿回弹药）
            foreach (var playedRecord in player.playRecords)
                if (playedRecord.usedInExplore) playedRecord.locked = true;

            // 回合不因探索结束（2026-10-18 用户确认）——但五点全耗尽的自动结束照常判定
            if (AllPointsExhausted)
            {
                Debug.Log("[GameState] 五项点数全部耗尽——自动结束回合");
                EndTurn();
            }
            return result;
        }

        /// <summary>打出一张手牌（拖拽到游戏区，2026-10-18 用户定义）：牌面朝上进入游戏区并执行其对应效果。
        /// 点数消耗：使用行动牌=行动点、使用物品=物品点（为 0 时不能再打）；弹药牌/武器牌暂不耗点（武器附着机制待定义）。
        /// 武器牌弹药检查（2026-10-18 用户定义）：弹药需求大于当前获得的弹药数量时仍可拖入游戏区——不扣弹药、
        /// 记入打出记录 ammoInsufficient（「弹药不足状态」，界面以红色粒子环绕）；弹药满足则扣减填装并记 ammoSpent（收回时返还）。
        /// 返回 false 表示不可打出（点数不足或不在手牌中）。</summary>
        public bool PlayCard(CardData card)
        {
            return PlayCard(card, out _);
        }

        /// <summary>同 PlayCard(card)，reason 返回不可打出的原因（可打出时为 null）。</summary>
        public bool PlayCard(CardData card, out string reason)
        {
            var player = CurrentPlayer;
            if (player == null) { reason = "当前无玩家回合"; return false; }
            int handIndex = player.hand.IndexOf(card);
            if (handIndex < 0)
            {
                reason = "该牌不在当前玩家手牌中，无法打出";
                Debug.LogWarning("[GameState] 该牌不在当前玩家手牌中，无法打出");
                return false;
            }
            if (card.type == CardType.Action && player.turnPoints[(int)TurnPoint.Action] <= 0)
            {
                reason = $"玩家{CurrentPlayerIndex + 1} 行动点已为 0，不能再使用行动牌（{card.cardName}）";
                Debug.LogWarning($"[GameState] 玩家{CurrentPlayerIndex + 1} 行动点已为 0，不能再使用行动牌（{card.cardName}）");
                return false;
            }
            if (card.type == CardType.Item && player.turnPoints[(int)TurnPoint.Item] <= 0)
            {
                reason = $"玩家{CurrentPlayerIndex + 1} 物品点已为 0，不能再使用物品（{card.cardName}）";
                Debug.LogWarning($"[GameState] 玩家{CurrentPlayerIndex + 1} 物品点已为 0，不能再使用物品（{card.cardName}）");
                return false;
            }

            var weapon = card as WeaponCard; // 武器牌（弹药状态处理见打出记录段）

            player.hand.RemoveAt(handIndex);
            var record = new PlayedCardRecord { card = card }; // 打出记录（收回手牌时按此完整回退——2026-10-18）
            if (card.type == CardType.Action) record.consumedPoint = TurnPoint.Action;
            else if (card.type == CardType.Item) record.consumedPoint = TurnPoint.Item;
            if (weapon != null)
            {
                if (player.ammo >= weapon.ammoRequirement)
                {
                    record.ammoSpent = weapon.ammoRequirement; // 弹药满足状态：填装扣减（收回时返还）
                    player.ammo -= weapon.ammoRequirement;
                    Debug.Log($"[GameState] 玩家{CurrentPlayerIndex + 1} 打出武器「{card.cardName}」：弹药满足（需求 {weapon.ammoRequirement}，剩余弹药 {player.ammo}）");
                }
                else
                {
                    record.ammoInsufficient = true; // 弹药不足状态（2026-10-18 用户定义）：弹药需求大于当前弹药仍可拖入游戏区——不扣弹药，界面以红色粒子环绕标记
                    Debug.Log($"[GameState] 玩家{CurrentPlayerIndex + 1} 打出武器「{card.cardName}」：弹药不足（需求 {weapon.ammoRequirement}，当前弹药 {player.ammo}）——仍进入游戏区（未填装弹药，界面红色粒子环绕）");
                }
            }
            player.playZone.Add(card); // 面朝上进入游戏区
            player.playRecords.Add(record);

            ExecutePlayEffects(player, card, record); // 先执行效果（含可加点的「城市」），再扣点——扣点后五点全耗尽则自动结束回合
            if (card.type == CardType.Action) TryConsume(TurnPoint.Action);
            else if (card.type == CardType.Item) TryConsume(TurnPoint.Item);
            reason = null;
            return true;
        }

        /// <summary>把游戏区的一张牌收回手牌（2026-10-18 用户定义：从游戏区拖回手牌区，重算弹药与黄金）：
        /// 按打出记录完整回退——效果给予的弹药/黄金/奖章/体力/行动点数加成与回合战斗加成（武器伤害+/受到的伤害-/不受感染伤害）回退、
        /// 该牌摸到的牌放回牌库顶（按卡牌引用匹配；同名卡互为可互换副本——被再打出的副本以手牌中另一张同名卡顶替放回，牌数守恒）、
        /// 消耗的行动/物品点返还、武器填装的弹药返还；
        /// 探索结算后使用过的武器已锁定（2026-10-18 用户确认：武器只能使用一次）不可收回；
        /// 本回合消耗过黄金（付费购买）时整体禁用收回（2026-10-18 用户定义——提示「本回合已购买过资源，无法再撤销」，免费购买不计）。
        /// 收回弹药牌（含给予弹药的其他牌，ammoGained≠0）使本回合弹药变化后，复检游戏区各武器的弹药需求——
        /// 当前弹药不足者转为「弹药不足状态」（2026-10-18 用户定义：先变化弹药数再检查；已扣的填装弹药仍记于记录、收回该武器时照常返还）。
        /// 资源回退后不为负——中途已用于其他用途（填装等；购买情形已被上面的禁收回拦截）时钳制为 0。返回 false 及原因。</summary>
        public bool RetractPlayedCard(PlayedCardRecord record, out string reason)
        {
            var player = CurrentPlayer;
            if (player == null || record == null) { reason = "无效的收回目标"; return false; }
            int index = player.playRecords.IndexOf(record);
            if (index < 0) { reason = "该牌不在当前玩家游戏区（回合已结束或已被收回）"; return false; }
            if (record.locked) { reason = "该武器已在探索战斗中使用过（武器只能使用一次），不能再收回"; return false; }
            if (player.goldSpentThisTurn > 0) { reason = "本回合已购买过资源，无法再撤销"; return false; } // 2026-10-18 用户定义：本回合消耗过黄金→整体禁用收回
            if (index >= player.playZone.Count || player.playZone[index] != record.card) { reason = "游戏区数据异常"; return false; }

            // 回退效果给予的资源（钳制不为负——中途已用于其他用途时以 0 为准）
            bool clamped = false;
            if (record.ammoGained != 0)
            {
                if (player.ammo < record.ammoGained) clamped = true;
                player.ammo = Mathf.Max(0, player.ammo - record.ammoGained);
            }
            if (record.goldGained != 0)
            {
                if (player.gold < record.goldGained) clamped = true;
                player.gold = Mathf.Max(0, player.gold - record.goldGained);
            }
            if (record.medalsGained != 0)
            {
                if (player.medals < record.medalsGained) clamped = true;
                player.medals = Mathf.Max(0, player.medals - record.medalsGained);
            }
            if (record.healthGained != 0) player.health = Mathf.Max(0, player.health - record.healthGained);
            if (record.actionPointsGained != 0)
                player.turnPoints[(int)TurnPoint.Action] = Mathf.Max(0, player.turnPoints[(int)TurnPoint.Action] - record.actionPointsGained);
            if (record.weaponDamageBonusGained != 0)
                player.turnWeaponDamageBonus = Mathf.Max(0, player.turnWeaponDamageBonus - record.weaponDamageBonusGained);
            if (record.damageReductionGained != 0)
                player.turnDamageReduction = Mathf.Max(0, player.turnDamageReduction - record.damageReductionGained);
            if (record.immuneGranted) player.turnImmuneToInfectionDamage = false;

            // 该牌摸到的牌放回牌库顶（仍在手牌中的；已被再打出的跳过；逆序恢复原顺序）
            int returned = 0, skipped = 0;
            for (int i = record.drawnCards.Count - 1; i >= 0; i--)
            {
                var drawn = record.drawnCards[i];
                int handIndex = player.hand.IndexOf(drawn);
                if (handIndex < 0) { skipped++; continue; }
                player.hand.RemoveAt(handIndex);
                player.stockPile.Insert(0, drawn);
                returned++;
            }

            // 武器填装的弹药返还（探索准备阶段收回武器——重算弹药）与打出时消耗的点数返还
            if (record.ammoSpent != 0) player.ammo += record.ammoSpent;
            if (record.consumedPoint.HasValue) player.turnPoints[(int)record.consumedPoint.Value]++;

            // 从游戏区移回手牌（同步本次探索武器表——收回后伤害总值实时下降）
            if (record.usedInExplore) exploreWeapons.Remove(record); // 以打出记录移除——同名副本不混淆
            player.playZone.RemoveAt(index);
            player.playRecords.RemoveAt(index);
            player.hand.Add(record.card);

            Debug.Log($"[GameState] 玩家{CurrentPlayerIndex + 1} 收回「{record.card.cardName}」回手牌：弹药 -{record.ammoGained}、黄金 -{record.goldGained}"
                + (record.medalsGained != 0 ? $"、奖章 -{record.medalsGained}" : string.Empty)
                + (record.healthGained != 0 ? $"、体力 -{record.healthGained}" : string.Empty)
                + (record.actionPointsGained != 0 ? $"、行动点 -{record.actionPointsGained}" : string.Empty)
                + (record.weaponDamageBonusGained != 0 ? $"、探索伤害加成 -{record.weaponDamageBonusGained}" : string.Empty)
                + (record.damageReductionGained != 0 ? $"、战败减免 -{record.damageReductionGained}" : string.Empty)
                + (record.immuneGranted ? "、战败免疫移除" : string.Empty)
                + (record.ammoSpent != 0 ? $"、返还填装弹药 +{record.ammoSpent}" : string.Empty)
                + (returned > 0 ? $"、放回牌库顶 {returned} 张" : string.Empty)
                + (skipped > 0 ? $"（{skipped} 张已被再打出未放回）" : string.Empty)
                + (record.consumedPoint.HasValue ? $"、返还 {TurnPointName(record.consumedPoint.Value)}" : string.Empty)
                + (clamped ? "；部分资源已用于其他用途，回退后钳制为 0" : string.Empty));

            // 弹药牌收回后复检游戏区武器（2026-10-18 用户定义：先变化本回合弹药数，再检查各武器的弹药需求是否仍满足）
            if (record.ammoGained != 0)
                RefreshPlayZoneWeaponAmmoStates(player);

            reason = null;
            return true;
        }

        /// <summary>复检当前玩家游戏区各武器的弹药状态（2026-10-18 用户定义——收回弹药牌使本回合弹药变化后触发）：
        /// 当前弹药 < 该武器「弹药需求」者转为「弹药不足状态」（界面红色粒子环绕、探索结算伤害 0 明细「未装填0」；
        /// 其打出时已扣的填装弹药仍记在打出记录——收回该武器时照常返还）；弹药需求 0 的近战武器恒满足。
        /// 反向补装（弹药充足时「弹药不足」转回「弹药满足」的时机）待用户定义——现阶段仅由本复检随弹药减少把不足者标红。</summary>
        private void RefreshPlayZoneWeaponAmmoStates(PlayerState player)
        {
            int flipped = 0;
            for (int i = 0; i < player.playZone.Count; i++)
            {
                var weapon = player.playZone[i] as WeaponCard;
                if (weapon == null) continue;
                var state = i < player.playRecords.Count ? player.playRecords[i] : null;
                if (state == null) continue;
                bool insufficient = player.ammo < weapon.ammoRequirement;
                if (state.ammoInsufficient != insufficient)
                {
                    state.ammoInsufficient = insufficient;
                    flipped++;
                    Debug.Log($"[GameState] 玩家{CurrentPlayerIndex + 1} 游戏区武器「{weapon.cardName}」弹药状态复检（当前弹药 {player.ammo}、需求 {weapon.ammoRequirement}）→ {(insufficient ? "弹药不足状态（红粒子、探索伤害 0）" : "弹药满足状态（绿粒子）")}");
                }
            }
            if (flipped > 0)
                Debug.Log($"[GameState] 玩家{CurrentPlayerIndex + 1} 收回弹药牌后复检游戏区武器：{flipped} 件弹药状态更新（当前弹药 {player.ammo}）");
        }

        /// <summary>打出后执行该牌对应的效果与奖励（2026-10-18 用户定义）。
        /// 已实现：弹药+N / 黄金+N / 奖章+N / 体力值+N / 行动点数+N（可超 1，上限规则待述）/
        /// 摸牌（抽 N 张牌、再摸一张/两张牌——从自己牌库顶摸，不足摸尽）；
        /// 各项实际增量记入打出记录（收回手牌时按此回退）；其余效果（战斗、回合内加成、免疫、查看牌堆、返回资源区、武器附着等）在日志中标注待实现。</summary>
        private void ExecutePlayEffects(PlayerState player, CardData card, PlayedCardRecord record)
        {
            var applied = new System.Text.StringBuilder();
            var pending = new System.Text.StringBuilder();
            foreach (var rawLine in card.info.Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.Length == 0) continue;

                bool handled = false;
                handled |= TryApplyResource(line, "弹药+", v => { player.ammo += v; record.ammoGained += v; }, applied);
                handled |= TryApplyResource(line, "黄金+", v => { player.gold += v; record.goldGained += v; }, applied);
                handled |= TryApplyResource(line, "奖章+", v => { player.medals += v; record.medalsGained += v; }, applied);
                handled |= TryApplyResource(line, "体力值+", v =>
                {
                    int before = player.health;
                    player.health = Mathf.Min(player.health + v, player.maxHealth); // 上限截断——记录实际恢复量
                    record.healthGained += player.health - before;
                }, applied);
                handled |= TryApplyResource(line, "行动点数+", v => { player.turnPoints[(int)TurnPoint.Action] += v; record.actionPointsGained += v; }, applied);
                handled |= TryApplyResource(line, "武器伤害+", v => { player.turnWeaponDamageBonus += v; record.weaponDamageBonusGained += v; }, applied); // 快速射击等：本回合探索总伤害+N
                handled |= TryApplyResource(line, "受到的伤害-", v => { player.turnDamageReduction += v; record.damageReductionGained += v; }, applied);     // 强化防御等：本回合战败伤害-N
                if (line.Contains("不受感染伤害"))                                                                                                     // 优先撤离：本回合战败不受感染伤害
                {
                    player.turnImmuneToInfectionDamage = true;
                    record.immuneGranted = true;
                    applied.Append("本回合不受感染伤害 ");
                    handled = true;
                }

                int drawCount = ExtractDrawCount(line);
                if (drawCount > 0)
                {
                    int drawn = 0;
                    for (int i = 0; i < drawCount && player.stockPile.Count > 0; i++)
                    {
                        var drawnCard = DrawFromStock(player);
                        if (drawnCard == null) break;
                        player.hand.Add(drawnCard);
                        record.drawnCards.Add(drawnCard); // 收回时放回牌库顶
                        drawn++;
                    }
                    applied.Append("摸牌").Append(drawn).Append(' ');
                    handled = true;
                }

                if (!handled) pending.Append(line).Append('；');
            }

            Debug.Log($"[GameState] 玩家{CurrentPlayerIndex + 1} 打出「{card.cardName}」→ 游戏区（面朝上）"
                + (applied.Length > 0 ? $"；已执行：{applied}" : string.Empty)
                + (pending.Length > 0 ? $"；效果待实现：{pending}" : string.Empty));
        }

        /// <summary>行内是否含资源给予（如「弹药+10」），含则执行并记录。</summary>
        private static bool TryApplyResource(string line, string token, System.Action<int> apply, System.Text.StringBuilder applied)
        {
            int index = line.IndexOf(token);
            if (index < 0) return false;
            int value = ExtractNumber(line, index + token.Length);
            if (value <= 0) return false;
            apply(value);
            applied.Append(token).Append(value).Append(' ');
            return true;
        }

        /// <summary>解析行内的摸牌数量：抽 N 张牌 / 摸 N 张牌（数字）或 一张牌 / 两张牌（中文计数）。</summary>
        private static int ExtractDrawCount(string line)
        {
            int mark = line.IndexOf("张牌");
            if (mark >= 0)
            {
                int n = ExtractNumberBefore(line, mark);
                if (n > 0) return n;
            }
            if (line.Contains("两张牌")) return 2;
            if (line.Contains("一张牌")) return 1;
            return 0;
        }

        /// <summary>从 startIndex 起提取连续数字（无数字返回 -1）。</summary>
        private static int ExtractNumber(string line, int startIndex)
        {
            int value = 0;
            bool any = false;
            for (int i = startIndex; i < line.Length && char.IsDigit(line[i]); i++)
            {
                value = value * 10 + (line[i] - '0');
                any = true;
            }
            return any ? value : -1;
        }

        /// <summary>提取 endIndex 前紧邻的连续数字（跳过空白；无数字返回 -1）。</summary>
        private static int ExtractNumberBefore(string line, int endIndex)
        {
            int end = endIndex - 1;
            while (end >= 0 && char.IsWhiteSpace(line[end])) end--;
            int start = end;
            while (start >= 0 && char.IsDigit(line[start])) start--;
            if (start == end) return -1;
            return int.Parse(line.Substring(start + 1, end - start));
        }

        /// <summary>点数中文名（界面与日志用）。</summary>
        public static string TurnPointName(TurnPoint type)
        {
            switch (type)
            {
                case TurnPoint.Action: return "行动点";
                case TurnPoint.Purchase: return "购买点";
                case TurnPoint.Explore: return "探索点";
                case TurnPoint.Item: return "物品点";
                case TurnPoint.Character: return "角色点";
                default: return type.ToString();
            }
        }

        /// <summary>点数短名（回合条筹码用）。</summary>
        public static string TurnPointShortName(TurnPoint type)
        {
            switch (type)
            {
                case TurnPoint.Action: return "行动";
                case TurnPoint.Purchase: return "购买";
                case TurnPoint.Explore: return "探索";
                case TurnPoint.Item: return "物品";
                case TurnPoint.Character: return "角色";
                default: return type.ToString();
            }
        }

        /// <summary>从基础资源指定堆暗抽一张（随机；堆空时报警并返回 null）。</summary>
        public CardData DrawFromBasicPile(int pileIndex)
        {
            if (pileIndex < 0 || pileIndex >= basicPiles.Count)
            {
                Debug.LogWarning($"[GameState] 无效的基础资源堆下标：{pileIndex}");
                return null;
            }
            var pile = basicPiles[pileIndex];
            if (pile.Count == 0)
            {
                Debug.LogWarning($"[GameState] 基础资源堆 {pileIndex + 1} 已抽空，无法继续拿取");
                return null;
            }
            int index = Random.Range(0, pile.cards.Count);
            var card = pile.cards[index];
            pile.cards.RemoveAt(index);
            return card;
        }

        /// <summary>从玩家牌库顶摸一张（空时返回 null）。</summary>
        public CardData DrawFromStock(PlayerState player)
        {
            if (player.stockPile.Count == 0) return null;
            var card = player.stockPile[0];
            player.stockPile.RemoveAt(0);
            return card;
        }

        /// <summary>Fisher-Yates 洗切。</summary>
        private static void Shuffle(List<CardData> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
