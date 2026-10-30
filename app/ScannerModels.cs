namespace DfoHotbarCompanion;

public sealed record DungeonLocation(string Name, string Region, int RegionIndex, int MapId, byte DungeonType,
    string DungeonTypeName, string Table, int Longitude, int Latitude, bool Discovered, int BlockCount,
    byte ExteriorWidth, byte ExteriorHeight, string Tier, string MapStatus);

public sealed record MagicTemplate(string Name, byte Type, byte Group, byte GroupIndex,
    IReadOnlyList<MagicEffect> Effects, short Uses, int Value, byte Material)
{
    public string DisplayName => Name.Replace("%it", "item");
}

public sealed record MagicEffect(sbyte Type, sbyte Parameter);

public sealed class LootCategory
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required string[] Items { get; init; }
    public bool LevelScaled { get; init; }
    public bool SupportsMaterial { get; init; }
    public override string ToString() => Name;
}

public sealed class LootResult
{
    public int Rank { get; set; }
    public required string Name { get; init; }
    public required string Region { get; init; }
    public required string DungeonType { get; init; }
    public required string Size { get; init; }
    public required string Tier { get; init; }
    public int Blocks { get; init; }
    public required string Coordinates { get; init; }
    public required string Discovery { get; init; }
    public double DistanceValue { get; init; } = double.PositiveInfinity;
    public string Distance => double.IsFinite(DistanceValue) ? DistanceValue < 1000 ? $"{DistanceValue:N0} m" : $"{DistanceValue / 1000:N2} km" : "—";
    public string Direction { get; init; } = "—";
    public double ChanceValue { get; init; }
    public string Chance => $"{ChanceValue:P3}";
}

public sealed record GameData(IReadOnlyList<DungeonLocation> Dungeons, IReadOnlyList<MagicTemplate> MagicTemplates);

