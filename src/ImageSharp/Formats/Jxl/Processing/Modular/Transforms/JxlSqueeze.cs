// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

#pragma warning disable IDE0057 // Use range operator

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Transforms;

/// <summary>
/// Implements the <em>squeeze transform</em>.
/// </summary>
/// <remarks>
/// The squeeze transform in JXL is a reversible
/// wavelet-like decomposition used in the modular mode
/// to reduce redundancy and improve compression,
/// especially for structured or synthetic images.
/// It works by hierarchically splitting channesl
/// into lower-resolution representations plus
/// residuals, giving us multi-resolution coding while
/// remaining lossless.
/// </remarks>
internal static class JxlSqueeze
{
    private const int MaxFirstPreviewSize = 8;

    /// <summary>
    /// Computes the average of two integers.
    /// </summary>
    /// <remarks>
    /// This method is specific to the Squeeze transform.
    /// It is not a generic average method.
    /// </remarks>
    /// <param name="x">First integer</param>
    /// <param name="y">Second integer</param>
    /// <returns>The average of x, y.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Average(int x, int y) => (x + y + ((x > y) ? 1 : 0)) >> 1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int SmoothTendency(int b, int a, int n)
    {
        int diff = 0;
        if (b >= a && a >= n)
        {
            diff = ((4 * b) - (3 * n) - a + 6) / 12;

            if (diff - (diff & 1) > 2 * (b - a))
            {
                diff = (2 * (b - a)) + 1;
            }

            if (diff + (diff & 1) > 2 * (a - n))
            {
                diff = 2 * (a - n);
            }
        }
        else if (b <= a && a <= n)
        {
            diff = ((4 * b) - (3 * n) - a - 6) / 12;

            if (diff + (diff & 1) < 2 * (b - a))
            {
                diff = (2 * (b - a)) - 1;
            }

            if (diff - (diff & 1) < 2 * (a - n))
            {
                diff = 2 * (a - n);
            }
        }

        return diff;
    }

    // The function operates on 256-bit fixed size vectors,
    // 8 elements at a time. It should still work even on CPUs
    // without 256-bit vector support (the JIT will translate
    // these into 128-bit halves, or scalar without SIMD support).
    //
    // The FastUnsqueeze method CAN operate on vectors below
    // 256-bit, but not above. It's better to simply use Vector256
    // rather than duplicate everything. Vector<T> may be a problem
    // as its number of elements can be greater than 8 which is too
    // much for this method.
    [MethodImpl(InliningOptions.HotPath)] // Called on an entire image
    private static void FastUnsqueeze(Span<int> pResidual, Span<int> pAvg, Span<int> pNAvg, Span<int> pPout, Span<int> pOut, Span<int> pNOut)
    {
        Vector256<int> oneThird = Vector256.Create(0x55555556);

        ref int pAvgRef = ref MemoryMarshal.GetReference(pAvg);
        ref int pNAvgRef = ref MemoryMarshal.GetReference(pNAvg);
        ref int pPoutReference = ref MemoryMarshal.GetReference(pPout);
        ref int pResidualRef = ref MemoryMarshal.GetReference(pResidual);
        ref int pOutRef = ref MemoryMarshal.GetReference(pOut);
        ref int pNOutRef = ref MemoryMarshal.GetReference(pNOut);

        Vector256<int> avg = Vector256.LoadUnsafe(ref pAvgRef);
        Vector256<int> nextAvg = Vector256.LoadUnsafe(ref pNAvgRef);
        Vector256<int> top = Vector256.LoadUnsafe(ref pPoutReference);

        Vector256<int> ba = top - avg;
        Vector256<int> an = avg - nextAvg;
        Vector256<int> nonmono = ba ^ an;
        Vector256<int> absba = Vector256.Abs(ba);
        Vector256<int> absan = Vector256.Abs(an);
        Vector256<int> absbn = Vector256.Abs(top - nextAvg);

        Vector256<long> a3eh = Vector256_.MultiplyEven(absba, oneThird);
        Vector256<long> a3oh = Vector256_.MultiplyOdd(absba, oneThird);

        Vector256<int> a3 = BitConverter.IsLittleEndian
            ? Vector256_.InterleaveOdd(a3eh.AsInt32(), a3oh.AsInt32())
            : Vector256_.InterleaveEven(a3eh.AsInt32(), a3oh.AsInt32());

        a3 += absbn + Vector256.Create(2);

        Vector256<int> absdiff = a3 >> 2;

        Vector256<int> skipdiff = Vector256_.NotEqual(ba, Vector256<int>.Zero);
        skipdiff &= Vector256_.NotEqual(an, Vector256<int>.Zero);
        skipdiff &= Vector256.LessThan(nonmono, Vector256<int>.Zero);

        Vector256<int> absBa2 = (absba << 1) + (absdiff & Vector256<int>.One);

        absdiff = Vector256.ConditionalSelect(
            Vector256.GreaterThan(absdiff, absBa2),
            (absba << 1) + Vector256<int>.One,
            absdiff);

        Vector256<int> absan2 = absan << 1;
        absdiff = Vector256.ConditionalSelect(
            Vector256.GreaterThan(absdiff + (absdiff & Vector256<int>.One), absan2),
            absan2,
            absdiff);

        Vector256<int> diff1 = Vector256.ConditionalSelect(
            Vector256.LessThan(top, nextAvg),
            -absdiff,
            absdiff);

        Vector256<int> tendency = diff1 & ~skipdiff;
        Vector256<int> diffMinusTendency = Vector256.LoadUnsafe(ref pResidualRef);
        Vector256<int> diff = diffMinusTendency + tendency;
        Vector256<int> output = avg + (diff + (diff << 31));

        output.StoreUnsafe(ref pOutRef);
        (output - diff).StoreUnsafe(ref pNOutRef);
    }

