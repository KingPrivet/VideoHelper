// Copyright (c) 2026 KingPrivet. All rights reserved.
namespace VideoHelper;

public sealed class VideoPart
{
    public string Path { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Folder { get; set; } = string.Empty;
    public long Size { get; set; }
    public int Number { get; set; }
    public string SizeText => FormatSize(Size);

    private static string FormatSize(long bytes)
    {
        string[] units = ["Б", "КБ", "МБ", "ГБ"];
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.#} {units[unit]}";
    }
}
