// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding.ContextPrediction;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Transforms;

/// <summary>
/// Palette/indexed decoder and encoder.
/// </summary>
internal static class JxlPalette
{
    /// <summary>
    /// Compares stored palette arrays and borrowed color records with the same value-based contract.
    /// </summary>
    private static readonly PaletteColorComparer ColorKeyComparer = new();

    /// <summary>
    /// Represents maximum number of colors in a palette.
    /// </summary>
    private const int MaxPaletteLookupTableSize = 1 << 16;

    /// <summary>
    /// The first three components defined by implicit palette colors and retained RGB delta statistics.
    /// </summary>
    private const int RgbChannels = 3;

    /// <summary>
    /// 5x5x5 color cube for the larger cube.
    /// </summary>
    private const int LargeCube = 5;

    /// <summary>
    /// Smaller interleaved color cube to fill the holes of the larger cube.
    /// </summary>
    private const int SmallCube = 4;

    /// <summary>
    /// The wire-index bit count for one component of the four-position implicit cube.
    /// </summary>
    private const int SmallCubeBits = 2;

    /// <summary>
    /// Cube of SmallCube
    /// </summary>
    private const int LargeCubeOffset = SmallCube * SmallCube * SmallCube;

    private const int ImplicitPaletteSize = LargeCubeOffset + (LargeCube * LargeCube * LargeCube);

    /// <summary>
    /// The number of fixed RGB delta entries defined by the format.
    /// </summary>
    private const int DeltaPaletteEntries = 72;

    /// <summary>
    /// The first index in one complete signed implicit-delta period.
    /// </summary>
    private const int MinimumImplicitPaletteIndex = -((2 * DeltaPaletteEntries) - 1);

    /// <summary>
    /// Gets native diffusion offsets as adjacent [error row, padded column offset] pairs.
    /// </summary>
    private static ReadOnlySpan<int> DefaultOffsets =>
    [
        1, 2, 0, 3, 0, 4, 1, 1, 1, 3, 2, 2, 1, 0, 1, 4, 2, 1, 2, 3, 2, 0, 2, 4
    ];

    /// <summary>
    /// Static delta palette used by <see cref="GetPaletteValue(ReadOnlySpan{Int32}, int, int, int, int, int)" />.
    /// </summary>
    private static ReadOnlySpan<int> DeltaPalette =>
    [
        0, 0, 0, 4, 4, 4, 11, 0, 0,
        0, 0, -13, 0, -12, 0, -10, -10, -10,
        -18, -18, -18, -27, -27, -27, -18, -18, 0,
        0, 0, -32, -32, 0, 0, -37, -37, -37,
        0, -32, -32, 24, 24, 45, 50, 50, 50,
        -45, -24, -24, -24, -45, -45, 0, -24, -24,
        -34, -34, 0, -24, 0, -24, -45, -45, -24,
        64, 64, 64, -32, 0, -32, 0, -32, 0,
        -32, 0, 32, -24, -45, -24, 45, 24, 45,
        24, -24, -45, -45, -24, 24, 80, 80, 80,
        64, 0, 0, 0, 0, -64, 0, -64, -64,
        -24, -24, 45, 96, 96, 96, 64, 64, 0,
        45, -24, -24, 34, -34, 0, 112, 112, 112,
        24, -45, -45, 45, 45, -24, 0, -32, 32,
        24, -24, 45, 0, 96, 96, 45, -24, 24,
        24, -45, -24, -24, -45, 24, 0, -64, 0,
        96, 0, 0, 128, 128, 128, 64, 0, 64,
        144, 144, 144, 96, 96, 0, -36, -36, 36,
        45, -24, -45, 45, -45, -24, 0, 0, -96,
        0, 128, 128, 0, 96, 0, 45, 24, -45,
        -128, 0, 0, 24, -45, 24, -45, 24, -45,
        64, 0, -64, 64, -64, -64, 96, 0, 96,
        45, -45, 24, 24, 45, -45, 64, 64, -64,
        128, 128, 0, 0, 0, -128, -24, 45, -45
    ];

    /// <summary>
    /// Scales one implicit cube component with the format's fixed denominator of four.
    /// </summary>
    /// <param name="value">The component position within the cube.</param>
    /// <param name="bitDepth">The source sample precision.</param>
    /// <returns>The integer component. Widened multiplication preserves the full supported integer range.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Scale(int value, int bitDepth) => (int)(((long)value * ((1L << bitDepth) - 1)) >> 2);

    /// <summary>
    /// Resolves an explicit palette entry or a format-defined implicit color or delta.
    /// </summary>
    /// <param name="palette">The complete borrowed palette storage, including row padding.</param>
    /// <param name="index">The explicit, implicit or negative delta index.</param>
    /// <param name="c">The component index.</param>
    /// <param name="paletteSize">The logical number of explicit entries, excluding row padding and other component rows.</param>
    /// <param name="oneRow">The physical stride between component rows.</param>
    /// <param name="bitDepth">The sample precision used by implicit entries.</param>
    /// <returns>The selected component. Callers add prediction when this entry is a predictive delta.</returns>
    public static int GetPaletteValue(ReadOnlySpan<int> palette, int index, int c, int paletteSize, int oneRow, int bitDepth)
    {
        if (index < 0)
        {
            if (c >= RgbChannels)
            {
                return 0;
            }

            index = -(index + 1);
            index %= 1 + (2 * (DeltaPaletteEntries - 1));

            // Entry zero has one index. Other entries have opposite-sign pairs, selected by index parity.
            int entry = (index + 1) >> 1;
            int result = DeltaPalette[(entry * RgbChannels) + c] * ((index & 1) != 0 ? 1 : -1);

            if (bitDepth > 8)
            {
                result *= 1 << (bitDepth - 8);
            }

            return result;
        }
        else if (paletteSize <= index && index < paletteSize + LargeCubeOffset)
        {
            if (c >= RgbChannels)
            {
                return 0;
            }

            index -= paletteSize;
            index >>= c * SmallCubeBits;
            return Scale(index % SmallCube, bitDepth) + (1 << Math.Max(0, bitDepth - 3));
        }
        else if (paletteSize + LargeCubeOffset <= index)
        {
            if (c >= RgbChannels)
            {
                return 0;
            }

            index -= paletteSize + LargeCubeOffset;

            switch (c)
            {
                case 1:
                    index /= LargeCube;
                    break;

                case 2:
                    index /= LargeCube * LargeCube;
                    break;

                default:
                    break;
            }

            return Scale(index % LargeCube, bitDepth);
        }

        return palette[(c * oneRow) + index];
    }

    /// <summary>
    /// Replaces equal-shaped channels with their index channel and an owned palette metachannel before decoding.
    /// </summary>
    /// <param name="configuration">The configuration used for palette sample allocation.</param>
    /// <param name="input">The image that owns the retained index channel and new palette.</param>
    /// <param name="beginC">The first channel replaced by palette indices.</param>
    /// <param name="endC">The last channel replaced by palette indices.</param>
    /// <param name="numberOfColors">The number of explicit palette entries.</param>
    /// <param name="numberOfDeltas">The number of predictive delta entries.</param>
    public static void MetaPalette(Configuration configuration, JxlModularImage input, int beginC, int endC, int numberOfColors, int numberOfDeltas)
    {
        JxlTransform.CheckEqualChannels(input, beginC, endC);
        int nb = endC - beginC + 1;
        if (beginC >= input.MetaChannels)
        {
            // Palette was done on normal channels
            input.MetaChannels++;
        }
        else
        {
            // Palette was done on metachannels
            if (endC >= input.MetaChannels)
            {
                throw new InvalidOperationException("End channel offset is out of bounds");
            }

            input.MetaChannels += 2 - nb;
        }

        // Reserve references before allocating samples. The first selected channel remains as the index plane.
        input.Channels.EnsureCapacity(input.Channels.Count + 1);
        JxlModularChannel ch = new(configuration, numberOfColors + numberOfDeltas, nb, -1, -1);

        for (int c = beginC + 1; c <= endC; c++)
        {
            input.Channels[c].Dispose();
        }

        input.Channels.RemoveRange(beginC + 1, endC - beginC);
        input.Channels.Insert(0, ch);
    }