    public static void InverseHorizontalSqueeze(Configuration configuration, JxlModularImage input, int c, int rc)
    {
        // Channel offsets should not overflow.
        DebugGuard.MustBeLessThan(c, input.Channels.Count, nameof(c));
        DebugGuard.MustBeLessThan(rc, input.Channels.Count, nameof(c));

        JxlModularChannel inputChannel = input.Channels[c];
        JxlModularChannel inputResidualChannel = input.Channels[rc];

        if (inputChannel.Width != JxlMath.DivCeil(inputChannel.Width + inputResidualChannel.Width, 2))
        {
            throw new InvalidOperationException("Invalid width");
        }

        if (inputChannel.Height != inputResidualChannel.Height)
        {
            throw new InvalidOperationException("Height of the input channel must be equal to the height of the residual channel");
        }

        if (inputResidualChannel.Width == 0)
        {
            input.Channels[c].HorizontalShift--;
            return;
        }

        // Do not dispose.
        JxlModularChannel outputChannel = new(
            configuration,
            inputChannel.Width + inputResidualChannel.Width,
            inputChannel.Height,
            inputChannel.HorizontalShift - 1,
            inputChannel.VerticalShift);

        if (inputResidualChannel.Height == 0)
        {
            input.Channels[c] = outputChannel;
            return;
        }

        // The number of rows a single parallel iteration computes
        // is stored here.
        const int rowsPerThread = 8;

        // rowsPerThread * 9, aligned to the power of 2.
        const int rowsPerThreadMul9Alignment = 128;

        // rowsPerThread * 8, aligned to the power of 2.
        const int rowsPerThreadMul8Alignment = 64;

        _ = Parallel.For(0, JxlMath.DivCeil(inputChannel.Height, rowsPerThread), configuration.GetParallelOptions(), idx =>
        {
            int y0 = idx * rowsPerThread;
            int rows = Math.Min(rowsPerThread, inputChannel.Height - y0);
            int x = 0;

            int onerow_in = inputChannel.Plane.PixelsPerRow;
            int onerow_inr = inputResidualChannel.Plane.PixelsPerRow;
            int onerow_out = outputChannel.Plane.PixelsPerRow;
            Span<int> pResidual = inputResidualChannel.GetRow(y0);
            Span<int> pAverage = inputChannel.GetRow(y0);
            Span<int> pOut = outputChannel.GetRow(y0);
            ref int pOutRef = ref MemoryMarshal.GetReference(pOut);

            Span<int> bpAvg = stackalloc int[rowsPerThreadMul9Alignment].Slice(0, rowsPerThread * 9);
            Span<int> bpResidual = stackalloc int[rowsPerThreadMul8Alignment].Slice(0, rowsPerThread * 8);
            Span<int> bpOutEven = stackalloc int[rowsPerThreadMul8Alignment].Slice(0, rowsPerThread * 8);
            Span<int> bpOutOdd = stackalloc int[rowsPerThreadMul8Alignment].Slice(0, rowsPerThread * 8);
            Span<int> bpOutEvenT = stackalloc int[rowsPerThreadMul8Alignment].Slice(0, rowsPerThread * 8);
            Span<int> bpOutOddT = stackalloc int[rowsPerThreadMul8Alignment].Slice(0, rowsPerThread * 8);

            ref int bpOutEvenTRef = ref MemoryMarshal.GetReference(bpOutEvenT);
            ref int bpOutOddTRef = ref MemoryMarshal.GetReference(bpOutOddT);

            int n = Vector256<int>.Count;

            if (inputResidualChannel.Width > 16 && rows == rowsPerThread)
            {
                for (; x < inputResidualChannel.Width - 9; x += 8)
                {
                    JxlSimdUtils.Transpose8x8Block(pResidual[x..], bpResidual, onerow_inr);
                    JxlSimdUtils.Transpose8x8Block(pAverage[x..], bpAvg, onerow_in);

                    for (int y = 0; y < rowsPerThread; y++)
                    {
                        bpAvg[64 + y] = pAverage[x + 8 + (onerow_in * y)];
                    }

                    for (int i = 0; i < 8; i++)
                    {
                        // i * 8
                        int i8 = i << 3;

                        FastUnsqueeze(
                            bpResidual[i8..],
                            bpAvg[i8..],
                            bpAvg[(8 * (i + 1))..],
                            (x + i > 0) ? bpOutOdd[(8 * ((x + i - 1) & 7))..] : bpAvg[i8..],
                            bpOutEven[i8..],
                            bpOutOdd[i8..]);
                    }

                    JxlSimdUtils.Transpose8x8Block(bpOutEven, bpOutEvenT, 8);
                    JxlSimdUtils.Transpose8x8Block(bpOutOdd, bpOutOddT, 8);

                    for (int y = 0; y < rowsPerThread; y++)
                    {
                        // y * 8
                        int y8 = y << 3;

                        for (int i = 0; i < rowsPerThread; i += n)
                        {
                            int offset = y8 + i;

                            Vector256<int> even = Vector256.LoadUnsafe(ref Unsafe.Add(ref bpOutEvenTRef, offset));
                            Vector256<int> odd = Vector256.LoadUnsafe(ref Unsafe.Add(ref bpOutOddTRef, offset));

                            JxlSimdUtils.StoreInterleaved(
                                even,
                                odd,
                                ref Unsafe.Add(ref pOutRef, ((x + i) << 1) + (onerow_out * y)));
                        }
                    }
                }
            }

            for (int y = 0; y < rows; y++)
            {
                UnsqueezeRow(y0 + y, x);
            }
        });

        input.Channels[c] = outputChannel;

        void UnsqueezeRow(int y, int x0)
        {
            Span<int> residual = inputResidualChannel.GetRow(y);
            Span<int> average = inputChannel.GetRow(y);
            Span<int> output = outputChannel.GetRow(y);
            int inputChannelWidth = inputChannel.Width;
            int outputChannelWidth = outputChannel.Width;

            for (int x = x0; x < inputResidualChannel.Width; x++)
            {
                int xLsh1 = x << 1; // Prevents left shifting three times. Saves on CPU cycles.

                int diffMinusTendency = residual[x];
                int avg = average[x];
                int nextAverage = x + 1 < inputChannelWidth ? average[x + 1] : avg;

                int left = x > 0 ? output[xLsh1 - 1] : avg;
                int tendency = SmoothTendency(left, avg, nextAverage);
                int diff = diffMinusTendency + tendency;

                int a = avg + (diff / 2);
                output[xLsh1] = a;

                int b = a - diff;
                output[xLsh1 + 1] = b;
            }

            if ((outputChannelWidth & 1) > 0)
            {
                output[outputChannelWidth - 1] = average[inputChannelWidth - 1];
            }
        }
    }

