using CerberusConverter.Models;
using SkiaSharp;

namespace CerberusConverter.Services;

public sealed class ImageConversionService
{
    public static readonly string[] OutputFormats = ["webp", "avif", "jpg", "png", "ico"];

    public async Task<ImageMetadata> ReadMetadataAsync(string path, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var stream = File.OpenRead(path);
            using var codec = SKCodec.Create(stream);
            if (codec is null)
            {
                throw new InvalidDataException("Formato de imagem nao suportado.");
            }

            var fileInfo = new FileInfo(path);
            return new ImageMetadata(
                FormatName(codec.EncodedFormat),
                codec.Info.Width,
                codec.Info.Height,
                fileInfo.Length);
        }, cancellationToken);
    }

    public async Task<long> EstimateOutputBytesAsync(
        string sourcePath,
        string outputFormat,
        int quality,
        CancellationToken cancellationToken = default)
    {
        var bytes = await EncodeAsync(sourcePath, outputFormat, quality, cancellationToken);
        return bytes.Length;
    }

    public async Task<long> ConvertAsync(
        string sourcePath,
        string outputPath,
        string outputFormat,
        int quality,
        CancellationToken cancellationToken = default)
    {
        var bytes = await EncodeAsync(sourcePath, outputFormat, quality, cancellationToken);
        await File.WriteAllBytesAsync(outputPath, bytes, cancellationToken);
        return bytes.LongLength;
    }

    public async Task<byte[]?> CreatePreviewPngAsync(
        string sourcePath,
        int maxPixelSize = 96,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var bitmap = SKBitmap.Decode(sourcePath);
            if (bitmap is null)
            {
                return null;
            }

            var maxSide = Math.Max(bitmap.Width, bitmap.Height);
            var scale = maxSide > maxPixelSize ? maxPixelSize / (double)maxSide : 1d;
            var targetWidth = Math.Max(1, (int)Math.Round(bitmap.Width * scale));
            var targetHeight = Math.Max(1, (int)Math.Round(bitmap.Height * scale));
            var info = new SKImageInfo(targetWidth, targetHeight, SKColorType.Rgba8888, SKAlphaType.Premul);

            using var surface = SKSurface.Create(info);
            surface.Canvas.Clear(SKColors.Transparent);
            surface.Canvas.DrawBitmap(bitmap, new SKRect(0, 0, targetWidth, targetHeight));
            surface.Canvas.Flush();

            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            return data?.ToArray();
        }, cancellationToken);
    }

    public static string GetExtension(string outputFormat)
    {
        return outputFormat.Equals("jpg", StringComparison.OrdinalIgnoreCase) ? "jpg" : outputFormat.ToLowerInvariant();
    }

    private static async Task<byte[]> EncodeAsync(
        string sourcePath,
        string outputFormat,
        int quality,
        CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var bitmap = SKBitmap.Decode(sourcePath);
            if (bitmap is null)
            {
                throw new InvalidDataException("Nao foi possivel decodificar a imagem.");
            }

            if (outputFormat.Equals("ico", StringComparison.OrdinalIgnoreCase))
            {
                return EncodeIcon(bitmap);
            }

            using var image = CreateImageForTarget(bitmap, outputFormat);
            using var data = image.Encode(ToSkiaFormat(outputFormat), Math.Clamp(quality, 1, 100));
            if (data is null)
            {
                throw new InvalidDataException("Nao foi possivel gerar uma imagem nesse formato.");
            }

            return data.ToArray();
        }, cancellationToken);
    }

    private static SKImage CreateImageForTarget(SKBitmap bitmap, string outputFormat)
    {
        if (!outputFormat.Equals("jpg", StringComparison.OrdinalIgnoreCase))
        {
            return SKImage.FromBitmap(bitmap);
        }

        var info = new SKImageInfo(bitmap.Width, bitmap.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        surface.Canvas.Clear(SKColors.White);
        surface.Canvas.DrawBitmap(bitmap, 0, 0);
        surface.Canvas.Flush();
        return surface.Snapshot();
    }

    private static byte[] EncodeIcon(SKBitmap source)
    {
        var width = source.Width;
        var height = source.Height;
        var maxSide = Math.Max(width, height);

        using var iconImage = maxSide > 256
            ? ResizeForIcon(source, width, height, maxSide)
            : SKImage.FromBitmap(source);

        using var pngData = iconImage.Encode(SKEncodedImageFormat.Png, 100);
        if (pngData is null)
        {
            throw new InvalidDataException("Nao foi possivel gerar a imagem interna do ICO.");
        }

        var pngBytes = pngData.ToArray();
        var iconWidth = iconImage.Width >= 256 ? 0 : iconImage.Width;
        var iconHeight = iconImage.Height >= 256 ? 0 : iconImage.Height;
        const int imageOffset = 22;

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write((ushort)1);
        writer.Write((byte)iconWidth);
        writer.Write((byte)iconHeight);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write((uint)pngBytes.Length);
        writer.Write((uint)imageOffset);
        writer.Write(pngBytes);
        writer.Flush();

        return stream.ToArray();
    }

    private static SKImage ResizeForIcon(SKBitmap source, int width, int height, int maxSide)
    {
        var scale = 256d / maxSide;
        var targetWidth = Math.Max(1, (int)Math.Round(width * scale));
        var targetHeight = Math.Max(1, (int)Math.Round(height * scale));
        var info = new SKImageInfo(targetWidth, targetHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);

        surface.Canvas.Clear(SKColors.Transparent);
        surface.Canvas.DrawBitmap(source, new SKRect(0, 0, targetWidth, targetHeight));
        surface.Canvas.Flush();

        return surface.Snapshot();
    }

    private static SKEncodedImageFormat ToSkiaFormat(string outputFormat)
    {
        return outputFormat.ToLowerInvariant() switch
        {
            "webp" => SKEncodedImageFormat.Webp,
            "avif" => SKEncodedImageFormat.Avif,
            "jpg" or "jpeg" => SKEncodedImageFormat.Jpeg,
            "png" => SKEncodedImageFormat.Png,
            _ => throw new NotSupportedException(
                $"Formato de saida nao suportado: {outputFormat}. Use WebP, AVIF, JPG, PNG ou ICO.")
        };
    }

    private static string FormatName(SKEncodedImageFormat format)
    {
        return format switch
        {
            SKEncodedImageFormat.Jpeg => "JPEG",
            SKEncodedImageFormat.Png => "PNG",
            SKEncodedImageFormat.Webp => "WebP",
            SKEncodedImageFormat.Gif => "GIF",
            SKEncodedImageFormat.Bmp => "BMP",
            SKEncodedImageFormat.Ico => "ICO",
            SKEncodedImageFormat.Wbmp => "WBMP",
            SKEncodedImageFormat.Heif => "HEIF",
            _ => format.ToString()
        };
    }
}