    /// <summary>
    /// Decodes palette/indexed images.
    /// </summary>
    /// <param name="configuration">The configuration used for restored channel samples.</param>
    /// <param name="input">Input &amp; out images.</param>
    /// <param name="beginC">Offset of output channel.</param>
    /// <param name="nbColors">Number of colors.</param>
    /// <param name="nbDeltas">Number of deltas.</param>
    /// <param name="predictor">Kind of predictor mode to use.</param>
    /// <param name="weightedHeader">For weighted prediction.</param>
    /// <exception cref="InvalidOperationException">Thrown when the input for prediction is invalid.</exception>
    /// <exception cref="NotImplementedException">Thrown when there are too many channels.</exception>
    public static void InversePalette(
        Configuration configuration,
        JxlModularImage input,
        int beginC,
        int nbColors,
        int nbDeltas,
        JxlPredictor predictor,
        JxlModularHeader weightedHeader)
    {
        if (input.MetaChannels < 1)
        {
            throw new InvalidOperationException("A palette transform was invoked without a palette");
        }

        int nb = input.Channels[0].Height;
        int c0 = beginC + 1;

        if (c0 >= input.Channels.Count)
        {
            throw new InvalidOperationException("Channel is out of range");
        }

        JxlModularChannel channel = input.Channels[c0];
        int w = channel.Width;
        int h = channel.Height;

        if (nb < 1)
        {
            throw new InvalidOperationException("Transforms are corrupted");
        }

        // Each new channel belongs to input as soon as its samples are allocated. A later failure leaves no unattached owner.
        input.Channels.EnsureCapacity(input.Channels.Count + nb - 1);

        for (int i = 1; i < nb; i++)
        {
            JxlModularChannel newChannel = new(configuration, w, h, channel.HorizontalShift, channel.VerticalShift);
            input.Channels.Insert(c0 + 1, newChannel);
        }

        JxlModularChannel palette = input.Channels[0];

        int oneRow = palette.Plane.PixelsPerRow;
        int bitDepth = Math.Min(input.BitDepth, 24);
        Span<int> paletteData = palette.GetRow(0);

        if (w == 0)
        {
            // Channel is empty. Don't do anything.
        }
        else if (nbDeltas == 0 && predictor == JxlPredictor.Zero)
        {
            if (nb == 1)
            {
                Span<int> channel0Samples = channel.GetRow(0);
                int channel0Stride = channel.Plane.PixelsPerRow;

                for (int y = 0; y < h; y++)
                {
                    Span<int> p = channel0Samples[(y * channel0Stride)..];

                    for (int x = 0; x < w; x++)
                    {
                        int index = Math.Clamp(p[x], 0, palette.Width - 1);

                        p[x] = GetPaletteValue(paletteData, index, 0, palette.Width, oneRow, bitDepth);
                    }
                }
            }
            else if (nb == 2)
            {
                Span<int> channel0Samples = channel.GetRow(0);
                int channel0Stride = channel.Plane.PixelsPerRow;
                Span<int> channel1Samples = input.Channels[c0 + 1].GetRow(0);
                int channel1Stride = input.Channels[c0 + 1].Plane.PixelsPerRow;

                for (int y = 0; y < h; y++)
                {
                    Span<int> p0 = channel0Samples[(y * channel0Stride)..];
                    Span<int> p1 = channel1Samples[(y * channel1Stride)..];

                    for (int x = 0; x < w; x++)
                    {
                        int index0 = Math.Clamp(p0[x], 0, palette.Width - 1);
                        int index1 = Math.Clamp(p1[x], 0, palette.Width - 1);

                        p0[x] = GetPaletteValue(paletteData, index0, 0, palette.Width, oneRow, bitDepth);
                        p1[x] = GetPaletteValue(paletteData, index1, 0, palette.Width, oneRow, bitDepth);
                    }
                }
            }
            else if (nb == 3)
            {
                Span<int> channel0Samples = channel.GetRow(0);
                int channel0Stride = channel.Plane.PixelsPerRow;
                Span<int> channel1Samples = input.Channels[c0 + 1].GetRow(0);
                int channel1Stride = input.Channels[c0 + 1].Plane.PixelsPerRow;
                Span<int> channel2Samples = input.Channels[c0 + 2].GetRow(0);
                int channel2Stride = input.Channels[c0 + 2].Plane.PixelsPerRow;

                for (int y = 0; y < h; y++)
                {
                    Span<int> p0 = channel0Samples[(y * channel0Stride)..];
                    Span<int> p1 = channel1Samples[(y * channel1Stride)..];
                    Span<int> p2 = channel2Samples[(y * channel2Stride)..];

                    for (int x = 0; x < w; x++)
                    {
                        int index0 = Math.Clamp(p0[x], 0, palette.Width - 1);
                        int index1 = Math.Clamp(p1[x], 0, palette.Width - 1);
                        int index2 = Math.Clamp(p2[x], 0, palette.Width - 1);

                        p0[x] = GetPaletteValue(paletteData, index0, 0, palette.Width, oneRow, bitDepth);
                        p1[x] = GetPaletteValue(paletteData, index1, 0, palette.Width, oneRow, bitDepth);
                        p2[x] = GetPaletteValue(paletteData, index2, 0, palette.Width, oneRow, bitDepth);
                    }
                }
            }
            else if (nb == 4)
            {
                Span<int> channel0Samples = channel.GetRow(0);
                int channel0Stride = channel.Plane.PixelsPerRow;
                Span<int> channel1Samples = input.Channels[c0 + 1].GetRow(0);
                int channel1Stride = input.Channels[c0 + 1].Plane.PixelsPerRow;
                Span<int> channel2Samples = input.Channels[c0 + 2].GetRow(0);
                int channel2Stride = input.Channels[c0 + 2].Plane.PixelsPerRow;
                Span<int> channel3Samples = input.Channels[c0 + 3].GetRow(0);
                int channel3Stride = input.Channels[c0 + 3].Plane.PixelsPerRow;

                for (int y = 0; y < h; y++)
                {
                    Span<int> p0 = channel0Samples[(y * channel0Stride)..];
                    Span<int> p1 = channel1Samples[(y * channel1Stride)..];
                    Span<int> p2 = channel2Samples[(y * channel2Stride)..];
                    Span<int> p3 = channel3Samples[(y * channel3Stride)..];

                    for (int x = 0; x < w; x++)
                    {
                        int index0 = Math.Clamp(p0[x], 0, palette.Width - 1);
                        int index1 = Math.Clamp(p1[x], 0, palette.Width - 1);
                        int index2 = Math.Clamp(p2[x], 0, palette.Width - 1);
                        int index3 = Math.Clamp(p3[x], 0, palette.Width - 1);

                        p0[x] = GetPaletteValue(paletteData, index0, 0, palette.Width, oneRow, bitDepth);
                        p1[x] = GetPaletteValue(paletteData, index1, 0, palette.Width, oneRow, bitDepth);
                        p2[x] = GetPaletteValue(paletteData, index2, 0, palette.Width, oneRow, bitDepth);
                        p3[x] = GetPaletteValue(paletteData, index3, 0, palette.Width, oneRow, bitDepth);
                    }
                }
            }
            else if (nb == 5)
            {
                Span<int> channel0Samples = channel.GetRow(0);
                int channel0Stride = channel.Plane.PixelsPerRow;
                Span<int> channel1Samples = input.Channels[c0 + 1].GetRow(0);
                int channel1Stride = input.Channels[c0 + 1].Plane.PixelsPerRow;
                Span<int> channel2Samples = input.Channels[c0 + 2].GetRow(0);
                int channel2Stride = input.Channels[c0 + 2].Plane.PixelsPerRow;
                Span<int> channel3Samples = input.Channels[c0 + 3].GetRow(0);
                int channel3Stride = input.Channels[c0 + 3].Plane.PixelsPerRow;
                Span<int> channel4Samples = input.Channels[c0 + 4].GetRow(0);
                int channel4Stride = input.Channels[c0 + 4].Plane.PixelsPerRow;

                for (int y = 0; y < h; y++)
                {
                    Span<int> p0 = channel0Samples[(y * channel0Stride)..];
                    Span<int> p1 = channel1Samples[(y * channel1Stride)..];
                    Span<int> p2 = channel2Samples[(y * channel2Stride)..];
                    Span<int> p3 = channel3Samples[(y * channel3Stride)..];
                    Span<int> p4 = channel4Samples[(y * channel4Stride)..];

                    for (int x = 0; x < w; x++)
                    {
                        int index0 = Math.Clamp(p0[x], 0, palette.Width - 1);
                        int index1 = Math.Clamp(p1[x], 0, palette.Width - 1);
                        int index2 = Math.Clamp(p2[x], 0, palette.Width - 1);
                        int index3 = Math.Clamp(p3[x], 0, palette.Width - 1);
                        int index4 = Math.Clamp(p4[x], 0, palette.Width - 1);

                        p0[x] = GetPaletteValue(paletteData, index0, 0, palette.Width, oneRow, bitDepth);
                        p1[x] = GetPaletteValue(paletteData, index1, 0, palette.Width, oneRow, bitDepth);
                        p2[x] = GetPaletteValue(paletteData, index2, 0, palette.Width, oneRow, bitDepth);
                        p3[x] = GetPaletteValue(paletteData, index3, 0, palette.Width, oneRow, bitDepth);
                        p4[x] = GetPaletteValue(paletteData, index4, 0, palette.Width, oneRow, bitDepth);
                    }
                }
            }
            else
            {
                throw new NotImplementedException($"Too many channels for palette compressed images: {nb}");
            }
        }
        else
        {
            JxlPlane<int> plane = input.Channels[c0].Plane;
            using JxlPlane<int> indices = new(configuration, plane.XSize, plane.YSize);

            // Keep the original index samples separate from reconstructed output. The channel retains its plane wrapper;
            // the scoped temporary receives the old index allocation and releases it after every output channel has read it.
            plane.Swap(indices);

            Span<int> indexSamples = indices.GetRow(0);
            int indexStride = indices.PixelsPerRow;

            // Read each output stride after the swap. The original indices can retain wider storage from a metadata shrink.
            if (predictor == JxlPredictor.Weighted)
            {
                using JxlModularState wpState = new(configuration, weightedHeader, indices.XSize, 1);

                for (int c = 0; c < nb; c++)
                {
                    JxlModularChannel outputChannel = input.Channels[c0 + c];
                    int oneRowImage = outputChannel.Plane.PixelsPerRow;
                    Span<int> outputSamples = outputChannel.GetRow(0);
                    Span<uint> weightedStorage = wpState.Reset(weightedHeader, outputChannel.Width);

                    for (int y = 0; y < outputChannel.Height; y++)
                    {
                        Span<int> p = outputSamples[(y * oneRowImage)..];
                        Span<int> idx = indexSamples[(y * indexStride)..];

                        for (int x = 0; x < outputChannel.Width; x++)
                        {
                            int index = idx[x];
                            int value = 0;
                            int paletteEntry = GetPaletteValue(paletteData, index, c, palette.Width, oneRow, bitDepth);
                            JxlPredictionResult pred = JxlContextPrediction.PredictNoTreeWeightedPrediction(
                                outputChannel.Width,
                                ref p[x],
                                oneRowImage,
                                x,
                                y,
                                predictor,
                                wpState,
                                weightedStorage,
                                0);

                            if (index < nbDeltas)
                            {
                                value = pred.Guess + paletteEntry;
                            }
                            else
                            {
                                value = paletteEntry;
                            }

                            p[x] = value;
                            wpState.UpdatePredictionErrors(p[x], x, y, weightedStorage, 0);
                        }
                    }
                }
            }
            else
            {
                for (int c = 0; c < nb; c++)
                {
                    JxlModularChannel outputChannel = input.Channels[c0 + c];
                    int oneRowImage = outputChannel.Plane.PixelsPerRow;
                    Span<int> outputSamples = outputChannel.GetRow(0);

                    for (int y = 0; y < outputChannel.Height; y++)
                    {
                        Span<int> p = outputSamples[(y * oneRowImage)..];
                        Span<int> idx = indexSamples[(y * indexStride)..];

                        for (int x = 0; x < outputChannel.Width; x++)
                        {
                            int index = idx[x];
                            int value = 0;
                            int paletteEntry = GetPaletteValue(paletteData, index, c, palette.Width, oneRow, bitDepth);

                            if (index < nbDeltas)
                            {
                                JxlPredictionResult pred = JxlContextPrediction.PredictNoTreeNoWeightedPrediction(
                                    outputChannel.Width,
                                    p[x..],
                                    oneRowImage,
                                    x,
                                    y,
                                    predictor);

                                value = pred.Guess + paletteEntry;
                            }
                            else
                            {
                                value = paletteEntry;
                            }

                            p[x] = value;
                        }
                    }
                }
            }
        }

        if (c0 >= input.MetaChannels)
        {
            input.MetaChannels--;
        }
        else
        {
            if (input.MetaChannels < 2 - nb)
            {
                throw new InvalidOperationException("Too many meta channels");
            }

            input.MetaChannels -= 2 - nb;

            if (beginC + nb - 1 >= input.MetaChannels)
            {
                throw new InvalidOperationException("Too many meta channels");
            }

        }

        // Both normal-channel and metachannel palettes consume the leading palette after its final read.
        palette.Dispose();
        input.Channels.RemoveAt(0);
    }