    public static void InverseVerticalSqueeze(Configuration configuration, JxlModularImage input, int c, int rc)
    {
        // Channel offsets should not overflow.
        DebugGuard.MustBeLessThan(c, input.Channels.Count, nameof(c));
        DebugGuard.MustBeLessThan(rc, input.Channels.Count, nameof(c));

        JxlModularChannel inputChannel = input.Channels[c];
        JxlModularChannel inputResidualChannel = input.Channels[rc];

        if (inputChannel.Height != JxlMath.DivCeil(inputChannel.Height + inputResidualChannel.Height, 2))
        {
            throw new InvalidOperationException("Invalid height");
        }

        if (inputChannel.Width != inputResidualChannel.Width)
        {
            throw new InvalidOperationException("Width of the input channel must be equal to the width of the residual channel");
        }

        if (inputResidualChannel.Height == 0)
        {
            input.Channels[c].VerticalShift--;
            return;
        }

        // Do not dispose.
        JxlModularChannel outputChannel = new(
            configuration,
            inputChannel.Width,
            inputChannel.Height + inputResidualChannel.Height,
            inputChannel.HorizontalShift,
            inputChannel.VerticalShift - 1);

        if (inputResidualChannel.Width == 0)
        {
            input.Channels[c] = outputChannel;
            return;
        }

        // The number of columns a single parallel iteration computes
        // is stored here.
        const int colsPerThread = 8;

        _ = Parallel.For(0, JxlMath.DivCeil(inputChannel.Width, colsPerThread), configuration.GetParallelOptions(), idx =>
        {
            int x0 = idx * colsPerThread;
            int x1 = Math.Min((idx + 1) * colsPerThread, inputChannel.Width);
            int w = x1 - x0;

            for (int y = 0; y < inputResidualChannel.Height; y++)
            {
                int yLsh1 = y << 1;

                Span<int> pResidual = inputResidualChannel.GetRow(y)[x0..];
                Span<int> pAverage = inputChannel.GetRow(y)[x0..];
                Span<int> pNAvg = inputChannel.GetRow(y + 1 < inputChannel.Height ? y + 1 : y)[x0..];
                Span<int> pOut = outputChannel.GetRow(yLsh1)[x0..];
                Span<int> pNOut = outputChannel.GetRow(yLsh1 + 1)[x0..];
                Span<int> pPOut = y > 0 ? outputChannel.GetRow(yLsh1 - 1)[x0..] : pNAvg;
                int x = 0;

                for (; x + 7 < w; x += 8)
                {
                    FastUnsqueeze(
                        pResidual[x..],
                        pAverage[x..],
                        pNAvg[x..],
                        pPOut[x..],
                        pOut[x..],
                        pNOut[x..]);
                }

                // Remainder
                for (; x < w; x++)
                {
                    int avg = pNAvg[x];
                    int nextAvg = pNAvg[x];
                    int top = pPOut[x];
                    int tendency = SmoothTendency(top, avg, nextAvg);
                    int diffMinusTendency = pResidual[x];
                    int diff = diffMinusTendency + tendency;
                    int output = avg + (diff >> 1);
                    pOut[x] = output;
                    pNOut[x] = output - diff;
                }
            }
        });

        if ((outputChannel.Height & 1) > 0)
        {
            int y = inputChannel.Height - 1;

            Span<int> pAverage = inputChannel.GetRow(y);
            Span<int> pOutput = outputChannel.GetRow(y << 1);

            for (int x = 0; x < inputChannel.Width; x++)
            {
                pOutput[x] = pAverage[x];
            }
        }

        input.Channels[c] = outputChannel;
    }

