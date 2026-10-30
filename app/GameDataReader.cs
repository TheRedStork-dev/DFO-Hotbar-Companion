using System.IO;
using System.Text;

namespace DfoHotbarCompanion;

public static class GameDataReader
{
    sealed record BsaRecord(long Position, int Size, uint Id);
    sealed record DungeonLayout(int Blocks, byte ExteriorWidth, byte ExteriorHeight);

    public static GameData Load(string arena2Path)
    {
        string maps = Path.Combine(arena2Path, "MAPS.BSA"), magic = Path.Combine(arena2Path, "MAGIC.DEF");
        if (!File.Exists(maps) || !File.Exists(magic))
            throw new FileNotFoundException("Choose the ARENA2 folder containing MAPS.BSA and MAGIC.DEF.");
        return new(ReadDungeons(maps), ReadMagicTemplates(magic));
    }

    static IReadOnlyList<DungeonLocation> ReadDungeons(string path)
    {
        using var stream = File.OpenRead(path);
        using var br = new BinaryReader(stream, Encoding.ASCII, true);
        short count = br.ReadInt16(); ushort type = br.ReadUInt16();
        if (count % 4 != 0 || type is not (0x0100 or 0x0200)) throw new InvalidDataException("MAPS.BSA has an unexpected directory format.");
        var directory = new List<(uint id, int size)>();
        if (type == 0x0100)
        {
            stream.Position = stream.Length - count * 18L;
            for (int i = 0; i < count; i++) { _ = br.ReadBytes(14); directory.Add((0, br.ReadInt32())); }
        }
        else
        {
            stream.Position = stream.Length - count * 8L;
            for (int i = 0; i < count; i++) directory.Add((br.ReadUInt32(), br.ReadInt32()));
        }
        long position = 4;
        var records = directory.Select(x => { var r = new BsaRecord(position, x.size, x.id); position += x.size; return r; }).ToArray();
        var output = new List<DungeonLocation>();
        for (int region = 0; region < count / 4; region++)
        {
            var nr = records[region * 4 + 3]; stream.Position = nr.Position; uint nc = br.ReadUInt32();
            var names = new List<string>((int)nc);
            for (int i = 0; i < nc; i++) { var raw = br.ReadBytes(32); int z = Array.IndexOf(raw, (byte)0); names.Add(Encoding.ASCII.GetString(raw, 0, z < 0 ? raw.Length : z)); }
            var layouts = ReadDungeonLayouts(stream, br, records[region * 4], records[region * 4 + 1], names.Count);
            var mr = records[region * 4 + 2]; stream.Position = mr.Position; int entries = Math.Min(names.Count, mr.Size / 17);
            for (int i = 0; i < entries; i++)
            {
                int mapId = br.ReadInt32(); uint bits = br.ReadUInt32(); int rawLat = br.ReadInt32(); byte dt = br.ReadByte(); _ = br.ReadUInt32();
                int lon = (int)((bits & 0x1ffffff) >> 8), lat = (rawLat & 0xffffff) >> 8, lt = (int)((4L * bits) >> 27);
                if (lt is not (4 or 7 or 10 or 12 or 13) || dt >= GameDefinitions.DungeonTypeNames.Length) continue;
                bool discovered = ((bits >> 24) & 0x40) != 0;
                string rn = region < GameDefinitions.RegionNames.Length ? GameDefinitions.RegionNames[region] : $"Region {region}";
                var layout = layouts[i];
                output.Add(new(names[i], rn, region, mapId, dt, GameDefinitions.DungeonTypeNames[dt], GameDefinitions.DungeonTables[dt], lon, lat, discovered, layout.Blocks, layout.ExteriorWidth, layout.ExteriorHeight, "Regular", discovered ? "Known" : "Hidden"));
            }
        }
        return output;
    }

    static DungeonLayout[] ReadDungeonLayouts(Stream stream, BinaryReader br, BsaRecord mapP, BsaRecord mapD, int locationCount)
    {
        var locationIds = new ushort[locationCount]; var widths = new byte[locationCount]; var heights = new byte[locationCount];
        for (int i = 0; i < locationCount; i++)
        {
            stream.Position = mapP.Position + i * 4L; uint offset = br.ReadUInt32(); long start = mapP.Position + locationCount * 4L + offset;
            stream.Position = start; uint doors = br.ReadUInt32(); long afterRecord = start + 4 + doors * 6 + 112;
            stream.Position = start + 4 + doors * 6 + 33; locationIds[i] = br.ReadUInt16();
            stream.Position = afterRecord; ushort buildings = br.ReadUInt16();
            stream.Position = afterRecord + 7 + buildings * 26L + 32 + 8; widths[i] = br.ReadByte(); heights[i] = br.ReadByte();
        }
        stream.Position = mapD.Position; uint dungeonCount = br.ReadUInt32(); var byLocationId = new Dictionary<ushort, int>();
        long recordsStart = mapD.Position + 4 + dungeonCount * 8L;
        for (int i = 0; i < dungeonCount; i++)
        {
            stream.Position = mapD.Position + 4 + i * 8L; uint offset = br.ReadUInt32(); stream.Position += 2; ushort exteriorLocationId = br.ReadUInt16();
            long start = recordsStart + offset; stream.Position = start; uint doors = br.ReadUInt32(); long dungeonHeader = start + 4 + doors * 6 + 112;
            stream.Position = dungeonHeader + 10; byLocationId[exteriorLocationId] = br.ReadUInt16();
        }
        return Enumerable.Range(0, locationCount).Select(i => new DungeonLayout(byLocationId.GetValueOrDefault(locationIds[i]), widths[i], heights[i])).ToArray();
    }

    static IReadOnlyList<MagicTemplate> ReadMagicTemplates(string path)
    {
        using var br = new BinaryReader(File.OpenRead(path), Encoding.ASCII); int count = br.ReadInt32(); var result = new List<MagicTemplate>();
        for (int i = 0; i < count; i++)
        {
            var raw = br.ReadBytes(32); int z = Array.IndexOf(raw, (byte)0); string name = Encoding.ASCII.GetString(raw, 0, z < 0 ? raw.Length : z);
            byte type = br.ReadByte(), group = br.ReadByte(), groupIndex = br.ReadByte(); var effects = new List<MagicEffect>();
            for (int e = 0; e < 10; e++) effects.Add(new(br.ReadSByte(), br.ReadSByte()));
            short uses = br.ReadInt16(); int value = br.ReadInt32(); byte material = br.ReadByte();
            if (type == 0) result.Add(new(name, type, group, groupIndex, effects, uses, value, material));
        }
        return result;
    }
}