    /// <summary>
    /// Calculates the native palette-search distance for matching component vectors, including extra channels.
    /// </summary>
    /// <param name="a">The source components with diffusion error applied.</param>
    /// <param name="b">The candidate reconstructed components.</param>
    /// <returns>The weighted component and luminance error used to rank palette candidates.</returns>
    private static float ColorDistance(ReadOnlySpan<float> a, ReadOnlySpan<int> b)
    {
        float distance = 0;
        float ave3 = 0;

        if (a.Length >= RgbChannels)
        {
            ave3 = (a[0] + b[0] + a[1] + b[1] + a[2] + b[2]) * (1.21F / 3F);
        }

        float sumA = 0;
        float sumB = 0;

        for (int c = 0; c < a.Length; c++)
        {
            float diff = a[c] - b[c];
            float weight = c == 0 ? 3F : c == 1 ? 5F : 2F;

            if (c < RgbChannels && a[c] + b[c] >= ave3)
            {
                weight += c == 2 ? 1.12F : 1.15F;

                if (c == 2 && a[2] + b[2] < 1.22D * ave3)
                {
                    weight -= 0.5F;
                }
            }

            distance += diff * diff * weight * weight;
            int sumWeight = c == 0 ? 3 : c == 1 ? 5 : 1;
            sumA += a[c] * sumWeight;
            sumB += b[c] * sumWeight;
        }

        distance *= 4;
        float sumDiff = sumA - sumB;
        return distance + (sumDiff * sumDiff);
    }

    /// <summary>
    /// Selects the nearest implicit cube entry using the native integer quantization formula.
    /// </summary>
    /// <param name="color">The bounded source components.</param>
    /// <param name="paletteSize">The number of explicit entries preceding the two implicit cubes.</param>
    /// <param name="bitDepth">The source precision used to quantize cube positions.</param>
    /// <param name="highQuality">Whether to select the larger five-position cube instead of the offset four-position cube.</param>
    /// <returns>The implicit index after the explicit palette entries.</returns>
    private static int QuantizeColorToImplicitPaletteIndex(Span<int> color, int paletteSize, int bitDepth, bool highQuality)
    {
        int index = 0;
        int quant = (1 << bitDepth) - 1;
        int half = bitDepth > 1 ? (1 << (bitDepth - 1)) : 0;

        if (highQuality)
        {
            int multiplier = 1;

            for (int i = 0; i < color.Length; i++)
            {
                int value = color[i];
                int quantized = (((LargeCube - 1) * value) + half) / quant;
                index += quantized * multiplier;
                multiplier *= LargeCube;
            }

            return index + paletteSize + LargeCubeOffset;
        }
        else
        {
            int multiplier = 1;
            int bdMinus3 = 1 << Math.Max(0, bitDepth - 3);

            for (int i = 0; i < color.Length; i++)
            {
                int value = color[i];
                value -= bdMinus3;
                value = Math.Max(0, value);

                int quantized = (((LargeCube - 1) * value) + half) / quant;
                quantized = Math.Min(quantized, SmallCube - 1); // cannot be > SmallCube - 1

                index += quantized * multiplier;
                multiplier *= SmallCube;
            }

            return index + paletteSize;
        }
    }