    public static void InverseSqueeze(Configuration configuration, JxlModularImage input, Span<JxlSqueezeParameters> parameters)
    {
        int totalNumberOfChannels = input.Channels.Count;

        for (int i = parameters.Length - 1; i >= 0; i--)
        {
            ref JxlSqueezeParameters parameter = ref parameters[i];

            CheckMetaSqueezeParameters(parameter, totalNumberOfChannels);

            bool horizontal = parameter.Horizontal;
            bool inPlace = parameter.InPlace;
            int beginC = parameter.BeginC;
            int endC = parameter.BeginC + parameter.NumC - 1;

            int offset = inPlace
                ? endC + 1
                : totalNumberOfChannels + beginC + endC - 1;

            if (beginC < input.MetaChannels)
            {
                if (input.MetaChannels <= parameter.NumC)
                {
                    throw new InvalidOperationException("Not enough meta channels");
                }

                input.MetaChannels -= parameter.NumC;
            }

            for (int c = beginC; c <= endC; c++)
            {
                int rc = offset + c - beginC;

                if (rc >= totalNumberOfChannels)
                {
                    throw new InvalidOperationException("Residual channel offset out of bounds");
                }

                JxlModularChannel channelC = input.Channels[c]; // Input channel
                JxlModularChannel channelRC = input.Channels[rc]; // Residual channel

                if (channelC.Width < channelRC.Width || channelC.Height < channelRC.Height)
                {
                    throw new InvalidOperationException("Input channel width or height does not match residual channel width/height");
                }

                if (horizontal)
                {
                    InverseHorizontalSqueeze(configuration, input, c, rc);
                }
                else
                {
                    InverseVerticalSqueeze(configuration, input, c, rc);
                }
            }
        }
    }

