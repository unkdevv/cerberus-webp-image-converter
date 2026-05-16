namespace CerberusConverter.Services;

public static class FormatHelpers
{
    public static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = (double)bytes;
        var unit = 0;

        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{bytes} {units[unit]}" : $"{value:0.##} {units[unit]}";
    }

    public static string FormatReduction(long originalBytes, long estimatedBytes)
    {
        if (originalBytes <= 0)
        {
            return "-";
        }

        var delta = originalBytes - estimatedBytes;
        var percent = delta / (double)originalBytes * 100;
        var sign = delta >= 0 ? "menor" : "maior";
        return $"{Math.Abs(percent):0.#}% {sign}";
    }

    public static string BuildUniqueOutputPath(string sourcePath, string outputFolder, string extension)
    {
        Directory.CreateDirectory(outputFolder);

        var baseName = Path.GetFileNameWithoutExtension(sourcePath);
        var candidate = Path.Combine(outputFolder, $"{baseName}.{extension}");

        if (!Path.GetFullPath(candidate).Equals(Path.GetFullPath(sourcePath), StringComparison.OrdinalIgnoreCase)
            && !File.Exists(candidate))
        {
            return candidate;
        }

        for (var i = 1; i < 10_000; i++)
        {
            candidate = Path.Combine(outputFolder, $"{baseName}_converted_{i}.{extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException("Nao foi possivel criar um nome unico para o arquivo de saida.");
    }
}

