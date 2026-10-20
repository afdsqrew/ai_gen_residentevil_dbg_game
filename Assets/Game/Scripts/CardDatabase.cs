using UnityEngine;

namespace Game
{
    /// <summary>卡牌种类（设计文档 2026-10-17：共七类）。</summary>
    public enum CardType
    {
        Character = 0,  // 角色牌
        Ammunition = 1, // 弹药牌
        Weapon = 2,     // 武器牌
        Infection = 3,  // 感染牌
        Marker = 4,     // 标记牌
        Item = 5,       // 物品牌
        Action = 6,     // 行动牌
    }

    /// <summary>
    /// 一张卡牌的数据：名称、种类、购买消耗，以及面朝上时展示的关键信息。
    /// 当前数值均为占位示例，设计细化后在此集中调整即可（StoryModeUI 会据此重建界面）。
    /// 界面排版建议：牌名 ≤5 字；info 每行 ≤12 字、总行数 ≤5（超出会被省略号截断）。
    /// </summary>
    public class CardData
    {
        public string cardName;
        public CardType type;
        public int buyCost;   // 购买消耗
        public string info;   // 面朝上时展示的信息（奖励/效果等，支持多行；建议每行 ≤12 字、总行数 ≤5，超出会被省略号截断）

        public CardData(string cardName, CardType type, int buyCost, string info)
        {
            this.cardName = cardName;
            this.type = type;
            this.buyCost = buyCost;
            this.info = info;
        }
    }

    /// <summary>角色牌：在通用卡牌数据上增加初始体力值与最大体力值（设计文档：角色牌有初始值和最大体力值）。
    /// 等级（奖章消耗与效果）待设计定义，暂以 info 文本占位展示；角色牌非购买项，buyCost 恒为 0。</summary>
    public class CharacterCard : CardData
    {
        public int initialHealth; // 初始体力值
        public int maxHealth;     // 最大体力值

        public CharacterCard(string cardName, int initialHealth, int maxHealth, string info)
            : base(cardName, CardType.Character, 0, info)
        {
            this.initialHealth = initialHealth;
            this.maxHealth = maxHealth;
        }
    }

    /// <summary>感染牌：仅出现于大厦牌堆，作为敌人——玩家打败它获得奖章奖励，被袭击玩家战败则受到其伤害。
    /// 效果（0~n）待设计定义；感染牌非购买项，buyCost 恒为 0。</summary>
    public class InfectionCard : CardData
    {
        public int health;      // 体力值
        public int damage;     // 伤害（被袭击玩家战败时承受）
        public int medalReward; // 奖章奖励（打败后获得）

        public InfectionCard(string cardName, int health, int damage, int medalReward, string info)
            : base(cardName, CardType.Infection, 0, info)
        {
            this.health = health;
            this.damage = damage;
            this.medalReward = medalReward;
        }
    }

    /// <summary>武器牌：在通用卡牌数据上增加结构化战斗数值（设计文档：武器牌有武器类型、弹药需求、武器伤害与 0~n 特殊效果）。
    /// 特殊效果暂以 info 文本行承载，其中已定义的行在探索结算中执行（见 GameState.CalculateExploreDamage）；
    /// 新增武器时结构化数值与 info 展示文本需保持一致（排版建议：info 每行 ≤12 字）。</summary>
    public class WeaponCard : CardData
    {
        public string weaponType;   // 武器类型（轻型/重型/远程/近战/特殊…）
        public int ammoRequirement; // 弹药需求（打出参与战斗时按此扣减玩家弹药）
        public int damage;          // 武器伤害

        public WeaponCard(string cardName, int buyCost, string weaponType, int ammoRequirement, int damage, string info)
            : base(cardName, CardType.Weapon, buyCost, info)
        {
            this.weaponType = weaponType;
            this.ammoRequirement = ammoRequirement;
            this.damage = damage;
        }
    }

    /// <summary>带数量的卡牌堆叠：一种卡 × 数量（如基础资源/资源区牌堆）。</summary>
    public class CardStack
    {
        public CardData card; // 卡牌
        public int count;     // 数量

        public CardStack(CardData card, int count)
        {
            this.card = card;
            this.count = count;
        }
    }

    /// <summary>一个牌堆：由一种或多种卡各按数量混堆而成（如基础资源的牌堆）。</summary>
    public class CardPile
    {
        public CardStack[] stacks; // 牌堆内的卡牌组成（一种卡 × 数量）