    public static void DefaultSqueezeParameters(List<JxlSqueezeParameters> squeezeParameters, JxlModularImage image)
    {
        int numberOfChannels = image.Channels.Count - image.MetaChannels;
        squeezeParameters.Clear();

        JxlModularChannel numMetaChannelsChannel = image.Channels[image.MetaChannels];
        int w = numMetaChannelsChannel.Width;
        int h = numMetaChannelsChannel.Height;
        bool wide = w > h;

        JxlModularChannel nextNumMetaChannelsChannel = image.Channels[image.MetaChannels + 1];

        if (numberOfChannels > 2 && nextNumMetaChannelsChannel.Width == w && nextNumMetaChannelsChannel.Height == h)
        {
            JxlSqueezeParameters parameters = new()
            {
                Horizontal = true,
                InPlace = false,
                BeginC = image.MetaChannels + 1,
                NumC = 2
            };

            squeezeParameters.Add(parameters);
            parameters.Horizontal = false;
            squeezeParameters.Add(parameters);
        }

        JxlSqueezeParameters newParameters = new()
        {
            BeginC = image.MetaChannels,
            NumC = numberOfChannels,
            InPlace = true
        };

        if (!wide)
        {
            if (h > MaxFirstPreviewSize)
            {
                newParameters.Horizontal = false;
                squeezeParameters.Add(newParameters);
                h = (h + 1) >> 1;
            }
        }

        while (w > MaxFirstPreviewSize || h > MaxFirstPreviewSize)
        {
            if (w > MaxFirstPreviewSize)
            {
                newParameters.Horizontal = true;
                squeezeParameters.Add(newParameters);
                w = (w + 1) >> 1;
            }

            if (w > MaxFirstPreviewSize)
            {
                newParameters.Horizontal = false;
                squeezeParameters.Add(newParameters);
                h = (h + 1) >> 1;
            }
        }
    }

    private static void CheckMetaSqueezeParameters(in JxlSqueezeParameters parameter, int numChannels)
    {
        int c1 = parameter.BeginC;
        int c2 = parameter.BeginC + parameter.NumC - 1;

        if ((uint)c1 >= numChannels ||
            (uint)c2 >= numChannels ||
            c2 < c1)
        {
            throw new InvalidOperationException("Invalid channel range");
        }
    }

