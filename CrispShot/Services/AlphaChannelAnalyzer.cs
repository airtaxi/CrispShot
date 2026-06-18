using CrispShot.Enums;

namespace CrispShot.Services;

public static class AlphaChannelAnalyzer
{
    public static AlphaChannelKind Analyze(byte[] bgraPixels, int width, int height)
    {
        if (bgraPixels.Length < width * height * 4) return AlphaChannelKind.Invalid;

        var totalPixels = width * height;
        var sampleStride = Math.Max(1, totalPixels / 4096);
        var hasPartialAlpha = false;
        var hasAnyTransparency = false;

        for (var sampleIndex = 0; sampleIndex < totalPixels; sampleIndex += sampleStride)
        {
            var alpha = bgraPixels[sampleIndex * 4 + 3];
            if (alpha == 0) hasAnyTransparency = true;
            else if (alpha < 255) hasPartialAlpha = true;
        }

        if (hasPartialAlpha || hasAnyTransparency) return AlphaChannelKind.Present;
        return AlphaChannelKind.Absent;
    }
}