        public CardPile(params CardStack[] stacks)
        {
            this.stacks = stacks;
        }
    }

    /// <summary>
    /// 卡牌库：所有卡牌的统一定义（占位）。
    /// 各剧本（ScenarioDatabase）从这里挑选卡牌，组成自己资源区的牌堆；
    /// 新增/修改卡牌在此进行，改哪个剧本放哪些牌到 ScenarioDatabase 调整。
    /// </summary>
    public static class CardDatabase
    {
        // —— 角色牌（10 名，全队唯一：开局供玩家们逐一抽取；等级效果待定义，数值为占位）——
        public static readonly CharacterCard SwatCaptain = new CharacterCard("特警队长", 12, 12, "等级1：奖章×2\n等级2：奖章×4\n（等级效果待定义）");
        public static readonly CharacterCard EmergencyDoctor = new CharacterCard("急救医生", 8, 8, "等级1：奖章×2\n等级2：奖章×4\n（等级效果待定义）");
        public static readonly CharacterCard Firefighter = new CharacterCard("消防员", 11, 11, "等级1：奖章×3\n（等级效果待定义）");
        public static readonly CharacterCard Veteran = new CharacterCard("退伍军人", 10, 10, "等级1：奖章×2\n等级2：奖章×5\n（等级效果待定义）");
        public static readonly CharacterCard CombatNurse = new CharacterCard("战地护士", 9, 9, "等级1：奖章×2\n等级2：奖章×4\n（等级效果待定义）");
        public static readonly CharacterCard MechanicalEngineer = new CharacterCard("机械工程师", 8, 8, "等级1：奖章×3\n（等级效果待定义）");
        public static readonly CharacterCard Virologist = new CharacterCard("病毒学家", 7, 7, "等级1：奖章×2\n等级2：奖章×4\n（等级效果待定义）");
        public static readonly CharacterCard Hunter = new CharacterCard("猎人", 9, 9, "等级1：奖章×2\n等级2：奖章×4\n（等级效果待定义）");
        public static readonly CharacterCard Electrician = new CharacterCard("电工", 8, 8, "等级1：奖章×3\n（等级效果待定义）");
        public static readonly CharacterCard CollegeStudent = new CharacterCard("大学生", 7, 7, "等级1：奖章×2\n等级2：奖章×4\n（等级效果待定义）");

        /// <summary>角色牌池（10 名）：游戏开局供玩家们逐一抽取，全队唯一（已确认的角色不再回到牌堆）。</summary>
        public static readonly CharacterCard[] CharacterCards =
        {
            SwatCaptain, EmergencyDoctor, Firefighter, Veteran, CombatNurse,
            MechanicalEngineer, Virologist, Hunter, Electrician, CollegeStudent,
        };

        // —— 弹药牌 ——
        public static readonly CardData HandgunAmmo = new CardData("手枪弹药", CardType.Ammunition, 2, "奖励：弹药+10");
        public static readonly CardData RifleAmmo = new CardData("步枪弹药", CardType.Ammunition, 4, "奖励：弹药+20");
        public static readonly CardData ShotgunAmmo = new CardData("霰弹枪弹药", CardType.Ammunition, 3, "奖励：弹药+15");
        public static readonly CardData SniperAmmo = new CardData("狙击弹药", CardType.Ammunition, 5, "奖励：弹药+25");
        public static readonly CardData MilitaryAmmoCrate = new CardData("军用弹药箱", CardType.Ammunition, 6, "奖励：弹药+30\n黄金+5");
        public static readonly CardData FineAmmo = new CardData("精制弹药", CardType.Ammunition, 3, "奖励：弹药+10\n黄金+10");
        public static readonly CardData AnestheticDart = new CardData("麻醉镖", CardType.Ammunition, 2, "奖励：弹药+8\n黄金+3");
        public static readonly CardData NailBomb = new CardData("铁钉弹", CardType.Ammunition, 3, "奖励：弹药+12");
        public static readonly CardData AmmoPack10 = new CardData("弹药x10", CardType.Ammunition, 0, "奖励：弹药+10\n黄金+10");
        public static readonly CardData AmmoPack20 = new CardData("弹药x20", CardType.Ammunition, 30, "奖励：弹药+20\n黄金+20");
        public static readonly CardData AmmoPack30 = new CardData("弹药x30", CardType.Ammunition, 60, "奖励：弹药+30\n黄金+30");

