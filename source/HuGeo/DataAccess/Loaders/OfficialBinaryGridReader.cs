using HuGeo.Core.Math;

namespace HuGeo.DataAccess.Loaders;

internal static class OfficialBinaryGridReader
{
    private const int Version = 1;
    private const int Hd72Magic = 0x31474448; // HDG1
    private const int GeoidMagic = 0x31474447; // GDG1

    public static GeodeticOffsetGrid ReadHd72(Stream stream)
    {
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        ValidateHeader(reader, Hd72Magic);

        var rows = reader.ReadInt32();
        var cols = reader.ReadInt32();
        var lon0 = reader.ReadDouble();
        var lat0 = reader.ReadDouble();
        var stepLon = reader.ReadDouble();
        var stepLat = reader.ReadDouble();
        var count = checked(rows * cols);

        var latOffsets = ReadSingleArrayAsDouble(reader, count);
        var lonOffsets = ReadSingleArrayAsDouble(reader, count);

        return new GeodeticOffsetGrid(
            rows,
            cols,
            stepLon,
            stepLat,
            lon0,
            lat0,
            latOffsets,
            lonOffsets);
    }

    public static GeoidHeightGrid ReadGeoid(Stream stream)
    {
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        ValidateHeader(reader, GeoidMagic);

        var rows = reader.ReadInt32();
        var cols = reader.ReadInt32();
        var lon0 = reader.ReadDouble();
        var lat0 = reader.ReadDouble();
        var lonStep = reader.ReadDouble();
        var latStep = reader.ReadDouble();
        var count = checked(rows * cols);
        var values = ReadSingleArrayAsDouble(reader, count);

        return new GeoidHeightGrid(
            rows,
            cols,
            lonStep,
            latStep,
            lon0,
            lat0,
            values);
    }

    private static void ValidateHeader(BinaryReader reader, int expectedMagic)
    {
        var magic = reader.ReadInt32();
        if (magic != expectedMagic)
            throw new InvalidOperationException($"Invalid binary grid magic: 0x{magic:X8}");

        var version = reader.ReadInt32();
        if (version != Version)
            throw new InvalidOperationException($"Unsupported binary grid version: {version}");
    }

    private static double[] ReadSingleArrayAsDouble(BinaryReader reader, int count)
    {
        var values = new double[count];
        for (var i = 0; i < values.Length; i++)
            values[i] = reader.ReadSingle();

        return values;
    }
}
