namespace DfoHotbarCompanion;

public static class GameDefinitions
{
    public const string DefaultArena2 = @"G:\SteamLibrary\steamapps\common\The Elder Scrolls Daggerfall\DF\DAGGER\ARENA2";
    public static readonly string[] RegionNames = { "Alik'r Desert", "Dragontail Mountains", "Glenpoint Foothills", "Daggerfall Bluffs", "Yeorth Burrowland", "Dwynnen", "Ravennian Forest", "Devilrock", "Malekna Forest", "Isle of Balfiera", "Bantha", "Dak'fron", "Islands in the Western Iliac Bay", "Tamarilyn Point", "Lainlyn Cliffs", "Bjoulsae River", "Wrothgarian Mountains", "Daggerfall", "Glenpoint", "Betony", "Sentinel", "Anticlere", "Lainlyn", "Wayrest", "Gen Tem High Rock village", "Gen Rai Hammerfell village", "Orsinium Area", "Skeffington Wood", "Hammerfell bay coast", "Hammerfell sea coast", "High Rock bay coast", "High Rock sea coast", "Northmoor", "Menevia", "Alcaire", "Koegria", "Bhoriane", "Kambria", "Phrygias", "Urvaius", "Ykalon", "Daenia", "Shalgora", "Abibon-Gora", "Kairou", "Pothago", "Myrkwasa", "Ayasofya", "Tigonus", "Kozanset", "Satakalaam", "Totambu", "Mournoth", "Ephesus", "Santaki", "Antiphyllos", "Bergama", "Gavaudon", "Tulune", "Glenumbra Moors", "Ilessan Hills", "Cybiades" };
    public static readonly string[] DungeonTypeNames = { "Crypt", "Orc Stronghold", "Human Stronghold", "Prison", "Desecrated Temple", "Mine", "Natural Cave", "Coven", "Vampire Haunt", "Laboratory", "Harpy Nest", "Ruined Castle", "Spider Nest", "Giant Stronghold", "Dragon's Den", "Barbarian Stronghold", "Volcanic Caves", "Scorpion Nest", "Cemetery" };
    public static readonly string[] DungeonTables = { "K", "N", "N", "N", "K", "M", "M", "Q", "K", "U", "D", "N", "L", "F", "S", "N", "M", "L", "N" };
    public static readonly Dictionary<string, Dictionary<string, int>> LootTables = new()
    {
        ["D"] = T(6,6,6,6,6,6,0,0,0,0,0,0,4), ["F"] = T(2,2,5,5,5,2,50,50,1,0,0,3,0),
        ["K"] = T(3,3,3,3,3,3,5,5,3,0,5,2,100), ["L"] = T(0,0,3,3,3,3,50,50,1,75,0,5,3),
        ["M"] = T(1,1,1,1,1,2,10,10,1,15,2,3,1), ["N"] = T(5,5,5,5,5,5,5,5,1,20,5,2,5),
        ["Q"] = T(2,2,8,8,8,2,10,25,3,35,5,3,0), ["S"] = T(5,5,5,5,5,15,10,10,3,0,5,5,0),
        ["U"] = T(5,5,5,5,5,10,10,10,2,0,2,2,10)
    };
    static Dictionary<string,int> T(params int[] v) => new[] { "P1","P2","C1","C2","C3","M1","AM","WP","MI","CL","BK","M2","RL" }.Select((k,i)=>(k,v[i])).ToDictionary(x=>x.k,x=>x.Item2);
    static string[] N(params string[] v) => v.Select(x => x.Replace('_', ' ')).ToArray();
    public static readonly LootCategory[] Categories =
    {
        new() { Key="WP", Name="Weapon", SupportsMaterial=true, Items=N("Dagger","Tanto","Staff","Shortsword","Wakazashi","Broadsword","Saber","Longsword","Katana","Claymore","Dai_Katana","Mace","Flail","Warhammer","Battle_Axe","War_Axe","Short_Bow","Long_Bow","Arrow") },
        new() { Key="AM", Name="Armour", SupportsMaterial=true, Items=N("Cuirass","Gauntlets","Greaves","Left_Pauldron","Right_Pauldron","Helm","Boots","Buckler","Round_Shield","Kite_Shield","Tower_Shield") },
        new() { Key="MI", Name="Enchanted item", Items=Array.Empty<string>() },
        new() { Key="CL", Name="Clothing", Items=new[] { "Any clothing item" } },
        new() { Key="BK", Name="Book", Items=new[] { "Any book" } },
        new() { Key="RL", Name="Religious item", Items=N("Prayer_beads","Rare_symbol","Common_symbol","Bell","Holy_water","Talisman","Religious_item","Small_statue","Icon","Scarab","Holy_candle","Holy_dagger","Holy_tome") },
        new() { Key="P1", Name="Temperate plant", LevelScaled=true, Items=N("Twigs","Green_leaves","Red_flowers","Yellow_flowers","Root_tendrils","Root_bulb","Pine_branch","Green_berries","Red_berries","Yellow_berries","Clover","Red_rose","Yellow_rose","Red_poppy","Golden_poppy") },
        new() { Key="P2", Name="Warm-climate plant", LevelScaled=true, Items=N("Twigs","Green_leaves","Red_flowers","Yellow_flowers","Root_tendrils","Root_bulb","Green_berries","Red_berries","Yellow_berries","Black_rose","White_rose","Black_poppy","White_poppy","Ginkgo_leaves","Bamboo","Palm","Aloe","Fig","Cactus") },
        new() { Key="C1", Name="Creature ingredient I", LevelScaled=true, Items=N("Werewolfs_blood","Fairy_dragon_scales","Wraith_essence","Ectoplasm","Ghouls_tongue","Spider_venom","Troll_blood","Snake_venom","Gorgon_snake","Lich_dust","Giant_blood","Basilisk_eye","Daedra_heart","Saints_hair","Orcs_blood") },
        new() { Key="C2", Name="Creature ingredient II", LevelScaled=true, Items=N("Dragons_scales","Giant_scorpion_stinger","Small_scorpion_stinger","Mummy_wrappings","Gryphon_Feather") },
        new() { Key="C3", Name="Creature ingredient III", Items=N("Wereboar_tusk","Nymph_hair","Unicorn_horn") },
        new() { Key="M1", Name="Misc. ingredient I", Items=N("Holy_relic","Big_tooth","Medium_tooth","Small_tooth","Pure_water","Rain_water","Elixir_vitae","Nectar","Ichor") },
        new() { Key="M2", Name="Misc. ingredient II", Items=new[] { "Ivory", "Pearl" } }
    };
}