        // —— 武器牌（WeaponCard 结构化：类型/弹药需求/伤害 + info 效果文本；2026-10-18 全部迁移）——
        public static readonly WeaponCard Pistol = new WeaponCard("手枪", 20, "轻型武器", 20, 10, "类型：轻型武器\n弹药需求：20\n伤害：10");
        public static readonly WeaponCard HuntingRifle = new WeaponCard("猎枪", 4, "轻型武器", 4, 6, "类型：轻型武器\n弹药需求：4\n伤害：6");
        public static readonly WeaponCard Shotgun = new WeaponCard("霰弹枪", 5, "重型武器", 5, 8, "类型：重型武器\n弹药需求：5\n伤害：8");
        public static readonly WeaponCard SniperRifle = new WeaponCard("狙击步枪", 6, "远程武器", 6, 12, "类型：远程武器\n弹药需求：6\n伤害：12");
        public static readonly WeaponCard AssaultRifle = new WeaponCard("突击步枪", 7, "重型武器", 8, 10, "类型：重型武器\n弹药需求：8\n伤害：10");
        public static readonly WeaponCard FireAxe = new WeaponCard("消防斧", 4, "近战武器", 0, 5, "类型：近战武器\n弹药需求：0\n伤害：5\n效果：无需弹药填装");
        public static readonly WeaponCard Scalpel = new WeaponCard("手术刀", 2, "近战武器", 0, 2, "类型：近战武器\n弹药需求：0\n伤害：2\n效果：无需弹药填装");
        public static readonly WeaponCard Baton = new WeaponCard("警棍", 3, "近战武器", 0, 3, "类型：近战武器\n弹药需求：0\n伤害：3\n效果：无需弹药填装");
        public static readonly WeaponCard Taser = new WeaponCard("电击器", 5, "特殊武器", 3, 6, "类型：特殊武器\n弹药需求：3\n伤害：6\n效果：麻痹命中的感染物");
        public static readonly WeaponCard Crowbar = new WeaponCard("撬棍", 2, "近战武器", 0, 4, "类型：近战武器\n弹药需求：0\n伤害：4\n效果：无需弹药填装");
        public static readonly WeaponCard CombatDagger = new WeaponCard("战斗匕首刀", 0, "近战武器", 0, 5, "类型：近战武器\n弹药需求：0\n伤害：5\n效果：无需弹药填装");
        public static readonly WeaponCard SurvivalDagger = new WeaponCard("生存匕首刀", 50, "近战武器", 0, 10, "类型：近战武器\n弹药需求：0\n伤害：10\n效果：本回合每有其他刀类\n武器被使用，伤害+5");
        public static readonly WeaponCard MachinePistol = new WeaponCard("连发手枪", 60, "轻型武器", 30, 20, "类型：轻型武器\n弹药需求：30\n伤害：20\n效果：袭击时若拥有不止\n一个武器，伤害+20");

        // —— 感染牌（仅出现于大厦牌堆；35 张感染牌 2026-10-18 起逐张定义）——
        public static readonly InfectionCard HellHound = new InfectionCard("地狱犬", 25, 10, 2, "体力：25\n伤害：10\n奖章：2");

        // —— 标记牌（仅出现于大厦牌堆；效果待定义——占位：翻开后放回牌堆底部，2026-10-18）——
        public static readonly CardData MarkerPlaceholder = new CardData("未知标记", CardType.Marker, 0, "效果：待定义（占位）");

        // —— 物品牌 ——
        public static readonly CardData GreenGrass = new CardData("绿草", CardType.Item, 20, "效果：本牌返回资源区，\n恢复20体力；或本牌与\n另一张绿草一起返回，\n恢复60体力");

