// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding.ContextPrediction;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Transforms;

/// <summary>
/// Palette/indexed decoder and encoder.
/// </summary>
internal static class JxlPalette
{
    /// <summary>
    /// Represents maximum number of colors in a palette.
    /// </summary>
    private const int MaxPaletteLookupTableSize = 1 << 16;

    /// <summary>
    /// Represents number of channels for RGB. This is 3 because RGB
    /// has three channels: R (Red), G (Green), B (Blue).
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
    /// Number of bits required to represent a small cube.
    /// </summary>
    private const int SmallCubeBits = 2; // 2 bits gives us 0..3 inclusive, so it's perfect to represent a small cube

    /// <summary>
    /// Cube of SmallCube
    /// </summary>
    private const int LargeCubeOffset = SmallCube * SmallCube * SmallCube;

    private const int ImplicitPaletteSize = LargeCubeOffset + (LargeCube * LargeCube * LargeCube);

    private const bool EncodeToHighQualityImplicitPalette = true;

    /// <summary>
    /// Minimum index required to use the implicit palette.
    /// </summary>
    private const int MinimumImplicitPaletteIndex = -((2 * 72) - 1);

    /// <summary>
    /// Backing array used to construct data for the <see cref="DefaultOffsets"/> matrix.
    /// </summary>
    private static readonly int[,] DefaultOffsetsData =
    {
        { 1, 2 },
        { 0, 3 },
        { 0, 4 },
        { 1, 1 },
        { 1, 3 },
        { 2, 2 },
        { 1, 0 },
        { 1, 4 },
        { 2, 1 },
        { 2, 3 },
        { 2, 0 },
        { 2, 4 }
    };

    /// <summary>
    /// Used by the palette encoder.
    /// </summary>
    private static readonly DenseMatrix<int> DefaultOffsets = new(DefaultOffsetsData);

    /// <summary>
    /// Static delta palette used by <see cref="GetPaletteValue(Span{int}, int, int, int, int)" />.
    /// </summary>
    private static readonly int[][] DeltaPalette =
    [
        [0, 0, 0], [4, 4, 4], [11, 0, 0],
        [0, 0, -13], [0, -12, 0], [-10, -10, -10],
        [-18, -18, -18], [-27, -27, -27], [-18, -18, 0],
        [0, 0, -32], [-32, 0, 0], [-37, -37, -37],
        [0, -32, -32], [24, 24, 45], [50, 50, 50],
        [-45, -24, -24], [-24, -45, -45], [0, -24, -24],
        [-34, -34, 0], [-24, 0, -24], [-45, -45, -24],
        [64, 64, 64], [-32, 0, -32], [0, -32, 0],
        [-32, 0, 32], [-24, -45, -24], [45, 24, 45],
        [24, -24, -45], [-45, -24, 24], [80, 80, 80],
        [64, 0, 0], [0, 0, -64], [0, -64, -64],
        [-24, -24, 45], [96, 96, 96], [64, 64, 0],
        [45, -24, -24], [34, -34, 0], [112, 112, 112],
        [24, -45, -45], [45, 45, -24], [0, -32, 32],
        [24, -24, 45], [0, 96, 96], [45, -24, 24],
        [24, -45, -24], [-24, -45, 24], [0, -64, 0],
        [96, 0, 0], [128, 128, 128], [64, 0, 64],
        [144, 144, 144], [96, 96, 0], [-36, -36, 36],
        [45, -24, -45], [45, -45, -24], [0, 0, -96],
        [0, 128, 128], [0, 96, 0], [45, 24, -45],
        [-128, 0, 0], [24, -45, 24], [-45, 24, -45],
        [64, 0, -64], [64, -64, -64], [96, 0, 96],
        [45, -45, 24], [24, 45, -45], [64, 64, -64],
        [128, 128, 0], [0, 0, -128], [-24, 45, -45]
    ];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Scale(int denominator, int value, int bitDepth)
    {
        DebugGuard.IsTrue(denominator == 4, "Denominator is defined as 4");

        return (value * ((1 << bitDepth) - 1)) >> 2;
    }

