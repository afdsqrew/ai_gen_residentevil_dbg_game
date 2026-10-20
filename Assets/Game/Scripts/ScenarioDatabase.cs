namespace Game
{
    /// <summary>一个剧本：决定资源区「剧本牌堆」部分放置哪些牌堆（另 6 堆基础资源全剧本共享）；简介在剧本选择界面展示。</summary>
    public class ScenarioData
    {
        public string scenarioName;       // 剧本名
        public string description;        // 剧本简介（当前为占位，正式内容待设计定义）
        public CardStack[] resourcePiles; // 剧本牌堆（带数量；数组顺序即展示顺序。第一时间剧本含正式堆 门厅×10、城市×10，其余为占位堆、数量暂按 ×10）

        public ScenarioData(string scenarioName, string description, CardStack[] resourcePiles)
        {
            this.scenarioName = scenarioName;
            this.description = description;
            this.resourcePiles = resourcePiles;
        }
    }

    /// <summary>
    /// 剧本目录：三个官方剧本（2026-10-17 用户命名）——第一时间剧本 / 古典恐惧剧本 / 间谍游戏剧本，
    /// 玩家在剧本选择界面三选一后进入场地布局。
    /// 资源区 = 基础资源 6 堆（CardDatabase.BasicResources，全剧本共享）
    /// + 剧本牌堆（此处按剧本定义；2026-10-18 起带数量——卡底显示「共 N 张」）。
    /// 第一时间剧本含用户定义的正式牌堆「门厅」×10 与「城市」×10（2026-10-18，共 14 堆）；
    /// 其余牌堆与数量当前均为占位示例（数量暂按 ×10），待设计定义后在此调整。
    /// 注：「手枪」属基础资源牌堆4，已从各剧本占位牌表移除，避免同一卡出现两个牌堆。
    /// </summary>
    public static class ScenarioDatabase
    {
        public static readonly ScenarioData[] Scenarios =
        {
            // 第一时间剧本（14 堆 = 12 种占位 + 正式堆 门厅×10、城市×10）
            new ScenarioData(
                "第一时间剧本",
                "占位简介：突发危机，分秒必争（正式内容待定义）。",
                new[]
                {
                    new CardStack(CardDatabase.HandgunAmmo, 10), new CardStack(CardDatabase.RifleAmmo, 10),
                    new CardStack(CardDatabase.SniperAmmo, 10), new CardStack(CardDatabase.MilitaryAmmoCrate, 10),
                    new CardStack(CardDatabase.HuntingRifle, 10), new CardStack(CardDatabase.Shotgun, 10),
                    new CardStack(CardDatabase.SniperRifle, 10), new CardStack(CardDatabase.FireAxe, 10),
                    new CardStack(CardDatabase.SearchSupplies, 10), new CardStack(CardDatabase.RapidFire, 10),
                    new CardStack(CardDatabase.GatherIntel, 10), new CardStack(CardDatabase.FieldMedic, 10),
                    new CardStack(CardDatabase.Lobby, 10),  // 门厅 ×10（用户 2026-10-18 定义）
                    new CardStack(CardDatabase.City, 10),   // 城市 ×10（用户 2026-10-18 定义）
                }),

            // 古典恐惧剧本（占位牌表 12 堆：近战武器与医疗行动为主；数量暂按 ×10）
            new ScenarioData(
                "古典恐惧剧本",
                "占位简介：古典式恐怖氛围（正式内容待定义）。",
                new[]
                {
                    new CardStack(CardDatabase.ShotgunAmmo, 10), new CardStack(CardDatabase.FineAmmo, 10),
                    new CardStack(CardDatabase.AnestheticDart, 10), new CardStack(CardDatabase.NailBomb, 10),
                    new CardStack(CardDatabase.Scalpel, 10), new CardStack(CardDatabase.Baton, 10),
                    new CardStack(CardDatabase.Taser, 10), new CardStack(CardDatabase.Crowbar, 10),
                    new CardStack(CardDatabase.EmergencyBandage, 10), new CardStack(CardDatabase.SeekVaccine, 10),
                    new CardStack(CardDatabase.PriorityEvacuate, 10), new CardStack(CardDatabase.FieldMedic, 10),
                }),

            // 间谍游戏剧本（占位牌表 12 堆：弹药与武器丰富、行动牌稀少；数量暂按 ×10）
            new ScenarioData(
                "间谍游戏剧本",
                "占位简介：潜伏与谍战（正式内容待定义）。",
                new[]
                {
                    new CardStack(CardDatabase.HandgunAmmo, 10), new CardStack(CardDatabase.RifleAmmo, 10),
                    new CardStack(CardDatabase.SniperAmmo, 10), new CardStack(CardDatabase.FineAmmo, 10),
                    new CardStack(CardDatabase.AnestheticDart, 10), new CardStack(CardDatabase.HuntingRifle, 10),
                    new CardStack(CardDatabase.Shotgun, 10), new CardStack(CardDatabase.SniperRifle, 10),
                    new CardStack(CardDatabase.Taser, 10), new CardStack(CardDatabase.Baton, 10),
                    new CardStack(CardDatabase.AmmoResupply, 10), new CardStack(CardDatabase.FullyArmed, 10),
                }),
        };
    }
}