    public static void MetaSqueeze(Configuration configuration, JxlModularImage image, List<JxlSqueezeParameters> parameters)
    {
        if (parameters.Count == 0)
        {
            DefaultSqueezeParameters(parameters, image);
        }

        foreach (JxlSqueezeParameters parameter in parameters)
        {
            CheckMetaSqueezeParameters(parameter, image.Channels.Count);

            bool horizontal = parameter.Horizontal;
            bool inPlace = parameter.InPlace;
            int beginC = parameter.BeginC;
            int endC = parameter.BeginC + parameter.NumC - 1;

            if (beginC < image.MetaChannels)
            {
                if (endC >= image.MetaChannels)
                {
                    throw new InvalidOperationException("Invalid squeeze: mix of meta and nonmeta channels");
                }

                if (!inPlace)
                {
                    throw new InvalidOperationException("Invalid squeeze: meta channels require in-place residuals");
                }

                image.MetaChannels += parameter.NumC;
            }

            int offset = inPlace
                ? endC + 1
                : image.Channels.Count;

            for (int c = beginC; c <= endC; c++)
            {
                JxlModularChannel channel = image.Channels[c];

                if (channel.Height > 30 || channel.VerticalShift > 30)
                {
                    throw new InvalidOperationException("Too many squeezes: shift > 30");
                }

                int w = channel.Width;
                int h = channel.Height;

                if ((w & h) == 0) // either w, or h, is 0
                {
                    throw new InvalidOperationException("Squeezing empty channel");
                }

                if (horizontal)
                {
                    channel.Width = (w + 1) >> 1;

                    if (channel.HorizontalShift >= 0)
                    {
                        channel.HorizontalShift++;
                    }

                    w -= (w + 1) >> 1;
                }
                else
                {
                    channel.HorizontalShift = (h + 1) >> 1;

                    if (channel.VerticalShift >= 0)
                    {
                        channel.VerticalShift++;
                    }

                    h -= (h + 1) >> 1;
                }

                channel.Shrink(configuration);

                JxlModularChannel placeholder = new(configuration, w, h, channel.HorizontalShift, channel.VerticalShift)
                {
                    Component = channel.Component
                };

                image.Channels.Insert(offset + (c - beginC), placeholder);
            }
        }
    }

    public static void ForwardHorizontalSqueeze(Configuration configuration, JxlModularImage input, int c, int rc)
    {
        JxlModularChannel inputChannel = input.Channels[c];

        // Do not dispose these.
        JxlModularChannel outputChannel = new(configuration, (inputChannel.Width + 1) >> 1, inputChannel.Height, inputChannel.HorizontalShift + 1, inputChannel.VerticalShift);
        JxlModularChannel outputChannelResidual = new(configuration, inputChannel.Width - outputChannel.Width, outputChannel.Height, inputChannel.HorizontalShift + 1, inputChannel.VerticalShift);

        outputChannel.Component = inputChannel.Component;
        outputChannelResidual.Component = inputChannel.Component;

        for (int y = 0; y < outputChannel.Height; y++)
        {
            Span<int> pIn = inputChannel.GetRow(y);
            Span<int> pOut = outputChannel.GetRow(y);
            Span<int> pRes = outputChannelResidual.GetRow(y);

            for (int x = 0; x < outputChannelResidual.Width; x++)
            {
                int x2 = x << 1; // x * 2

                int a = pIn[x2];
                int b = pIn[x2 + 1];
                int avg = Average(a, b);
                pOut[x] = avg;
                int diff = a - b;
                int nextAvg = avg;

                if (x + 1 < outputChannelResidual.Width)
                {
                    int c2 = pIn[x2 + 2]; // actually C, but 1. variable 'c' already defined 2. names should be camelCase
                    int d = pIn[x2 + 3];

                    nextAvg = Average(c2, d);
                }
                else if ((inputChannel.Width & 1) != 0)
                {
                    nextAvg = pIn[x2 + 2];
                }

                int left = x > 0 ? pIn[x2 - 1] : avg;
                int tendency = SmoothTendency(left, avg, nextAvg);

                pRes[x] = diff - tendency;
            }

            if ((inputChannel.Width & 1) != 0)
            {
                int x = outputChannel.Width - 1;
                pOut[x] = pIn[x * 2];
            }
        }

        input.Channels[c] = outputChannel;
        input.Channels.Insert(rc, outputChannelResidual);
    }