    public static int GetPaletteValue(Span<int> palette, int index, int c, int oneRow, int bitDepth)
    {
        if (index < 0)
        {
            if (c >= RgbChannels)
            {
                return 0;
            }

            index = -(index + 1);
            index %= 1 + (2 * (DeltaPalette.Length - 1));

            // JPEG XL reference uses:
            //    static constexpr int kMultiplier[] = {-1, 1};
            //    kMultiplier[index & 1]
            //
            // we use:
            //    ((index & 1) != 0 ? 1 : -1)
            //
            // The latter avoids multiplication and can make use
            // of CPU registers instead of memory access (if the JIT allows it).
            int result = DeltaPalette[(index + 1) >> 1][c] * ((index & 1) != 0 ? 1 : -1);

            if (bitDepth > 8)
            {
                result *= 1 << (bitDepth - 8);
            }

            return result;
        }
        else if (palette.Length <= index && index < palette.Length + LargeCubeOffset)
        {
            if (c >= RgbChannels)
            {
                return 0;
            }

            index -= palette.Length;
            index >>= c * SmallCubeBits;
            return Scale(SmallCube, index % SmallCube, bitDepth) + (1 << Math.Max(0, bitDepth - 3));
        }
        else if (palette.Length + LargeCubeOffset <= index)
        {
            if (c >= RgbChannels)
            {
                return 0;
            }

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

            return Scale(LargeCube - 1, index % LargeCube, bitDepth);
        }

        return palette[(c * oneRow) + index];
    }

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