    /// <summary>
    /// Rounds an integer residual to the nearest bucket with symmetric handling of negative residuals.
    /// </summary>
    /// <param name="value">The signed residual.</param>
    /// <param name="div">The positive bucket size.</param>
    /// <returns>The signed nearest bucket index. Midpoints round away from zero.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int RoundInteger(int value, int div)
    {
        // Widen before negation or midpoint addition. Restore the sign before narrowing so Int32.MinValue remains representable.
        long magnitude = value < 0 ? -(long)value : value;
        long rounded = (magnitude + (div / 2)) / div;
        return (int)(value < 0 ? -rounded : rounded);
    }

    /// <summary>
    /// Encodes an image into palette/indexed coding mode.
    /// </summary>
    /// <param name="configuration">Configuration for memory allocation/management.</param>
    /// <param name="input">Input image.</param>
    /// <param name="beginC">Offset of starting channel.</param>
    /// <param name="endC">Offset of final channel.</param>
    /// <param name="numberOfColors">Number of colors.</param>
    /// <param name="numberOfDeltas">Number of deltas.</param>
    /// <param name="ordered">Should the output palette produced by this method be sorted?</param>
    /// <param name="lossy">Should the palette be quantized in lossy mode? (May discard subtle pixel values)</param>
    /// <param name="predictor">The kind of predictor that was used.</param>
    /// <param name="wpHeader">Header for weighted prediction.</param>
    public static void ForwardPalette(
        Configuration configuration,
        JxlModularImage input,
        int beginC,
        int endC,
        ref int numberOfColors,
        ref int numberOfDeltas,
        bool ordered,
        bool lossy,
        ref JxlPredictor predictor,
        JxlModularHeader wpHeader)
    {
        // Only the lossy preprocessing pass retains per-pixel delta statistics. The final pass reuses the same tracked buffers.
        // Statistics measure RGB residuals; one- and two-component palettes search directly without an RGB preprocessing pass.
        bool preprocess = lossy && input.BitDepth >= 8 && endC - beginC + 1 >= RgbChannels;
        int pixelCapacity = preprocess
            ? checked(input.Channels[beginC].Width * input.Channels[beginC].Height)
            : 0;

        using PaletteIterationData paletteIterationData = new(configuration, pixelCapacity);
        int originalNumberOfColors = numberOfColors;
        int originalNumberOfDeltas = numberOfDeltas;

        if (preprocess)
        {
            ForwardPaletteIteration(
                configuration,
                input,
                beginC,
                endC,
                ref originalNumberOfColors,
                ref originalNumberOfDeltas,
                ordered,
                lossy,
                ref predictor,
                wpHeader,
                paletteIterationData);
        }

        paletteIterationData.IsFinalRun = true;
        ForwardPaletteIteration(
            configuration,
            input,
            beginC,
            endC,
            ref numberOfColors,
            ref numberOfDeltas,
            ordered,
            lossy,
            ref predictor,
            wpHeader,
            paletteIterationData);
    }