        // —— 行动牌 ——
        public static readonly CardData SearchSupplies = new CardData("搜索物资", CardType.Action, 2, "效果：抽 1 张牌\n奖励：弹药+5");
        public static readonly CardData RapidFire = new CardData("快速射击", CardType.Action, 1, "效果：本回合武器伤害+2");
        public static readonly CardData HardenedDefense = new CardData("强化防御", CardType.Action, 2, "效果：本回合受到的伤害-1");
        public static readonly CardData FieldMedic = new CardData("战地急救", CardType.Action, 3, "效果：体力值+3");
        public static readonly CardData GatherIntel = new CardData("收集情报", CardType.Action, 1, "效果：查看大厦牌堆顶 2 张\n奖励：黄金+5");
        public static readonly CardData FullyArmed = new CardData("全副武装", CardType.Action, 4, "效果：本回合购买消耗-1\n奖励：弹药+10");
        public static readonly CardData EmergencyBandage = new CardData("紧急包扎", CardType.Action, 2, "效果：体力值+2\n奖励：黄金+3");
        public static readonly CardData SeekVaccine = new CardData("寻找疫苗", CardType.Action, 3, "效果：免疫本回合感染效果\n奖励：奖章+1");
        public static readonly CardData PriorityEvacuate = new CardData("优先撤离", CardType.Action, 1, "效果：本回合不受感染伤害");
        public static readonly CardData AmmoResupply = new CardData("弹药补给", CardType.Action, 2, "奖励：弹药+20");
        public static readonly CardData Lobby = new CardData("门厅", CardType.Action, 30, "效果：从自己的牌库区\n再摸两张牌加入手牌");
        public static readonly CardData City = new CardData("城市", CardType.Action, 70, "效果：从自己的牌库区\n再摸一张牌加入手牌\n行动点数+2");

        // —— 基础资源（2026-10-18 用户定义：6 个牌堆；同日追加绿草 ×6）——
        /// <summary>基础资源：共 6 个牌堆、86 张卡（手枪与连发手枪、战斗匕首刀与生存匕首刀各混为一个牌堆；弹药x10 已按 4 人开局各拿 7 张配置为 28 张——2026-10-18 确认）。抽取/购买/补货规则待设计说明。</summary>
        public static readonly CardPile[] BasicResources =
        {
            new CardPile(new CardStack(AmmoPack10, 28)),                                     // 牌堆1：弹药x10 ×28（4 人开局各拿 7 张）
            new CardPile(new CardStack(AmmoPack20, 15)),                                     // 牌堆2：弹药x20 ×15
            new CardPile(new CardStack(AmmoPack30, 15)),                                     // 牌堆3：弹药x30 ×15
            new CardPile(new CardStack(Pistol, 9), new CardStack(MachinePistol, 1)),         // 牌堆4：手枪×9 + 连发手枪×1
            new CardPile(new CardStack(CombatDagger, 11), new CardStack(SurvivalDagger, 1)), // 牌堆5：战斗匕首刀×11 + 生存匕首刀×1
            new CardPile(new CardStack(GreenGrass, 6)),                                      // 牌堆6：绿草 ×6
        };

        /// <summary>种类中文名。</summary>
        public static string TypeName(CardType type)
        {
            switch (type)
            {
                case CardType.Character: return "角色牌";
                case CardType.Ammunition: return "弹药牌";
                case CardType.Weapon: return "武器牌";
                case CardType.Infection: return "感染牌";
                case CardType.Marker: return "标记牌";
                case CardType.Item: return "物品牌";
                case CardType.Action: return "行动牌";
                default: return type.ToString();
            }
        }

        /// <summary>种类主题色（面朝上卡面的边框色）。</summary>
        public static Color TypeColor(CardType type)
        {
            switch (type)
            {
                case CardType.Character: return new Color(0.62f, 0.52f, 0.25f);  // 金
                case CardType.Ammunition: return new Color(0.55f, 0.42f, 0.18f); // 铜
                case CardType.Weapon: return new Color(0.30f, 0.38f, 0.52f);     // 钢蓝
                case CardType.Infection: return new Color(0.55f, 0.20f, 0.20f);  // 红
                case CardType.Marker: return new Color(0.45f, 0.30f, 0.58f);     // 紫
                case CardType.Item: return new Color(0.45f, 0.34f, 0.24f);      // 棕
                case CardType.Action: return new Color(0.28f, 0.50f, 0.32f);     // 绿
                default: return Color.gray;
            }
        }
    }

    /// <summary>
    /// 大厦牌堆的洗切成分（设计 2026-10-17 口述）：35 张感染牌 + 2 张标记牌，共 37 张。
    /// 感染牌/标记牌仅出现于大厦牌堆；这 35+2 张的具体卡面内容与抽取/重洗流程待设计定义。
    /// 成分暂按全局常量处理，是否随剧本变化待确认（若随剧本变化再移入 ScenarioData）。
    /// </summary>
    public static class BuildingDeck
    {
        public const int InfectionCount = 35; // 感染牌张数
        public const int MarkerCount = 2;      // 标记牌张数
        public const int TotalCount = InfectionCount + MarkerCount;
    }
}