        input.Channels.RemoveRange(beginC + 1, endC - beginC);
        JxlModularChannel ch = new(configuration, numberOfColors + numberOfDeltas, nb, -1, -1);
        input.Channels.Insert(0, ch);
    }

    /// <summary>
    /// Decodes palette/indexed images.
    /// </summary>
    /// <param name="configuration">Configuration for parallelism.</param>
    /// <param name="input">Input &amp; out images.</param>
    /// <param name="beginC">Offset of output channel.</param>
    /// <param name="nbColors">Number of colors.</param>
    /// <param name="nbDeltas">Number of deltas.</param>
    /// <param name="predictor">Kind of predictor mode to use.</param>
    /// <param name="weightedHeader">For weighted prediction.</param>
    /// <exception cref="InvalidOperationException">Thrown when the input for prediction is invalid.</exception>
    /// <exception cref="NotImplementedException">Thrown when there are too many channels.</exception>
    public static void InversePalette(Configuration configuration, JxlModularImage input, int beginC, int nbColors, int nbDeltas, JxlPredictor predictor, JxlModularHeader weightedHeader)
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

        for (int i = 1; i < nb; i++)
        {
            JxlModularChannel newChannel = new(configuration, w, h, channel.HorizontalShift, channel.VerticalShift);
            input.Channels.Insert(c0 + 1, newChannel);
        }

        JxlModularChannel palette = input.Channels[0];

        int oneRow = palette.Plane.PixelsPerRow;
        int oneRowImage = channel.Plane.PixelsPerRow;
        int bitDepth = Math.Min(input.BitDepth, 24);

        if (w == 0)
        {
            // Channel is empty. Don't do anything.
        }
        else if (nbDeltas == 0 && predictor == JxlPredictor.Zero)
        {
            if (nb == 1)
            {
                _ = Parallel.For(0, h, configuration.GetParallelOptions(), y =>
                {
                    Span<int> p = channel.GetRow(y);
                    Span<int> paletteData = palette.GetRow(0);

                    for (int x = 0; x < w; x++)
                    {
                        int index = Math.Clamp(p[x], 0, palette.Width - 1);

                        p[x] = GetPaletteValue(paletteData, index, 0, oneRow, bitDepth);
                    }
                });
            }
            else if (nb == 2)
            {
                _ = Parallel.For(0, h, configuration.GetParallelOptions(), y =>
                {
                    Span<int> paletteData = palette.GetRow(0);
                    Span<int> p0 = channel.GetRow(y);
                    Span<int> p1 = input.Channels[c0 + 1].GetRow(y);

                    for (int x = 0; x < w; x++)
                    {
                        int index0 = Math.Clamp(p0[x], 0, palette.Width - 1);
                        int index1 = Math.Clamp(p1[x], 0, palette.Width - 1);

                        p0[x] = GetPaletteValue(paletteData, index0, 0, oneRow, bitDepth);
                        p1[x] = GetPaletteValue(paletteData, index1, 0, oneRow, bitDepth);
                    }
                });
            }
            else if (nb == 3)
            {
                _ = Parallel.For(0, h, configuration.GetParallelOptions(), y =>
                {
                    Span<int> paletteData = palette.GetRow(0);
                    Span<int> p0 = channel.GetRow(y);
                    Span<int> p1 = input.Channels[c0 + 1].GetRow(y);
                    Span<int> p2 = input.Channels[c0 + 2].GetRow(y);

                    for (int x = 0; x < w; x++)
                    {
                        int index0 = Math.Clamp(p0[x], 0, palette.Width - 1);
                        int index1 = Math.Clamp(p1[x], 0, palette.Width - 1);
                        int index2 = Math.Clamp(p2[x], 0, palette.Width - 1);

                        p0[x] = GetPaletteValue(paletteData, index0, 0, oneRow, bitDepth);
                        p1[x] = GetPaletteValue(paletteData, index1, 0, oneRow, bitDepth);
                        p2[x] = GetPaletteValue(paletteData, index2, 0, oneRow, bitDepth);
                    }
                });
            }
            else if (nb == 4)
            {
                _ = Parallel.For(0, h, configuration.GetParallelOptions(), y =>
                {
                    Span<int> paletteData = palette.GetRow(0);
                    Span<int> p0 = channel.GetRow(y);
                    Span<int> p1 = input.Channels[c0 + 1].GetRow(y);
                    Span<int> p2 = input.Channels[c0 + 2].GetRow(y);
                    Span<int> p3 = input.Channels[c0 + 3].GetRow(y);

                    for (int x = 0; x < w; x++)
                    {
                        int index0 = Math.Clamp(p0[x], 0, palette.Width - 1);
                        int index1 = Math.Clamp(p1[x], 0, palette.Width - 1);
                        int index2 = Math.Clamp(p2[x], 0, palette.Width - 1);
                        int index3 = Math.Clamp(p3[x], 0, palette.Width - 1);

                        p0[x] = GetPaletteValue(paletteData, index0, 0, oneRow, bitDepth);
                        p1[x] = GetPaletteValue(paletteData, index1, 0, oneRow, bitDepth);
                        p2[x] = GetPaletteValue(paletteData, index2, 0, oneRow, bitDepth);
                        p3[x] = GetPaletteValue(paletteData, index3, 0, oneRow, bitDepth);
                    }
                });
            }
            else if (nb == 5)
            {
                _ = Parallel.For(0, h, configuration.GetParallelOptions(), y =>
                {
                    Span<int> paletteData = palette.GetRow(0);
                    Span<int> p0 = channel.GetRow(y);
                    Span<int> p1 = input.Channels[c0 + 1].GetRow(y);
                    Span<int> p2 = input.Channels[c0 + 2].GetRow(y);
                    Span<int> p3 = input.Channels[c0 + 3].GetRow(y);
                    Span<int> p4 = input.Channels[c0 + 4].GetRow(y);

                    for (int x = 0; x < w; x++)
                    {
                        int index0 = Math.Clamp(p0[x], 0, palette.Width - 1);
                        int index1 = Math.Clamp(p1[x], 0, palette.Width - 1);
                        int index2 = Math.Clamp(p2[x], 0, palette.Width - 1);
                        int index3 = Math.Clamp(p3[x], 0, palette.Width - 1);
                        int index4 = Math.Clamp(p4[x], 0, palette.Width - 1);

                        p0[x] = GetPaletteValue(paletteData, index0, 0, oneRow, bitDepth);
                        p1[x] = GetPaletteValue(paletteData, index1, 0, oneRow, bitDepth);
                        p2[x] = GetPaletteValue(paletteData, index2, 0, oneRow, bitDepth);
                        p3[x] = GetPaletteValue(paletteData, index3, 0, oneRow, bitDepth);
                        p4[x] = GetPaletteValue(paletteData, index4, 0, oneRow, bitDepth);
                    }
                });
            }
            else
            {
                throw new NotImplementedException($"Too many channels for palette compressed images: {nb}");
            }
        }
        else
        {
            JxlImageI plane = input.Channels[c0].Plane;
            JxlImageI indices = new(configuration, plane.XSize, plane.YSize);
            input.Channels[c0].Plane = indices;

            if (predictor == JxlPredictor.Weighted)
            {
                _ = Parallel.For(0, nb, configuration.GetParallelOptions(), c =>
                {
                    JxlModularChannel channel = input.Channels[c0 + c];
                    JxlModularState wpState = new(weightedHeader, channel.Width);
                    Span<int> paletteData = palette.GetRow(0);

                    for (int y = 0; y < channel.Height; y++)
                    {
                        Span<int> p = channel.GetRow(y);
                        Span<int> idx = indices.GetRow(y);

                        for (int x = 0; x < channel.Width; x++)
                        {
                            int index = idx[x];
                            int value = 0;
                            int paletteEntry = GetPaletteValue(paletteData, index, c, oneRow, bitDepth);
                            JxlPredictionResult pred = JxlContextPrediction.PredictTreeNoWeightedPrediction(channel.Width, p[x..], oneRowImage, x, y, predictor, wpState);

                            if (index < nbDeltas)
                            {
                                value = pred.Guess + paletteEntry;
                            }
                            else
                            {
                                value = paletteEntry;
                            }

                            p[x] = value;
                            wpState.UpdatePredictionErrors(p[x], x, y, channel.Width);
                        }
                    }
                });
            }
            else
            {
                _ = Parallel.For(0, nb, configuration.GetParallelOptions(), c =>
                {
                    JxlModularChannel channel = input.Channels[c0 + c];
                    Span<int> paletteData = palette.GetRow(0);

                    for (int y = 0; y < channel.Height; y++)
                    {
                        Span<int> p = channel.GetRow(y);
                        Span<int> idx = indices.GetRow(y);

                        for (int x = 0; x < channel.Width; x++)
                        {
                            int index = idx[x];
                            int value = 0;
                            int paletteEntry = GetPaletteValue(paletteData, index, c, oneRow, bitDepth);

                            if (index < nbDeltas)
                            {
                                JxlPredictionResult pred = JxlContextPrediction.PredictNoTreeNoWeightedPrediction(channel.Width, p[x..], oneRowImage, x, y, predictor);
                                value = pred.Guess + paletteEntry;
                            }
                            else
                            {
                                value = paletteEntry;
                            }

                            p[x] = value;
                        }
                    }
                });
            }
        }

        if (c0 >= input.MetaChannels)
        {
            input.MetaChannels--;
        }
        else
        {
            if (input.MetaChannels >= 2 - nb)
            {
                throw new InvalidOperationException("Too many meta channels");
            }

            input.MetaChannels -= 2 - nb;

            if (beginC + nb - 1 < input.MetaChannels)
            {
                throw new InvalidOperationException("Too many meta channels");
            }

            input.Channels.RemoveAt(0);
        }
    }

    private static float ColorDistance(Span<float> a, Span<int> b)
    {
        InlineArray3<int> array = default;
        array[0] = b[0];
        array[1] = b[1];
        array[2] = b[2];
        return ColorDistance(a, array);
    }

    private static float ColorDistance(Span<float> a, InlineArray3<int> b)
    {
        if (a.Length != 3)
        {
            throw new InvalidOperationException("Length mismatch");
        }

        float distance = 0;
        float ave3 = 0;

        if (a.Length >= 3)
        {
            ave3 = ((a[0] + b[0]) + (a[1] + b[1]) + (a[2] + b[2])) * (1.21f / 3.0f);
        }

        float sumA = 0;
        float sumB = 0;

        for (int c = 0; c < a.Length; c++)
        {
            float diff = a[c] - b[c];
            float weight = c == 0 ? 3f : c == 1 ? 5f : 2f;

            if (c < 3 && (a[c] + b[c] >= ave3))
            {
                weight += c == 2 ? 1.12f : 1.15f;

                if (c == 2 && ((a[2] + b[2]) < 1.22f * ave3))
                {
                    weight -= 0.5f;
                }
            }

            distance += diff * diff * weight * weight;
            int sumWeight = c == 0 ? 3 : c == 1 ? 5 : 1;

            sumA += a[c] * sumWeight;
            sumB += b[c] * sumWeight;
        }

        distance *= 4;
        float sumDiff = sumA - sumB;
        distance += sumDiff * sumDiff;
        return distance;
    }

    private static int QuantizeColorToImplicitPaletteIndex(Span<int> color, int paletteSize, int bitDepth, bool highQuality)
    {
        int index = 1;
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

            return index + (paletteSize * LargeCubeOffset);
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int RoundInteger(int value, int div)
    {
        if (value < 0)
        {
            return (-value + (div / 2)) / div;
        }
        else
        {
            return (value + (div / 2)) / div;
        }
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
        PaletteIterationData paletteIterationData = new();
        int originalNumberOfColors = numberOfColors;
        int originalNumberOfDeltas = numberOfDeltas;

        if (lossy && input.BitDepth >= 8)
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

        paletteIterationData.IsFinalRun = false;
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

        // TODO: if this assert below triggers, increase nb to 6 and update
        // the stack allocation for the 'tmp' variable below
        // so the size is big enough.
        DebugGuard.MustBeLessThanOrEqualTo(nb, 5, nameof(nb));

        JxlModularChannel beginCChannel = input.Channels[beginC];
        int w = beginCChannel.Width;
        int h = beginCChannel.Height;

        if (input.BitDepth >= 32)
        {
            throw new InvalidOperationException("Bit depth is too large");
        }

        if (!lossy && numberOfColors < 2)
        {
            throw new InvalidOperationException("Lossless palette transform needs at least 3 channels");
        }

        int idx = 0;

        if (!lossy && nb == 1)
        {
            if (numberOfColors == 0)
            {
                throw new InvalidOperationException("No colors");
            }

            JxlTransform.ComputeMinMax(beginCChannel, out int minValue, out int maxValue);
            int lookupTableSize = maxValue - minValue + 1;

            if (lookupTableSize < MaxPaletteLookupTableSize)
            {
                HashSet<int> chPalette = [];

                for (int y = 0; y < h; y++)
                {
                    Span<int> p = beginCChannel.GetRow(y);

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

                // Don't dispose. The channel is stored into the input.
                JxlModularChannel modularChannel = new(configuration, idx, 1, -1, -1);

                numberOfColors = idx;
                idx = 0;

                Span<int> ppalette = modularChannel.GetRow(0);

                foreach (int p in chPalette)
                {
                    ppalette[idx++] = p;
                }

                for (int y = 0; y < h; y++)
                {
                    Span<int> p = beginCChannel.GetRow(y);

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
                input.Channels.Insert(0, modularChannel);

                return;
            }

            Span<int> lookup = stackalloc int[lookupTableSize];
            idx = 0;

            for (int y = 0; y < h; y++)
            {
                Span<int> p = beginCChannel.GetRow(y);
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

            // Don't dispose. The channel is stored into the input.
            JxlModularChannel channel = new(configuration, idx, 1, -1, -1);
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
                Span<int> p = beginCChannel.GetRow(y);

                for (int x = 0; x < w; x++)
                {
                    p[x] = lookup[p[x] - minValue];
                }
            }

            predictor = JxlPredictor.Zero;
            input.MetaChannels++;
            input.Channels.Insert(0, channel);

            return;
        }

        JxlModularImage quantizedInput = new(configuration, 1, 1, -1, -1);

        if (lossy)
        {
            quantizedInput.Dispose();
            quantizedInput = new(configuration, w, h, input.BitDepth, nb);

            for (int c = 0; c < nb; c++)
            {
                if (!JxlImageOperations.CopyImage(input.Channels[beginC + c].Plane, quantizedInput.Channels[c].Plane))
                {
                    throw new InvalidOperationException("Copying failed");
                }
            }
        }

        numberOfDeltas = 0;
        bool deltaUsed = false;
        List<int[]> candidatePalette = [];
        List<int[]> candidatePaletteImageOrder = [];
        Dictionary<int[], int> inversePalette = [];

        // Don't use stackalloc for color so we can store it as a
        // dictionary member in colorFrequencyMap (see below)
        int[] color = new int[nb];
        Span<int> colorSpan = color.AsSpan();
        Span<float> colorWithError = stackalloc float[nb];

        if (lossy)
        {
            paletteIterationData.FindFrequentColorDeltas(w * h, input.BitDepth);
            numberOfDeltas = paletteIterationData.FrequentDeltas[0].Count;
            Dictionary<int[], int> colorFrequencyMap = [];

            DenseMatrix<int> offsets = new(4, 2);

            for (int y = 1; y + 1 < h; y++)
            {
                for (int x = 1; x + 1 < w; x++)
                {
                    for (int c = 0; c < nb; c++)
                    {
                        colorSpan[c] = input.Channels[beginC + c].GetRow(y)[x];
                    }

                    // Defaults
                    offsets[0, 0] = 1;
                    offsets[0, 0] = 0;
                    offsets[1, 0] = -1;
                    offsets[1, 1] = 0;
                    offsets[2, 0] = 0;
                    offsets[2, 1] = 1;
                    offsets[3, 0] = 0;
                    offsets[3, 1] = -1;

                    bool makesCross = true;

                    for (int i = 0; i < 4 && makesCross; ++i)
                    {
                        int dx = offsets[i, 0];
                        int dy = offsets[i, 1];

                        for (int c = 0; c < nb && makesCross; c++)
                        {
                            if (input.Channels[beginC + c].GetRow(y + dy)[x + dx] != colorSpan[c])
                            {
                                makesCross = false;
                            }
                        }
                    }

                    if (makesCross)
                    {
                        colorFrequencyMap[color]++;
                    }
                }
            }

            const float imageFraction = 0.01f;
            int colorFrequencyLowerBound = 5 + (int)(input.Height * input.Width * imageFraction);

            foreach (KeyValuePair<int[], int> colorFreq in colorFrequencyMap)
            {
                if (colorFreq.Value > colorFrequencyLowerBound)
                {
                    candidatePalette.Insert(0, colorFreq.Key);
                    candidatePaletteImageOrder.Add(colorFreq.Key);
                }
            }
        }

        Dictionary<int[], bool> implicitColor = [];
        int[][] implicitColors = new int[ImplicitPaletteSize][];

        for (int k = 0; k < ImplicitPaletteSize; k++)
        {
            for (int i = 0; i < nb; i++)
            {
                color[i] = GetPaletteValue([], k, i, 0, input.BitDepth);
            }

            implicitColor[color] = true;
            implicitColors[k] = color;
        }

        int implicitColorsUsed = 0;
        Dictionary<int[], int> colorFreqMap = [];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (lossy && candidatePalette.Count >= numberOfColors)
                {
                    break;
                }

                for (int c = 0; c < nb; c++)
                {
                    colorSpan[c] = input.Channels[beginC + c].GetRow(y)[x];
                }

                const bool new_color = candidatePalette.Add(color).second;
                if (new_color)
                {
                    if (implicitColor[color])
                    {
                        implicitColorsUsed++;
                    }
                    else
                    {
                        candidatePaletteImageOrder.Add(color);
                        if (candidatePaletteImageOrder.Count > numberOfColors)
                        {
                            throw new InvalidOperationException("Too many colors for palette/indexed");
                        }
                    }
                }

                colorFreqMap[color]++;
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
            color = implicitColors[k];
            if (colorFreqMap[color] > 10)
            {
                numberOfColors++;
                candidatePaletteImageOrder.Add(color);
            }
        }

        for (int k = 0; k < ImplicitPaletteSize; k++)
        {
            color = implicitColors[k];
            inversePalette[color] = numberOfColors + k;
        }

        // Don't dispose this.
        JxlModularChannel newChannel = new(configuration, numberOfColors, nb, -1, -1);
        Span<int> palette = newChannel.GetRow(0);
        int oneRow = newChannel.Plane.PixelsPerRow;
        int oneRowImage = beginCChannel.Plane.PixelsPerRow;
        int bitDepth = Math.Min(input.BitDepth, 24); // max. 24 bits, cannot be greater

        if (lossy)
        {
            for (int i = 0; i < numberOfDeltas; i++)
            {
                for (int c = 0; c < 3; c++)
                {
                    palette[(c * oneRow) + i] = paletteIterationData.FrequentDeltas[c][i];
                }
            }
        }

        float frequencyThreshold = 4f;
        int clr = 0;

        if (ordered && nb >= 3)
        {
            candidatePaletteImageOrder.Sort((ap, bp) =>
            {
                float ay = (0.299f * ap[0]) + (0.587f * ap[1]) + (0.114f * ap[2]) + 0.1f;

                if (ap.Length > 3)
                {
                    ay *= 1f + ap[3];
                }

                float by = (0.299f * bp[0]) + (0.587f * bp[1]) + (0.114f * bp[2]) + 0.1f;

                if (bp.Length > 3)
                {
                    by *= 1f + bp[3];
                }

                ay = colorFreqMap[ap] > frequencyThreshold ? -ay : ay;
                by = colorFreqMap[bp] > frequencyThreshold ? -by : by;

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

        List<JxlModularState> wpStates = [];

        for (int c = 0; c < nb; c++)
        {
            wpStates.Add(new JxlModularState(wpHeader, w));
        }

        InlineArray3<DenseMatrix<float>> errorRow = default;

        if (lossy)
        {
            errorRow[0] = new(nb, w + 4);
            errorRow[1] = new(nb, w + 4);
            errorRow[2] = new(nb, w + 4);
        }

        Span<int> tmp = stackalloc int[32]; // Power of 2
        Span<int> bestValue = tmp.Slice(0 * nb, nb);
        Span<int> idealResidual = tmp.Slice(1 * nb, nb);
        Span<int> quantizedValue = tmp.Slice(2 * nb, nb);
        Span<int> predictions = tmp.Slice(30 * nb, nb);

        // This is a temporary buffer, values are copied here.
        // It is so we can swap spans. Since spans are just a view
        // of memory, using CopyTo as a swap means we need a
        // separate buffer like this for the swapping value.
        Span<float> tempBuffer = stackalloc float[w + 4];

        for (int y = 0; y < h; y++)
        {
            for (int c = 0; c < nb; c++)
            {
                p_in[c] = input.channel[beginC + c].Row(y);
                if (lossy)
                    p_quant[c] = quantized_input.channel[c].Row(y);
            }

            Span<int> p = beginCChannel.GetRow(y);

            for (int x = 0; x < w; x++)
            {
                int index;
                if (!lossy)
                {
                    for (int c = 0; c < nb; c++)
                    {
                        color[c] = p_in[c][x];
                    }

                    index = inversePalette[color];
                }
                else
                {
                    int best_index = 0;
                    bool best_is_delta = false;
                    float best_distance = float.PositiveInfinity;

                    tmp.Clear();

                    foreach (double diffusion_multiplier in (Span<double>)[0.55, 0.75])
                    {
                        for (int c = 0; c < nb; c++)
                        {
                            colorWithError[c] =
                                p_in[c][x] + ((paletteIterationData.IsFinalRun ? 1 : 0) *
                                                 diffusion_multiplier * errorRow[0][c, x + 2]);
                            color[c] = (int)Math.Clamp(MathF.Round(colorWithError[c]), 0, (1 << input.BitDepth) - 1);
                        }

                        for (int c = 0; c < nb; c++)
                        {
                            predictions[c] = PredictTreeNoWeightedPrediction(w, p_quant[c] + x, oneRowImage, x, y, predictor, wpStates[c]).Guess;
                        }

                        void TryIndex(int index, Span<int> predictions, Span<int> idealResidual, Span<float> colorWithError, ref Span<int> bestValue, ref Span<int> quantizedValue, Span<int> palette, ref int numberOfColors, ref int numberOfDeltas)
                        {
                            for (int c = 0; c < nb; c++)
                            {
                                quantizedValue[c] = GetPaletteValue(palette, index, c, oneRow, bitDepth);
                                if (index < numberOfDeltas)
                                {
                                    quantizedValue[c] += predictions[c];
                                }
                            }

                            float color_distance = 32.0f / (1 << Math.Max(0, 2 * (bitDepth - 8))) * ColorDistance(colorWithError, quantizedValue);

                            float indexPenalty = 0;
                            if (index == -1)
                            {
                                indexPenalty = -124;
                            }
                            else if (index < 0)
                            {
                                indexPenalty = -2 * index;
                            }
                            else if (index < numberOfDeltas)
                            {
                                indexPenalty = 250;
                            }
                            else if (index < numberOfColors)
                            {
                                indexPenalty = 150;
                            }
                            else if (index < numberOfColors + LargeCubeOffset)
                            {
                                indexPenalty = 70;
                            }
                            else
                            {
                                indexPenalty = 256;
                            }

                            float distance = color_distance + indexPenalty;
                            if (distance < best_distance)
                            {
                                best_distance = distance;
                                best_index = index;
                                best_is_delta = index < numberOfDeltas;

                                RuntimeUtility.Swap(ref bestValue, ref quantizedValue);

                                for (int c = 0; c < nb; c++)
                                {
                                    idealResidual[c] = (int)(colorWithError[c] - predictions[c]);
                                }
                            }
                        }

                        for (index = MinimumImplicitPaletteIndex; index < numberOfColors; index++)
                        {
                            TryIndex(index);
                        }

                        TryIndex(QuantizeColorToImplicitPaletteIndex(color, numberOfColors, bitDepth, false));

                        if (EncodeToHighQualityImplicitPalette)
                        {
                            TryIndex(QuantizeColorToImplicitPaletteIndex(color, numberOfColors, bitDepth, true));
                        }
                    }

                    index = best_index;
                    deltaUsed |= best_is_delta;

                    if (!paletteIterationData.IsFinalRun)
                    {
                        for (int c = 0; c < 3; c++)
                        {
                            paletteIterationData.Deltas[c].Add(idealResidual[c]);
                        }

                        paletteIterationData.DeltaDistances.Add(best_distance);
                    }

                    for (int c = 0; c < nb; c++)
                    {
                        wpStates[c].UpdatePredictionErrors(bestValue[c], x, y, w);
                        p_quant[c][x] = bestValue[c];
                    }

                    float len_error = 0;
                    for (int c = 0; c < nb; c++)
                    {
                        float local_error = colorWithError[c] - bestValue[c];
                        len_error += local_error * local_error;
                    }

                    len_error = MathF.Sqrt(len_error);
                    float modulate = 1f;
                    long len_limit = 38 << Math.Max(0, bitDepth - 8);
                    if (len_error > len_limit)
                    {
                        modulate *= len_limit / len_error;
                    }

                    DenseMatrix<int> offsets = new(12, 2);

                    for (int c = 0; c < nb; c++)
                    {
                        float total_error = colorWithError[c] - bestValue[c];

                        DefaultOffsets.Data.AsSpan().CopyTo(offsets.Data);

                        float total_available = 0;
                        for (int i = 0; i < 11; i++)
                        {
                            int row = offsets[i, 0];
                            int col = offsets[i, 1];

                            if (Math.Sign(errorRow[row][c, x + col]) != Math.Sign(total_error))
                            {
                                total_available += errorRow[row][c, x + col];
                            }
                        }

                        float weight = MathF.Abs(total_error) / (MathF.Abs(total_available) + 1e-3f);
                        weight = MathF.Min(weight, 1.0f);

                        for (int i = 0; i < 11; ++i)
                        {
                            int row = offsets[i, 0];
                            int col = offsets[i, 1];

                            if (Math.Sign(errorRow[row][c, x + col]) != Math.Sign(total_error))
                            {
                                total_error += weight * errorRow[row][c, x + col];
                                errorRow[row][c, x + col] *= 1 - weight;
                            }
                        }

                        total_error *= modulate;
                        float remaining_error = (1.0f / 14f) * total_error;
                        errorRow[0][c, x + 3] += 2 * remaining_error;
                        errorRow[0][c, x + 4] += remaining_error;
                        errorRow[1][c, x + 0] += remaining_error;

                        for (int i = 0; i < 5; ++i)
                        {
                            errorRow[1][c, x + i] += remaining_error;
                            errorRow[2][c, x + i] += remaining_error;
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
                for (int c = 0; c < nb; c++)
                {
                    // Variables for swapping
                    Span<float> pos0 = errorRow[0].Data.AsSpan(c, w + 4);
                    Span<float> pos1 = errorRow[1].Data.AsSpan(c, w + 4);
                    Span<float> pos2 = errorRow[2].Data.AsSpan(c, w + 4);

                    // we need to swap:
                    //     error_row[0][c].swap(error_row[1][c]);
                    pos0.CopyTo(tempBuffer);
                    pos1.CopyTo(pos0);
                    tempBuffer.CopyTo(pos1);

                    // swap old1, old2
                    pos1.CopyTo(tempBuffer);
                    pos2.CopyTo(pos1);

                    pos2.Clear();
                }
            }
        }

        if (!deltaUsed)
        {
            predictor = JxlPredictor.Zero;
        }

        if (paletteIterationData.IsFinalRun)
        {
            input.MetaChannels++;
            input.Channels.RemoveRange(beginC + 1, endC - beginC);
            input.Channels.Insert(0, newChannel);
        }

        numberOfColors -= numberOfDeltas;
    }

    /// <summary>
    /// For palette encoding.
    /// </summary>
    internal sealed class PaletteIterationData
    {
        /// <summary>
        /// Maximum number of deltas.
        /// </summary>
        private const int MaxDeltas = 128;

        private InlineArray3<List<int>> deltas;

        public bool IsFinalRun { get; set; }

        public InlineArray3<List<int>> Deltas
        {
            get => this.deltas;
            set => this.deltas = value;
        }

        public List<double> DeltaDistances { get; set; } = [];

        public List<int>[] FrequentDeltas { get; set; } = new List<int>[3];

        public void FindFrequentColorDeltas(int numPixels, int bitDepth)
        {
            Dictionary<InlineArray3<int>, double> deltaFrequencyMap = [];
            int bucketSize = 3 << Math.Max(0, bitDepth - 3);
            for (int i = 0; i < this.Deltas[0].Count; i++)
            {
                InlineArray3<int> delta = default;
                delta[0] = RoundInteger(this.Deltas[0][i], bucketSize);
                delta[1] = RoundInteger(this.Deltas[1][i], bucketSize);
                delta[2] = RoundInteger(this.Deltas[2][i], bucketSize);

                // Condition equivalent to delta[0] == 0 && delta[1] == 0 && delta[2] == 0
                if ((delta[0] | delta[1] | delta[2]) == 0)
                {
                    continue;
                }

                deltaFrequencyMap[delta] += Math.Sqrt(Math.Sqrt(this.DeltaDistances[i]));
            }

            float deltaDistanceMultiplier = 1f / numPixels;
            Span<float> allZero = [0, 0, 0];

            foreach (KeyValuePair<InlineArray3<int>, double> deltaFrequency in deltaFrequencyMap)
            {
                float deltaDistance = MathF.Sqrt(ColorDistance(allZero, deltaFrequency.Key)) + 1f;
                double second = deltaFrequency.Value * deltaDistance * deltaDistanceMultiplier;
                deltaFrequencyMap[deltaFrequency.Key] = second;
            }

            Dictionary<InlineArray3<int>, double> sorted = deltaFrequencyMap.ToDictionary(
                entry => entry.Key,
                entry => entry.Value);

            IOrderedEnumerable<KeyValuePair<InlineArray3<int>, double>> sortedEnumerator = sorted.OrderBy(
                x => x.Value);

            foreach (KeyValuePair<InlineArray3<int>, double> deltaFrequency in sortedEnumerator)
            {
                if (this.FrequentDeltas[0].Count >= MaxDeltas)
                {
                    break;
                }

                if (deltaFrequency.Value < 17)
                {
                    break;
                }

                for (int c = 0; c < 3; c++)
                {
                    this.FrequentDeltas[c].Add(deltaFrequency.Key[c] * bucketSize);
                }
            }
        }
    }
}
