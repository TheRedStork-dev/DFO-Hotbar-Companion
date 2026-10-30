using System.IO;
using System.Text.Json;

namespace DfoHotbarCompanion;

public sealed class CompanionSettings
{
    public bool HotbarEnabled { get; set; } = true;
    public bool ScannerEnabled { get; set; } = true;
    public string Arena2Path { get; set; } = GameDefinitions.DefaultArena2;
    static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DFO Hotbar Companion", "settings.json");
    public static CompanionSettings Load() { try { return JsonSerializer.Deserialize<CompanionSettings>(File.ReadAllText(FilePath)) ?? new(); } catch { return new(); } }
    public void Save() { Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!); File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented=true })); }
}