    /// <summary>
    /// Searches palette entries with scoped quantized samples and transfers a completed palette to the image.
    /// </summary>
    /// <param name="configuration">The configuration used for temporary and output samples.</param>
    /// <param name="input">The owner of the source channels and any completed palette.</param>
    /// <param name="beginC">The first source channel.</param>
    /// <param name="endC">The last source channel.</param>
    /// <param name="numberOfColors">The palette size limit, replaced with the selected count.</param>
    /// <param name="numberOfDeltas">The delta-entry count, updated by the search.</param>
    /// <param name="ordered">Whether to order selected colors.</param>
    /// <param name="lossy">Whether to quantize source colors.</param>
    /// <param name="predictor">The selected prediction method.</param>
    /// <param name="wpHeader">The weighted-prediction settings.</param>
    /// <param name="paletteIterationData">The search statistics and final-pass selection.</param>
    private static void ForwardPaletteIteration(
        Configuration configuration,
        JxlModularImage input,
        int beginC,
        int endC,
        ref int numberOfColors,
        ref int numberOfDeltas,
        bool ordered,
        bool lossy,
        ref JxlPredictor predictor,
        JxlModularHeader wpHeader,
        PaletteIterationData paletteIterationData)
    {
        JxlTransform.CheckEqualChannels(input, beginC, endC);
        DebugGuard.MustBeGreaterThanOrEqualTo(beginC, input.MetaChannels, nameof(beginC));
        int nb = endC - beginC + 1; // inclusive number of channels

        JxlModularChannel beginCChannel = input.Channels[beginC];
        int w = beginCChannel.Width;
        int h = beginCChannel.Height;
        Span<int> sourceSamples = beginCChannel.GetRow(0);
        int sourceStride = beginCChannel.Plane.PixelsPerRow;

        // Every successful palette adds one leading channel. Reserve its reference slot before allocating palette samples.
        input.Channels.EnsureCapacity(input.Channels.Count + 1);

        if (input.BitDepth >= 32)
        {
            throw new InvalidOperationException("Bit depth is too large");
        }

        if (!lossy && numberOfColors < 2)
        {
            throw new InvalidOperationException("A lossless palette needs at least two colors.");
        }

        int idx = 0;

        if (!lossy && nb == 1)
        {
            if (numberOfColors == 0)
            {
                throw new InvalidOperationException("No colors");
            }

            JxlImageOperations.ImageMinMax(beginCChannel.Plane, out int minValue, out int maxValue);
            long lookupTableSize = (long)maxValue - minValue + 1;

            if (lookupTableSize > MaxPaletteLookupTableSize)
            {
                SortedSet<int> chPalette = [];

                for (int y = 0; y < h; y++)
                {
                    Span<int> p = sourceSamples[(y * sourceStride)..];

                    for (int x = 0; x < w; x++)
                    {
                        bool newColor = chPalette.Add(p[x]);

                        if (newColor)
                        {
                            idx++;

                            if (idx > numberOfColors)
                            {
                                throw new InvalidOperationException("Index out of bounds");
                            }
                        }
                    }
                }

                // Attach the owner before filling its samples. The image releases it if a later encoding step fails.
                JxlModularChannel modularChannel = new(configuration, idx, 1, -1, -1);
                input.Channels.Insert(0, modularChannel);

                numberOfColors = idx;
                idx = 0;

                Span<int> ppalette = modularChannel.GetRow(0);

                foreach (int p in chPalette)
                {
                    ppalette[idx++] = p;
                }

                for (int y = 0; y < h; y++)
                {
                    Span<int> p = sourceSamples[(y * sourceStride)..];

                    for (int x = 0; x < w; x++)
                    {
                        for (idx = 0; p[x] != ppalette[idx] && idx < numberOfColors; idx++)
                        {
                            // nop; this is to find the value of idx
                        }

                        p[x] = idx;
                    }
                }

                predictor = JxlPredictor.Zero;
                input.MetaChannels++;
                return;
            }

            using IMemoryOwner<int> lookupOwner = configuration.MemoryAllocator.Allocate<int>((int)lookupTableSize, AllocationOptions.Clean);
            Span<int> lookup = lookupOwner.GetSpan()[..(int)lookupTableSize];
            idx = 0;

            for (int y = 0; y < h; y++)
            {
                Span<int> p = sourceSamples[(y * sourceStride)..];
                for (int x = 0; x < w; x++)
                {
                    if (lookup[p[x] - minValue] == 0)
                    {
                        lookup[p[x] - minValue] = 1;
                        idx++;

                        if (idx > numberOfColors)
                        {
                            throw new InvalidOperationException("Index out of bounds");
                        }
                    }
                }
            }

            // Attach before filling so input owns the palette on success and on partial failure.
            JxlModularChannel channel = new(configuration, idx, 1, -1, -1);
            input.Channels.Insert(0, channel);
            numberOfColors = idx;
            idx = 0;
            Span<int> pPalette = channel.GetRow(0);

            for (int i = 0; i < lookupTableSize; i++)
            {
                if (lookup[i] != 0)
                {
                    pPalette[idx] = i + minValue;
                    lookup[i] = idx;
                    idx++;
                }
            }

            for (int y = 0; y < h; y++)
            {
                Span<int> p = sourceSamples[(y * sourceStride)..];

                for (int x = 0; x < w; x++)
                {
                    p[x] = lookup[p[x] - minValue];
                }
            }

            predictor = JxlPredictor.Zero;
            input.MetaChannels++;
            return;
        }

        // Native palette search retains a reconstructed copy. One contiguous allocation covers every selected channel and four candidate vectors.
        // A current pixel still contains its original sample until the winning candidate is stored. Earlier pixels contain reconstructed history.
        int channelLength = checked(w * h);
        int quantizedLength = lossy ? checked(nb * channelLength) : 0;
        using IMemoryOwner<int>? quantizedOwner = lossy
            ? configuration.MemoryAllocator.Allocate<int>(checked(quantizedLength + (4 * nb)))
            : null;

        Span<int> quantizedStorage = quantizedOwner is null ? [] : quantizedOwner.GetSpan()[..(quantizedLength + (4 * nb))];
        Span<int> quantizedSamples = lossy ? quantizedStorage[..quantizedLength] : [];
        Span<int> candidateStorage = lossy ? quantizedStorage[quantizedLength..] : [];

        // Lossless search needs complete color records, not reconstructed history. Reuse one interleaved row across both scans.
        using IMemoryOwner<int>? colorRowOwner = lossy ? null : configuration.MemoryAllocator.Allocate<int>(checked(nb * w));
        Span<int> colorRow = colorRowOwner is null ? [] : colorRowOwner.GetSpan()[..(nb * w)];

        if (lossy)
        {
            for (int c = 0; c < nb; c++)
            {
                JxlModularChannel sourceChannel = input.Channels[beginC + c];
                Span<int> original = sourceChannel.GetRow(0);
                int originalStride = sourceChannel.Plane.PixelsPerRow;
                Span<int> reconstructed = quantizedSamples.Slice(c * channelLength, channelLength);

                for (int y = 0; y < h; y++)
                {
                    original.Slice(y * originalStride, w).CopyTo(reconstructed.Slice(y * w, w));
                }
            }
        }

        Span<int> deltaSamples = paletteIterationData.DeltaSamples;
        Span<double> deltaDistances = paletteIterationData.DeltaDistances;
        Span<int> frequentDeltas = paletteIterationData.FrequentDeltas;
        numberOfDeltas = 0;
        bool deltaUsed = false;
        HashSet<int[]> candidatePalette = new(ColorKeyComparer);
        HashSet<int[]>.AlternateLookup<ReadOnlySpan<int>> candidateLookup = candidatePalette.GetAlternateLookup<ReadOnlySpan<int>>();
        List<int[]> candidatePaletteImageOrder = [];
        Dictionary<int[], int> inversePalette = new(ColorKeyComparer);
        Dictionary<int[], int>.AlternateLookup<ReadOnlySpan<int>> inverseLookup = inversePalette.GetAlternateLookup<ReadOnlySpan<int>>();

        // This mutable array supplies lookup scratch. Stored collection keys are cloned only when a new color is inserted.
        int[] color = new int[nb];
        Span<int> colorSpan = color.AsSpan();

        if (lossy)
        {
            paletteIterationData.FindFrequentColorDeltas(w * h, input.BitDepth);
            numberOfDeltas = paletteIterationData.NumberOfFrequentDeltas;
            SortedDictionary<int[], int> colorFrequencyMap = new(ColorKeyComparer);

            // All cross and frequency scans finish before reconstructed samples replace originals in the workspace.
            for (int y = 1; y + 1 < h; y++)
            {
                for (int x = 1; x + 1 < w; x++)
                {
                    bool makesCross = true;
                    for (int c = 0; c < nb; c++)
                    {
                        int position = (c * channelLength) + (y * w) + x;
                        int value = quantizedSamples[position];
                        color[c] = value;
                        makesCross &= quantizedSamples[position - 1] == value && quantizedSamples[position + 1] == value &&
                                      quantizedSamples[position - w] == value && quantizedSamples[position + w] == value;
                    }

                    if (makesCross)
                    {
                        if (colorFrequencyMap.TryGetValue(color, out int frequency))
                        {
                            colorFrequencyMap[color] = frequency + 1;
                        }
                        else
                        {
                            colorFrequencyMap.Add(colorSpan.ToArray(), 1);
                        }
                    }
                }
            }

            const float imageFraction = 0.01f;
            int colorFrequencyLowerBound = 5 + (int)(input.Height * input.Width * imageFraction);

            foreach (KeyValuePair<int[], int> colorFreq in colorFrequencyMap)
            {
                if (colorFreq.Value > colorFrequencyLowerBound)
                {
                    candidatePalette.Add(colorFreq.Key);
                    candidatePaletteImageOrder.Add(colorFreq.Key);
                }
            }
        }

        HashSet<int[]> implicitColor = new(ColorKeyComparer);
        int[][] implicitColors = new int[ImplicitPaletteSize][];

        for (int k = 0; k < ImplicitPaletteSize; k++)
        {
            int[] implicitValue = new int[nb];
            for (int c = 0; c < nb; c++)
            {
                implicitValue[c] = GetPaletteValue([], k, c, 0, 0, input.BitDepth);
            }

            implicitColor.Add(implicitValue);
            implicitColors[k] = implicitValue;
        }

        int implicitColorsUsed = 0;
        Dictionary<int[], int> colorFreqMap = new(ColorKeyComparer);
        for (int y = 0; y < h; y++)
        {
            if (!lossy)
            {
                PrepareColorRow(input, beginC, nb, y, colorRow);
            }

            for (int x = 0; x < w; x++)
            {
                if (lossy && candidatePalette.Count >= numberOfColors)
                {
                    break;
                }

                ReadOnlySpan<int> record = colorRow;
                if (lossy)
                {
                    for (int c = 0; c < nb; c++)
                    {
                        color[c] = quantizedSamples[(c * channelLength) + (y * w) + x];
                    }

                    record = colorSpan;
                }
                else
                {
                    record = colorRow.Slice(x * nb, nb);
                }

                if (!candidateLookup.TryGetValue(record, out int[]? key))
                {
                    // Hash keys describe values. Retain a new immutable key only when this complete color is first encountered.
                    key = record.ToArray();
                    candidatePalette.Add(key);

                    if (implicitColor.Contains(key))
                    {
                        implicitColorsUsed++;
                    }
                    else
                    {
                        candidatePaletteImageOrder.Add(key);
                        if (candidatePaletteImageOrder.Count > numberOfColors)
                        {
                            throw new InvalidOperationException("Too many colors for palette/indexed");
                        }
                    }
                }

                ref int frequency = ref CollectionsMarshal.GetValueRefOrAddDefault(colorFreqMap, key, out _);
                frequency++;
            }
        }

        numberOfColors = numberOfDeltas + candidatePaletteImageOrder.Count;
        if (!lossy && numberOfColors + implicitColorsUsed == 1)
        {
            // It's not useful to have a single-color palette.
            throw new InvalidOperationException("Palette only has one color");
        }

        for (int k = 0; k < ImplicitPaletteSize; k++)
        {
            int[] implicitValue = implicitColors[k];
            colorFreqMap.TryGetValue(implicitValue, out int frequency);
            if (frequency > 10)
            {
                numberOfColors++;
                candidatePaletteImageOrder.Add(implicitValue);
            }
        }

        for (int k = 0; k < ImplicitPaletteSize; k++)
        {
            inversePalette[implicitColors[k]] = numberOfColors + k;
        }

        // This owner transfers to input only when the final encoding pass completes.
        JxlModularChannel newChannel = new(configuration, numberOfColors, nb, -1, -1);
        bool transferred = false;

        try
        {
            Span<int> palette = newChannel.GetRow(0);
            int oneRow = newChannel.Plane.PixelsPerRow;
            int bitDepth = Math.Min(input.BitDepth, 24); // max. 24 bits, cannot be greater
            long maximumSample = (1L << input.BitDepth) - 1;
            double distanceScale = 32D / (1L << Math.Max(0, 2 * (bitDepth - 8)));

            if (lossy)
            {
                // The encoder measures frequent deltas for RGB only. Zero extra-component deltas retain each component's predicted value.
                // Explicit palette entries have no implicit zero rule; initialize only these otherwise unwritten delta columns.
                for (int c = RgbChannels; c < nb; c++)
                {
                    palette.Slice(c * oneRow, numberOfDeltas).Clear();
                }

                for (int i = 0; i < numberOfDeltas; i++)
                {
                    for (int c = 0; c < 3; c++)
                    {
                        palette[(c * oneRow) + i] = frequentDeltas[(c * PaletteIterationData.MaxDeltas) + i];
                    }
                }
            }

            float frequencyThreshold = 4f;
            int clr = 0;

            if (ordered && nb >= 3)
            {
                candidatePaletteImageOrder.Sort((ap, bp) =>
                {
                    float ay = (0.299F * ap[0]) + (0.587F * ap[1]) + (0.114F * ap[2]) + 0.1F;

                    if (ap.Length > 3)
                    {
                        ay *= 1F + ap[3];
                    }

                    float by = (0.299F * bp[0]) + (0.587F * bp[1]) + (0.114F * bp[2]) + 0.1F;

                    if (bp.Length > 3)
                    {
                        by *= 1F + bp[3];
                    }

                    colorFreqMap.TryGetValue(ap, out int frequencyA);
                    colorFreqMap.TryGetValue(bp, out int frequencyB);
                    ay = frequencyA > frequencyThreshold ? -ay : ay;
                    by = frequencyB > frequencyThreshold ? -by : by;

                    return ay.CompareTo(by);
                });
            }

            foreach (int[] pcol in candidatePaletteImageOrder)
            {
                Span<int> pcolSpan = pcol.AsSpan();

                for (int i = 0; i < nb; i++)
                {
                    palette[numberOfDeltas + (i * oneRow) + clr] = pcolSpan[i];
                }

                inversePalette[pcol] = clr++;
            }

            // Lossy palette search interleaves channels within each pixel. Their independent histories share one tracked allocation.
            using JxlModularState? wpState = lossy ? new(configuration, wpHeader, w, nb) : null;
            Span<uint> weightedStorage = wpState is not null ? wpState.Reset(wpHeader, w) : default;

            // Three planar error rows cover y, y+1 and y+2. Each channel has two padding positions at both ends.
            // Rotate these borrowed spans after a row; no row copies or per-pixel owners are needed.
            int errorStride = checked(w + 4);
            int errorPlaneLength = checked(nb * errorStride);
            using IMemoryOwner<float>? errorOwner = lossy
                ? configuration.MemoryAllocator.Allocate<float>(checked((3 * errorPlaneLength) + nb), AllocationOptions.Clean)
                : null;

            Span<float> errors = errorOwner is null ? [] : errorOwner.GetSpan()[..((3 * errorPlaneLength) + nb)];
            Span<float> error0 = lossy ? errors[..errorPlaneLength] : [];
            Span<float> error1 = lossy ? errors.Slice(errorPlaneLength, errorPlaneLength) : [];
            Span<float> error2 = lossy ? errors.Slice(2 * errorPlaneLength, errorPlaneLength) : [];
            Span<float> colorWithError = lossy ? errors[(3 * errorPlaneLength)..] : [];
            ReadOnlySpan<int> errorOffsets = DefaultOffsets;
            Span<int> bestValue = lossy ? candidateStorage[..nb] : [];
            Span<int> idealResidual = lossy ? candidateStorage.Slice(nb, nb) : [];
            Span<int> quantizedValue = lossy ? candidateStorage.Slice(2 * nb, nb) : [];
            Span<int> predictions = lossy ? candidateStorage.Slice(3 * nb, nb) : [];
            ReadOnlySpan<double> diffusionMultipliers = [0.55, 0.75];
            using JxlPlane<int>? indexPlane = paletteIterationData.IsFinalRun ? new(configuration, w, h) : null;
            Span<int> indexSamples = indexPlane is null ? [] : indexPlane.GetRow(0);
            int indexStride = indexPlane is null ? 0 : indexPlane.PixelsPerRow;

            for (int y = 0; y < h; y++)
            {
                if (!lossy)
                {
                    PrepareColorRow(input, beginC, nb, y, colorRow);
                }

                Span<int> p = indexSamples[(y * indexStride)..];

                for (int x = 0; x < w; x++)
                {
                    int index;
                    if (wpState is null)
                    {
                        index = inverseLookup[colorRow.Slice(x * nb, nb)];
                    }
                    else
                    {
                        int bestIndex = 0;
                        bool bestIsDelta = false;
                        float bestDistance = float.PositiveInfinity;

                        candidateStorage.Clear();

                        // Both diffusion trials use the same reconstructed neighbors and error histories.
                        // Predict once; histories change only after the winning candidate is selected.
                        for (int c = 0; c < nb; c++)
                        {
                            predictions[c] = JxlContextPrediction.PredictNoTreeWeightedPrediction(
                                w,
                                ref quantizedSamples[(c * channelLength) + (y * w) + x],
                                w,
                                x,
                                y,
                                predictor,
                                wpState,
                                weightedStorage,
                                c).Guess;
                        }

                        foreach (double diffusionMultiplier in diffusionMultipliers)
                        {
                            for (int c = 0; c < nb; c++)
                            {
                                colorWithError[c] = (float)(quantizedSamples[(c * channelLength) + (y * w) + x] +
                                    ((paletteIterationData.IsFinalRun ? 1 : 0) * diffusionMultiplier * error0[(c * errorStride) + x + 2]));

                                long rounded = (long)MathF.Round(colorWithError[c], MidpointRounding.AwayFromZero);
                                color[c] = (int)Math.Clamp(rounded, 0L, maximumSample);
                            }

                            // Visit the explicit/negative entries, then the two nearest implicit cube candidates in native order.
                            // This is one scoring body; no delegate captures spans or allocates a per-pixel candidate operation.
                            for (int candidate = MinimumImplicitPaletteIndex; candidate < numberOfColors + 2; candidate++)
                            {
                                int candidateIndex = candidate < numberOfColors
                                    ? candidate
                                    : QuantizeColorToImplicitPaletteIndex(color, numberOfColors, bitDepth, candidate != numberOfColors);


                                for (int c = 0; c < nb; c++)
                                {
                                    quantizedValue[c] = GetPaletteValue(palette, candidateIndex, c, numberOfColors, oneRow, bitDepth);
                                    if (candidateIndex < numberOfDeltas)
                                    {
                                        quantizedValue[c] += predictions[c];
                                    }
                                }

                                // Native code evaluates the scale in double, then stores the weighted distance as float.
                                float colorDistance = (float)(distanceScale * ColorDistance(colorWithError, quantizedValue));
                                float indexPenalty = candidateIndex == -1 ? -124F
                                    : candidateIndex < 0 ? -2F * candidateIndex
                                    : candidateIndex < numberOfDeltas ? 250F
                                    : candidateIndex < numberOfColors ? 150F
                                    : candidateIndex < numberOfColors + LargeCubeOffset ? 70F
                                    : 256F;
                                float distance = colorDistance + indexPenalty;

                                if (distance < bestDistance)
                                {
                                    bestDistance = distance;
                                    bestIndex = candidateIndex;
                                    bestIsDelta = candidateIndex < numberOfDeltas;
                                    Span<int> previousBest = bestValue;
                                    bestValue = quantizedValue;
                                    quantizedValue = previousBest;

                                    for (int c = 0; c < nb; c++)
                                    {
                                        idealResidual[c] = (int)(colorWithError[c] - predictions[c]);
                                    }
                                }
                            }
                        }

                        index = bestIndex;
                        deltaUsed |= bestIsDelta;

                        if (!paletteIterationData.IsFinalRun)
                        {
                            for (int c = 0; c < 3; c++)
                            {
                                deltaSamples[(c * paletteIterationData.PixelCapacity) + paletteIterationData.SampleCount] = idealResidual[c];
                            }

                            deltaDistances[paletteIterationData.SampleCount++] = bestDistance;
                        }

                        for (int c = 0; c < nb; c++)
                        {
                            wpState.UpdatePredictionErrors(bestValue[c], x, y, weightedStorage, c);
                            quantizedSamples[(c * channelLength) + (y * w) + x] = bestValue[c];
                        }

                        float errorLength = 0;
                        for (int c = 0; c < nb; c++)
                        {
                            float local_error = colorWithError[c] - bestValue[c];
                            errorLength += local_error * local_error;
                        }

                        errorLength = MathF.Sqrt(errorLength);
                        float modulate = 1f;
                        int errorLimit = 38 << Math.Max(0, bitDepth - 8);
                        if (errorLength > errorLimit)
                        {
                            modulate *= errorLimit / errorLength;
                        }

                        for (int c = 0; c < nb; c++)
                        {
                            float totalError = colorWithError[c] - bestValue[c];

                            float totalAvailable = 0;
                            for (int i = 0; i < 11; i++)
                            {
                                int row = errorOffsets[2 * i];
                                int col = errorOffsets[(2 * i) + 1];
                                Span<float> rowErrors = row == 0 ? error0 : row == 1 ? error1 : error2;
                                ref float neighborError = ref rowErrors[(c * errorStride) + x + col];

                                if (float.IsNegative(neighborError) != float.IsNegative(totalError))
                                {
                                    totalAvailable += neighborError;
                                }
                            }

                            float weight = (float)(MathF.Abs(totalError) / (MathF.Abs(totalAvailable) + 1E-3D));
                            weight = MathF.Min(weight, 1.0f);

                            for (int i = 0; i < 11; ++i)
                            {
                                int row = errorOffsets[2 * i];
                                int col = errorOffsets[(2 * i) + 1];
                                Span<float> rowErrors = row == 0 ? error0 : row == 1 ? error1 : error2;
                                ref float neighborError = ref rowErrors[(c * errorStride) + x + col];

                                if (float.IsNegative(neighborError) != float.IsNegative(totalError))
                                {
                                    totalError += weight * neighborError;
                                    neighborError *= 1 - weight;
                                }
                            }

                            totalError *= modulate;
                            float remainingError = (float)((1F / 14D) * totalError);
                            error0[(c * errorStride) + x + 3] += 2 * remainingError;
                            error0[(c * errorStride) + x + 4] += remainingError;
                            error1[(c * errorStride) + x + 0] += remainingError;

                            for (int i = 0; i < 5; ++i)
                            {
                                error1[(c * errorStride) + x + i] += remainingError;
                                error2[(c * errorStride) + x + i] += remainingError;
                            }
                        }
                    }

                    if (paletteIterationData.IsFinalRun)
                    {
                        p[x] = index;
                    }
                }

                if (lossy)
                {
                    Span<float> recycled = error0;
                    error0 = error1;
                    error1 = error2;
                    error2 = recycled;
                    error2.Clear();
                }
            }

            if (!deltaUsed)
            {
                predictor = JxlPredictor.Zero;
            }

            if (indexPlane is not null)
            {
                // Publish the completed index allocation without copying. The scoped plane receives and releases the original samples.
                beginCChannel.Plane.Swap(indexPlane);
                input.MetaChannels++;

                // The first channel now contains indices. Release the other selected source channels before removing their references.
                for (int c = beginC + 1; c <= endC; c++)
                {
                    input.Channels[c].Dispose();
                }

                input.Channels.RemoveRange(beginC + 1, endC - beginC);
                input.Channels.Insert(0, newChannel);
                transferred = true;
            }

            numberOfColors -= numberOfDeltas;
        }
        finally
        {
            if (!transferred)
            {
                newChannel.Dispose();
            }
        }
    }

