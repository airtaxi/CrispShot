using SkiaSharp;
using System.Runtime.InteropServices;
using CrispShot.Models;
using CrispShot.Enums;

namespace CrispShot.Services;

public sealed class ShadowRendererService
{
    public byte[] Render(CapturedWindow capture, ShadowRenderOptions shadowOptions)
    {
        var width = capture.Width;
        var height = capture.Height;

        using var sourceBitmap = CreateSourceBitmap(capture.BgraPixels, width, height);
        if (shadowOptions.Intensity == ShadowIntensity.Off) return EncodePng(sourceBitmap);

        var shadowPadding = CalculateShadowPadding(shadowOptions.BlurRadius);
        var canvasWidth = width + shadowPadding * 2;
        var canvasHeight = height + shadowPadding * 2;
        using var canvasBitmap = new SKBitmap(canvasWidth, canvasHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(canvasBitmap);
        canvas.Clear(SKColors.Transparent);

        var opacity = Math.Clamp(shadowOptions.OpacityPercent / 100f, 0f, 1f);

        DrawShadow(canvas, sourceBitmap, shadowPadding, shadowPadding, shadowOptions.BlurRadius, opacity);

        canvas.DrawBitmap(sourceBitmap, shadowPadding, shadowPadding);

        return EncodePng(canvasBitmap);
    }

    private static int CalculateShadowPadding(int shadowBlurRadius) => Math.Max(1, (int)Math.Ceiling(shadowBlurRadius * 3.0));

    private static SKBitmap CreateSourceBitmap(byte[] bgraPixels, int width, int height)
    {
        var normalizedPixels = NormalizeTransparentPixels(bgraPixels);
        var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        unsafe
        {
            var pointer = (byte*)bitmap.GetPixels().ToPointer();
            Marshal.Copy(normalizedPixels, 0, (nint)pointer, normalizedPixels.Length);
        }
        return bitmap;
    }

    private static byte[] NormalizeTransparentPixels(byte[] bgraPixels)
    {
        var normalizedPixels = new byte[bgraPixels.Length];
        bgraPixels.CopyTo(normalizedPixels, 0);

        for (var index = 0; index < normalizedPixels.Length; index += 4)
        {
            if (normalizedPixels[index + 3] >= 128) continue;

            normalizedPixels[index] = 0;
            normalizedPixels[index + 1] = 0;
            normalizedPixels[index + 2] = 0;
            normalizedPixels[index + 3] = 0;
        }

        return normalizedPixels;
    }

    private static byte[] EncodePng(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static void DrawShadow(SKCanvas canvas, SKBitmap sourceBitmap, float offsetX, float offsetY, float blurRadius, float opacity)
    {
        using var paint = new SKPaint
        {
            IsAntialias = true,
            ImageFilter = SKImageFilter.CreateBlur(blurRadius, blurRadius),
            ColorFilter = CreateShadowColorFilter(opacity),
        };

        canvas.DrawBitmap(sourceBitmap, offsetX, offsetY, paint);
    }

    private static SKColorFilter CreateShadowColorFilter(float opacity)
    {
        var alphaScale = Math.Clamp(opacity, 0f, 1f);
        return SKColorFilter.CreateColorMatrix([0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, alphaScale, 0, ]);
    }
}

public sealed record ShadowRenderOptions(ShadowIntensity Intensity, int BlurRadius, int OpacityPercent)
{
    public static ShadowRenderOptions FromIntensity(ShadowIntensity shadowIntensity) =>
        shadowIntensity switch
        {
            ShadowIntensity.Off => new ShadowRenderOptions(shadowIntensity, 0, 0),
            ShadowIntensity.Low => new ShadowRenderOptions(shadowIntensity, 16, 65),
            ShadowIntensity.Medium => new ShadowRenderOptions(shadowIntensity, 36, 75),
            ShadowIntensity.High => new ShadowRenderOptions(shadowIntensity, 64, 88),
            _ => new ShadowRenderOptions(ShadowIntensity.Medium, 36, 75),
        };
}
