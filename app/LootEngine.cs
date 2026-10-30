namespace DfoHotbarCompanion;

public static class LootEngine
{
    static readonly HashSet<int> MainStoryIds = new() { 187853213,630439035,1291010263,6634853,19021260,728811286,701948302,83032363,1001,207828842,9570447,2352284,336619236,43196334 };

    public static IReadOnlyList<LootResult> Rank(GameData data, LootCategory category, string item, string enchantment,
        string material, int level, string? region, bool discoveredOnly, bool liveOnly=false, double? playerX=null, double? playerZ=null)
    {
        bool generic = item.StartsWith("Any ", StringComparison.OrdinalIgnoreCase);
        double mat = category.SupportsMaterial ? MaterialChance(category.Key, material, level) : 1;
        double magicMatch = category.Key == "MI" ? MagicMatchChance(data, item, enchantment) : 0;
        var rows = data.Dungeons.Where(d => (region == null || d.Region == region) && (!discoveredOnly || d.Discovered) && (!liveOnly || d.MapStatus == "Live now")).Select(d =>
        {
            int weight = GameDefinitions.LootTables[d.Table][category.Key];
            int effective = Math.Clamp((int)Math.Floor(weight * (category.LevelScaled ? level : 1.0)) + 1, 1, 100);
            double chance = category.Key == "MI" ? TargetChance(effective, magicMatch) : (generic ? effective / 100.0 : TargetChance(effective, 1.0 / category.Items.Length)) * mat;
            if (d.Tier == "Elite") chance = Math.Min(1, chance * (category.Key == "MI" ? 1.44 : 1.2));
            int mapX = d.Longitude / 128, mapY = 499 - (d.Latitude / 128), builtBlocks = OnlineBuiltBlockCount(d);
            double dx = playerX.HasValue ? (mapX + .5) * 32768 - playerX.Value : double.NaN;
            double dz = playerZ.HasValue ? (499 - mapY + .5) * 32768 - playerZ.Value : double.NaN;
            double distance = double.IsFinite(dx) && double.IsFinite(dz) ? Math.Sqrt(dx * dx + dz * dz) / 40 : double.PositiveInfinity;
            return new LootResult { Name=d.Name, Region=d.Region, DungeonType=d.DungeonTypeName, Size=SizeClass(builtBlocks), Tier=d.Tier, Blocks=builtBlocks, Coordinates=$"{mapX}, {mapY}", Discovery=d.MapStatus, DistanceValue=distance, Direction=Compass(dx,dz), ChanceValue=chance };
        }).ToList();
        rows = (liveOnly ? rows.OrderBy(x => x.DistanceValue).ThenByDescending(x => x.ChanceValue).ThenBy(x => x.Name) : rows.OrderByDescending(x => x.ChanceValue).ThenBy(x => x.Region).ThenBy(x => x.Name)).ToList();
        for (int i = 0; i < rows.Count; i++) rows[i].Rank = i + 1;
        return rows;
    }

    static string Compass(double dx, double dz) { if (!double.IsFinite(dx) || !double.IsFinite(dz)) return "—"; string[] p={"N","NE","E","SE","S","SW","W","NW"}; double d=(Math.Atan2(dx,dz)*180/Math.PI+360)%360; return p[(int)Math.Round(d/45)%8]; }
    static int OnlineBuiltBlockCount(DungeonLocation d) { if (d.BlockCount<=0 || MainStoryIds.Contains(d.MapId)) return d.BlockCount; double draw=SeededFirst(unchecked((uint)d.MapId)^0x5d1e5a2b)*4; return draw<1?Math.Min(d.BlockCount,5):draw<3?Math.Min(d.BlockCount,8):d.BlockCount; }
    static string SizeClass(int blocks) => blocks<=0?"Unknown":blocks<=5?"Small":blocks<=8?"Medium":"Large";
    static double SeededFirst(uint seed) { uint a=unchecked(seed+0x6D2B79F5),t=a; t=unchecked((t^(t>>15))|(0u)); t=unchecked(t*(a|1)); t^=unchecked(t+unchecked((t^(t>>7))*(t|61))); return (double)(t^(t>>14))/4294967296.0; }
    static double TargetChance(int chance,double targetPerItem) { if(targetPerItem>=1)return chance/100.0;if(targetPerItem<=0)return 0;double no=.99/(1-.01*(1-targetPerItem));var stages=new List<double>();int value=Math.Max(0,chance-1);while(value>0){stages.Add(Math.Clamp(value+1,1,100)/100.0);value=(int)Math.Floor(value*.5);}for(int i=stages.Count-1;i>=0;i--){double p=stages[i];no=(1-p)+p*(1-targetPerItem)*no;}return 1-no; }
    static double MagicMatchChance(GameData data,string item,string enchantment) { IEnumerable<MagicTemplate> t=data.MagicTemplates;if(!enchantment.StartsWith("Any ",StringComparison.OrdinalIgnoreCase))t=t.Where(x=>x.DisplayName==enchantment);double total=0;foreach(var x in t)total+=BaseItemChance(x,item);return total/data.MagicTemplates.Count; }
    static double BaseItemChance(MagicTemplate template,string item) { if(item.StartsWith("Any ",StringComparison.OrdinalIgnoreCase))return 1;if(!item.Contains("jewellery",StringComparison.OrdinalIgnoreCase))return 0;int choices=template.Group switch{0=>7,1=>5,_=>0};return choices==0?0:1.0/choices/8.0; }
    static double MaterialChance(string category,string material,int level) { if(material=="Any material")return 1;if(category=="AM"&&material=="Leather")return .69;if(category=="AM"&&material=="Chain")return .20;double plate=category=="AM"?.11:1;string[] metals={"Iron","Steel","Silver","Elven","Dwarven","Mithril","Adamantium","Ebony","Orcish","Daedric"};int wanted=Array.IndexOf(metals,material);if(wanted<0)return 0;int[] thresholds={64,128,10,21,13,8,5,3,2,5};int modifier=(level-10)*(level>=10?2:4),matches=0;for(int roll=0;roll<256;roll++){int value=Math.Clamp(modifier+roll,0,256),index=0;while(index<thresholds.Length-1&&thresholds[index]<value)value-=thresholds[index++];if(index==wanted)matches++;}return plate*matches/256.0; }
}

