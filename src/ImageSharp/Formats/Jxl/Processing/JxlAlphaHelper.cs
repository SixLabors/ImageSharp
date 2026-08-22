// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing;

internal sealed class JxlAlphaHelper
{
    // TODO: SIMD support
    private const float SmallAlpha = 1f / (1 << 26);

    // Force x to stay within the range of 0 through 1
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Clamp(float x) => Math.Clamp(x, 0f, 1f);

    public static void PerformAlphaBlending(
        JxlAlphaBlendingInputLayer background,
        JxlAlphaBlendingInputLayer foreground,
        JxlAlphaBlendingOutput output,
        int pixelCount,
        bool alphaIsPremultiplied,
        bool clamp)
    {
        // Store all channels from all parameters into spans, because
        // creating a Span<T> from Memory<T> is expensive, especially
        // in a loop.
        ReadOnlySpan<float> fgR = foreground.R.Span;
        ReadOnlySpan<float> fgG = foreground.G.Span;
        ReadOnlySpan<float> fgB = foreground.B.Span;
        ReadOnlySpan<float> fgA = foreground.A.Span;

        ReadOnlySpan<float> bgR = background.R.Span;
        ReadOnlySpan<float> bgG = background.G.Span;
        ReadOnlySpan<float> bgB = background.B.Span;
        ReadOnlySpan<float> bgA = background.A.Span;

        Span<float> outR = output.R.Span;
        Span<float> outG = output.G.Span;
        Span<float> outB = output.B.Span;
        Span<float> outA = output.A.Span;

        if (alphaIsPremultiplied)
        {
            for (int x = 0; x < pixelCount; x++)
            {
                float fga = clamp ? Clamp(fgA[x]) : fgA[x];
                outR[x] = fgR[x] + (bgR[x] * (1f - fga));
                outG[x] = fgG[x] + (bgG[x] * (1f - fga));
                outB[x] = fgB[x] + (bgB[x] * (1f - fga));
                outA[x] = 1f - ((1f - fga) * (1f - bgA[x]));
            }
        }
        else
        {
            for (int x = 0; x < pixelCount; x++)
            {
                float fga = clamp ? Clamp(fgA[x]) : fgA[x];
                float newA = 1f - ((1f - fga) * (1f - bgA[x]));
                float rnewA = newA > 0 ? 1f / newA : 0f;
                outR[x] = ((fgR[x] * fga) + (bgR[x] * bgA[x] * (1f - fga))) * rnewA;
                outG[x] = ((fgG[x] * fga) + (bgG[x] * bgA[x] * (1f - fga))) * rnewA;
                outB[x] = ((fgB[x] * fga) + (bgB[x] * bgA[x] * (1f - fga))) * rnewA;
                outA[x] = newA;
            }
        }
    }

    public static void PerformAlphaBlending(
        ReadOnlySpan<float> bg,
        ReadOnlySpan<float> bga,
        ReadOnlySpan<float> fg,
        ReadOnlySpan<float> fga,
        Span<float> output,
        int pixelCount,
        bool alphaIsPremultiplied,
        bool clamp)
    {
        if (bg == bga && fg == fga)
        {
            for (int x = 0; x < pixelCount; x++)
            {
                float fa = clamp ? Clamp(fga[x]) : fga[x];
                output[x] = 1f - ((1f - fa) * (1f - bga[x]));
            }
        }
        else
        {
            if (alphaIsPremultiplied)
            {
                for (int x = 0; x < pixelCount; x++)
                {
                    float fa = clamp ? Clamp(fga[x]) : fga[x];
                    output[x] = fg[x] + (bg[x] * (1f - fa));
                }
            }
            else
            {
                for (int x = 0; x < pixelCount; x++)
                {
                    float fa = clamp ? Clamp(fga[x]) : fga[x];
                    float new_a = 1f - ((1f - fa) * (1f - bga[x]));
                    float rnew_a = new_a > 0 ? 1f / new_a : 0f;
                    output[x] = ((fg[x] * fa) + (bg[x] * bga[x] * (1f - fa))) * rnew_a;
                }
            }
        }
    }

    public static void PerformAlphaWeightedAdd(
        ReadOnlySpan<float> bg,
        ReadOnlySpan<float> fg,
        ReadOnlySpan<float> fga,
        Span<float> output,
        int pixelCount,
        bool clamp)
    {
        if (fg == fga)
        {
            bg[pixelCount..].CopyTo(output);
        }
        else if (clamp)
        {
            for (int x = 0; x < pixelCount; x++)
            {
                output[x] = bg[x] + (fg[x] * Clamp(fga[x]));
            }
        }
        else
        {
            for (int x = 0; x < pixelCount; ++x)
            {
                output[x] = bg[x] + (fg[x] * fga[x]);
            }
        }
    }

    public static void PerformMultiplyBlending(
        ReadOnlySpan<float> bg,
        ReadOnlySpan<float> fg,
        Span<float> output,
        int pixelCount,
        bool clamp)
    {
        if (clamp)
        {
            for (int x = 0; x < pixelCount; x++)
            {
                output[x] = bg[x] * Clamp(fg[x]);
            }
        }
        else
        {
            for (int x = 0; x < pixelCount; x++)
            {
                output[x] = bg[x] * fg[x];
            }
        }
    }

    public static void PremultiplyAlpha(
        Span<float> r,
        Span<float> g,
        Span<float> b,
        ReadOnlySpan<float> a,
        int pixelCount)
    {
        for (int x = 0; x < pixelCount; x++)
        {
            float multiplier = Math.Max(SmallAlpha, a[x]);
            r[x] *= multiplier;
            g[x] *= multiplier;
            b[x] *= multiplier;
        }
    }

    public static void UnpremultiplyAlpha(
        Span<float> r,
        Span<float> g,
        Span<float> b,
        ReadOnlySpan<float> a,
        int pixelCount)
    {
        for (int x = 0; x < pixelCount; x++)
        {
            float multiplier = 1f / Math.Max(SmallAlpha, a[x]);
            r[x] *= multiplier;
            g[x] *= multiplier;
            b[x] *= multiplier;
        }
    }
}