    public static void ForwardVerticalSqueeze(Configuration configuration, JxlModularImage input, int c, int rc)
    {
        JxlModularChannel inputChannel = input.Channels[c];

        // Do not dispose these.
        JxlModularChannel outputChannel = new(configuration, inputChannel.Width, (inputChannel.Height + 1) >> 1, inputChannel.HorizontalShift, inputChannel.VerticalShift + 1);
        JxlModularChannel outputResidualChannel = new(configuration, inputChannel.Width, inputChannel.Height - outputChannel.Height, inputChannel.HorizontalShift, inputChannel.VerticalShift + 1);

        outputChannel.Component = inputChannel.Component;
        outputResidualChannel.Component = inputChannel.Component;

        int oneRowInput = inputChannel.Plane.PixelsPerRow;

        for (int y = 0; y < outputChannel.Height; y++)
        {
            Span<int> pIn = inputChannel.GetRow(y * 2);
            Span<int> pOut = outputChannel.GetRow(y);
            Span<int> pResidual = outputResidualChannel.GetRow(y);

            for (int x = 0; x < outputChannel.Width; x++)
            {
                int a = pIn[x];
                int b = pIn[x + oneRowInput];
                int avg = Average(a, b);
                pOut[x] = avg;
                int diff = a - b;
                int nextAvg = avg;

                if (y + 1 < outputResidualChannel.Height)
                {
                    int c2 = pIn[x + (2 * oneRowInput)]; // actually C, but 1. variable 'c' already defined 2. names should be camelCase
                    int d = pIn[x + (3 * oneRowInput)];
                    nextAvg = Average(c2, d);
                }
                else if ((inputChannel.Height & 1) != 0)
                {
                    nextAvg = pIn[x + (2 * oneRowInput)];
                }

                int top = y > 0 ? pIn[x - oneRowInput] : avg;
                int tendency = SmoothTendency(top, avg, nextAvg);

                pResidual[x] = diff - tendency;
            }
        }

        if ((inputChannel.Height & 1) != 0)
        {
            int y = outputChannel.Height - 1;

            Span<int> pIn = inputChannel.GetRow(y * 2);
            Span<int> pOut = outputChannel.GetRow(y);

            for (int x = 0; x < outputChannel.Width; x++)
            {
                pOut[x] = pIn[x];
            }
        }

        input.Channels[c] = outputChannel;
        input.Channels.Insert(rc, outputResidualChannel);
    }

    public static void ForwardSqueeze(Configuration configuration, JxlModularImage input, List<JxlSqueezeParameters> parameters)
    {
        if (parameters.Count == 0)
        {
            DefaultSqueezeParameters(parameters, input);

            if (parameters.Count == 0)
            {
                // If there's nothing to do, don't squeeze.
                return;
            }
        }

        foreach (JxlSqueezeParameters parameter in parameters)
        {
            CheckMetaSqueezeParameters(parameter, input.Channels.Count);

            bool horizontal = parameter.Horizontal;
            bool inPlace = parameter.InPlace;
            int beginC = parameter.BeginC;
            int endC = parameter.BeginC + parameter.NumC - 1;

            int offset = inPlace
                ? endC + 1
                : input.Channels.Count;

            for (int c = beginC; c <= endC; c++)
            {
                if (horizontal)
                {
                    ForwardHorizontalSqueeze(configuration, input, c, offset + c - beginC);
                }
                else
                {
                    ForwardVerticalSqueeze(configuration, input, c, offset + c - beginC);
                }
            }
        }
    }
}