    /// <summary>
    /// Prepares one interleaved color row for allocation-free span lookup in palette collections.
    /// </summary>
    /// <param name="input">The source channel owner. Every selected channel has the same logical dimensions.</param>
    /// <param name="firstChannel">The first selected channel.</param>
    /// <param name="channelCount">The number of components in each complete color record.</param>
    /// <param name="y">The source row.</param>
    /// <param name="records">The reusable row of width*channelCount integer components.</param>
    private static void PrepareColorRow(JxlModularImage input, int firstChannel, int channelCount, int y, Span<int> records)
    {
        int width = input.Channels[firstChannel].Width;
        if (channelCount == 1)
        {
            input.Channels[firstChannel].GetRow(y)[..width].CopyTo(records);
            return;
        }

        if (channelCount == 3 || channelCount == 4)
        {
            ReadOnlySpan<int> plane0 = input.Channels[firstChannel].GetRow(y);
            ReadOnlySpan<int> plane1 = input.Channels[firstChannel + 1].GetRow(y);
            ReadOnlySpan<int> plane2 = input.Channels[firstChannel + 2].GetRow(y);
            ReadOnlySpan<int> plane3 = channelCount == 4 ? input.Channels[firstChannel + 3].GetRow(y) : [];
            ref int first0 = ref MemoryMarshal.GetReference(plane0);
            ref int first1 = ref MemoryMarshal.GetReference(plane1);
            ref int first2 = ref MemoryMarshal.GetReference(plane2);
            ref int first3 = ref MemoryMarshal.GetReference(plane3);
            ref float destination = ref Unsafe.As<int, float>(ref MemoryMarshal.GetReference(records));
            int x = 0;

            // Registers contain consecutive components from each integer plane. The shared shuffles store complete three- or four-component records.
            // AsSingle reinterprets bits only: no integer-to-float conversion or floating-point arithmetic changes the component values.
            if (Vector512.IsHardwareAccelerated)
            {
                nuint count = (width - x).Vector512Count<int>();
                for (nuint block = 0; block < count; block++, x += Vector512<int>.Count)
                {
                    Vector512<float> c0 = Vector512.LoadUnsafe(ref first0, (uint)x).AsSingle();
                    Vector512<float> c1 = Vector512.LoadUnsafe(ref first1, (uint)x).AsSingle();
                    Vector512<float> c2 = Vector512.LoadUnsafe(ref first2, (uint)x).AsSingle();

                    if (channelCount == 3)
                    {
                        SimdUtils.Interleave3(c0, c1, c2, ref Unsafe.Add(ref destination, x * 3));
                    }
                    else
                    {
                        Vector512<float> c3 = Vector512.LoadUnsafe(ref first3, (uint)x).AsSingle();
                        SimdUtils.Interleave4(c0, c1, c2, c3, ref Unsafe.Add(ref destination, x * 4));
                    }
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                nuint count = (width - x).Vector256Count<int>();
                for (nuint block = 0; block < count; block++, x += Vector256<int>.Count)
                {
                    Vector256<float> c0 = Vector256.LoadUnsafe(ref first0, (uint)x).AsSingle();
                    Vector256<float> c1 = Vector256.LoadUnsafe(ref first1, (uint)x).AsSingle();
                    Vector256<float> c2 = Vector256.LoadUnsafe(ref first2, (uint)x).AsSingle();

                    if (channelCount == 3)
                    {
                        SimdUtils.Interleave3(c0, c1, c2, ref Unsafe.Add(ref destination, x * 3));
                    }
                    else
                    {
                        Vector256<float> c3 = Vector256.LoadUnsafe(ref first3, (uint)x).AsSingle();
                        SimdUtils.Interleave4(c0, c1, c2, c3, ref Unsafe.Add(ref destination, x * 4));
                    }
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                nuint count = (width - x).Vector128Count<int>();
                for (nuint block = 0; block < count; block++, x += Vector128<int>.Count)
                {
                    Vector128<float> c0 = Vector128.LoadUnsafe(ref first0, (uint)x).AsSingle();
                    Vector128<float> c1 = Vector128.LoadUnsafe(ref first1, (uint)x).AsSingle();
                    Vector128<float> c2 = Vector128.LoadUnsafe(ref first2, (uint)x).AsSingle();

                    if (channelCount == 3)
                    {
                        SimdUtils.Interleave3(c0, c1, c2, ref Unsafe.Add(ref destination, x * 3));
                    }
                    else
                    {
                        Vector128<float> c3 = Vector128.LoadUnsafe(ref first3, (uint)x).AsSingle();
                        SimdUtils.Interleave4(c0, c1, c2, c3, ref Unsafe.Add(ref destination, x * 4));
                    }
                }
            }

            // Every vector stores an exact number of complete records. The tail uses identical component order and touches no row padding.
            for (; x < width; x++)
            {
                int offset = x * channelCount;
                records[offset] = plane0[x];
                records[offset + 1] = plane1[x];
                records[offset + 2] = plane2[x];
                if (channelCount == 4)
                {
                    records[offset + 3] = plane3[x];
                }
            }

            return;
        }

        // Arbitrary channel counts use the same record layout. Borrow each channel once at row preparation, outside the sample loop.
        for (int c = 0; c < channelCount; c++)
        {
            ReadOnlySpan<int> source = input.Channels[firstChannel + c].GetRow(y);
            for (int x = 0; x < width; x++)
            {
                records[(x * channelCount) + c] = source[x];
            }
        }
    }

    /// <summary>
    /// Defines value equality between immutable stored color arrays and borrowed interleaved color records.
    /// </summary>
    private sealed class PaletteColorComparer : IEqualityComparer<int[]>, IAlternateEqualityComparer<ReadOnlySpan<int>, int[]>, IComparer<int[]>
    {
        /// <inheritdoc />
        public bool Equals(int[]? left, int[]? right) => this.Equals(left.AsSpan(), right);

        /// <inheritdoc />
        public bool Equals(ReadOnlySpan<int> alternate, int[]? other) => alternate.SequenceEqual(other);

        /// <inheritdoc />
        public int GetHashCode(int[] value) => this.GetHashCode(value.AsSpan());

        /// <inheritdoc />
        public int GetHashCode(ReadOnlySpan<int> alternate)
        {
            // Both lookup forms hash the same raw Int32 words. Signed values and every extra channel participate in the key.
            HashCode hash = default;
            hash.AddBytes(MemoryMarshal.AsBytes(alternate));
            return hash.ToHashCode();
        }

        /// <inheritdoc />
        public int[] Create(ReadOnlySpan<int> alternate) => alternate.ToArray();

        /// <inheritdoc />
        public int Compare(int[]? left, int[]? right) => left.AsSpan().SequenceCompareTo(right);
    }

    /// <summary>
    /// Owns the fixed-capacity delta statistics retained between palette search passes.
    /// </summary>
    /// <remarks>
    /// Three integer planes retain RGB residuals. A separate double plane retains candidate distances.
    /// The integer allocation also holds the selected delta palette. Callers borrow spans before their sample loops.
    /// </remarks>
    internal sealed class PaletteIterationData : IDisposable
    {
        /// <summary>
        /// The native limit for selected predictive delta colors.
        /// </summary>
        public const int MaxDeltas = 128;

        private readonly IMemoryOwner<int>? deltaOwner;
        private readonly IMemoryOwner<double>? distanceOwner;

        /// <summary>
        /// Prepares retained statistics once for an optional preprocessing pass.
        /// </summary>
        /// <param name="configuration">The configuration that owns numeric storage.</param>
        /// <param name="pixelCapacity">The exact preprocessing sample count, or zero when no preprocessing pass is used.</param>
        public PaletteIterationData(Configuration configuration, int pixelCapacity)
        {
            this.PixelCapacity = pixelCapacity;
            if (pixelCapacity == 0)
            {
                return;
            }

            // Attach the second owner only after allocation succeeds. A failed constructor must return the first buffer.
            this.deltaOwner = configuration.MemoryAllocator.Allocate<int>(checked(RgbChannels * (pixelCapacity + MaxDeltas)));
            try
            {
                this.distanceOwner = configuration.MemoryAllocator.Allocate<double>(pixelCapacity);
            }
            catch
            {
                this.deltaOwner.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Gets or sets whether search writes final indices instead of collecting preprocessing statistics.
        /// </summary>
        public bool IsFinalRun { get; set; }

        /// <summary>
        /// Gets the prepared per-component residual capacity.
        /// </summary>
        public int PixelCapacity { get; }

        /// <summary>
        /// Gets or sets the number of retained preprocessing samples.
        /// </summary>
        public int SampleCount { get; set; }

        /// <summary>
        /// Gets the number of selected RGB delta entries.
        /// </summary>
        public int NumberOfFrequentDeltas { get; private set; }

        /// <summary>
        /// Gets the three planar residual arrays. Borrow once before a sample loop.
        /// </summary>
        public Span<int> DeltaSamples => this.deltaOwner is null ? [] : this.deltaOwner.GetSpan()[..(RgbChannels * this.PixelCapacity)];

        /// <summary>
        /// Gets the retained native double-precision candidate distances. Borrow once before a sample loop.
        /// </summary>
        public Span<double> DeltaDistances => this.distanceOwner is null ? [] : this.distanceOwner.GetSpan()[..this.PixelCapacity];

        /// <summary>
        /// Gets three selected-delta component planes, each with MaxDeltas entries. Only NumberOfFrequentDeltas entries are initialized.
        /// </summary>
        public Span<int> FrequentDeltas => this.deltaOwner is null
            ? []
            : this.deltaOwner.GetSpan().Slice(RgbChannels * this.PixelCapacity, RgbChannels * MaxDeltas);

        /// <inheritdoc />
        public void Dispose()
        {
            this.distanceOwner?.Dispose();
            this.deltaOwner?.Dispose();
        }

        /// <summary>
        /// Selects up to MaxDeltas residual colors using the native frequency and distance weighting.
        /// </summary>
        /// <param name="numPixels">The image sample count used to normalize frequency.</param>
        /// <param name="bitDepth">The source precision used to cluster residuals.</param>
        public void FindFrequentColorDeltas(int numPixels, int bitDepth)
        {
            Span<int> deltas = this.DeltaSamples;
            Span<double> distances = this.DeltaDistances;
            Span<int> frequent = this.FrequentDeltas;
            SortedDictionary<(int Red, int Green, int Blue), double> frequencies = [];
            int bucketSize = 3 << Math.Max(0, bitDepth - 8);

            for (int i = 0; i < this.SampleCount; i++)
            {
                (int Red, int Green, int Blue) delta = (
                    RoundInteger(deltas[i], bucketSize),
                    RoundInteger(deltas[this.PixelCapacity + i], bucketSize),
                    RoundInteger(deltas[(2 * this.PixelCapacity) + i], bucketSize));

                if ((delta.Red | delta.Green | delta.Blue) == 0)
                {
                    continue;
                }

                frequencies.TryGetValue(delta, out double frequency);
                frequencies[delta] = frequency + Math.Sqrt(Math.Sqrt(distances[i]));
            }

            float multiplier = 1F / numPixels;
            ReadOnlySpan<float> zero = [0, 0, 0];
            List<KeyValuePair<(int Red, int Green, int Blue), double>> ranked = new(frequencies.Count);

            foreach (KeyValuePair<(int Red, int Green, int Blue), double> entry in frequencies)
            {
                InlineArray3<int> color = default;
                color[0] = entry.Key.Red;
                color[1] = entry.Key.Green;
                color[2] = entry.Key.Blue;
                float distance = MathF.Sqrt(ColorDistance(zero, color)) + 1F;
                ranked.Add(new(entry.Key, entry.Value * ((double)distance * multiplier)));
            }

            ranked.Sort(static (left, right) => right.Value.CompareTo(left.Value));
            this.NumberOfFrequentDeltas = 0;

            foreach (KeyValuePair<(int Red, int Green, int Blue), double> entry in ranked)
            {
                if (this.NumberOfFrequentDeltas == MaxDeltas || entry.Value < 17)
                {
                    break;
                }

                int index = this.NumberOfFrequentDeltas++;
                frequent[index] = entry.Key.Red * bucketSize;
                frequent[MaxDeltas + index] = entry.Key.Green * bucketSize;
                frequent[(2 * MaxDeltas) + index] = entry.Key.Blue * bucketSize;
            }
        }
    }
}
