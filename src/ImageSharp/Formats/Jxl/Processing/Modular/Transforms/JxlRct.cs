// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Transforms;

/// <summary>
/// Reversible Color Transform (RCT)
/// </summary>
internal static class JxlRct
{
    /// <summary>
    /// Performs Inverse Reversible Color Transform (RCT) on one row.
    /// </summary>
    /// <param name="transformType">
    /// The kind of RCT.
    /// </param>
    /// <param name="in0">Input Y</param>
    /// <param name="in1">Input Co</param>
    /// <param name="in2">Input Cg</param>
    /// <param name="out0">Output R</param>
    /// <param name="out1">Output G</param>
    /// <param name="out2">Output B</param>
    private static void InverseRctRow(int transformType, Span<int> in0, Span<int> in1, Span<int> in2, Span<int> out0, Span<int> out1, Span<int> out2)
    {
        DebugGuard.MustBeBetweenOrEqualTo(transformType, 0, 6, nameof(transformType));

        int width = in0.Length; // All input & output channels have equal widths

        int second = transformType >> 1;
        int third = transformType & 1;

        int n = Vector<int>.Count;

        if (transformType == 6)
        {
            // SIMD-aligned loop
            int x;
            for (x = 0; x + n - 1 < width; x += n)
            {
                Vector<int> y = new(in0[x..]);
                Vector<int> co = new(in1[x..]);
                Vector<int> cg = new(in2[x..]);
                y -= cg >> 1;
                Vector<int> g = cg + y;
                y -= co >> 1;
                Vector<int> r = y + co;
                r.CopyTo(out0[x..]);
                g.CopyTo(out1[x..]);
                y.CopyTo(out2[x..]);
            }

            // Remainder (SIMD-unaligned)
            for (; x < width; x++)
            {
                int y = in0[x];
                int co = in1[x];
                int cg = in2[x];
                int tmp = y + -(cg >> 1);
                int g = cg + tmp;
                int b = tmp + -(co >> 1);
                int r = b + co;
                out0[x] = r;
                out1[x] = g;
                out2[x] = b;
            }
        }
        else
        {
            // SIMD-aligned loop
            int x;
            for (x = 0; x + n - 1 < width; x += n)
            {
                // Add a Vec suffix because the variables
                // 'second' and 'third' are already defined.
                // Though 'first' isn't, it's still suffixed
                // for consistency.
                Vector<int> firstVec = new(in0[x..]);
                Vector<int> secondVec = new(in1[x..]);
                Vector<int> thirdVec = new(in2[x..]);

                if (third > 0)
                {
                    thirdVec += firstVec;
                }

                if (second == 1)
                {
                    secondVec += firstVec;
                }
                else if (second == 2)
                {
                    secondVec += (firstVec + thirdVec) >> 1;
                }

                firstVec.CopyTo(out0[x..]);
                secondVec.CopyTo(out1[x..]);
                thirdVec.CopyTo(out2[x..]);
            }

            // Remainder (SIMD-unaligned)
            for (; x < width; x++)
            {
                int firstCoeff = in0[x];
                int secondCoeff = in1[x];
                int thirdCoeff = in2[x];

                if (third > 0)
                {
                    thirdCoeff += firstCoeff;
                }

                if (second == 1)
                {
                    secondCoeff += firstCoeff;
                }
                else if (second == 2)
                {
                    secondCoeff += (firstCoeff + thirdCoeff) >> 1;
                }

                out0[x] = firstCoeff;
                out1[x] = secondCoeff;
                out2[x] = thirdCoeff;
            }
        }
    }

    /// <summary>
    /// Performs Inverse Reversible Color Transform (RCT) on an entire
    /// image.
    /// </summary>
    /// <param name="configuration">
    /// The configuration is used to access maximum degree of parallelism.
    /// </param>
    /// <param name="img">
    /// Image to compute inverse RCT.
    /// </param>
    /// <param name="beginC">
    /// Offset of the color channel for Y, Co, Cg.
    /// </param>
    /// <param name="rctType">
    /// Type of Reversible Color Transform
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Invoked when RCT/permutation is invalid.
    /// </exception>
    public static void InverseRct(Configuration configuration, JxlModularImage img, int beginC, int rctType)
    {
        JxlTransform.CheckEqualChannels(img, beginC, beginC + 2);

        int m = beginC;
        JxlModularChannel c0 = img.Channels[m + 0];
        int w = c0.Width;
        int h = c0.Height;

        if (rctType == 0)
        {
            // No-op
            return;
        }

        int permutation = rctType / 7;

        if (permutation >= 7)
        {
            throw new InvalidOperationException("Permutation must be <= 6");
        }

        int custom = rctType % 7;

        if (custom == 0)
        {
            // Permute-only.
            JxlModularChannel ch0 = img.Channels[m];
            JxlModularChannel ch1 = img.Channels[m + 1];
            JxlModularChannel ch2 = img.Channels[m + 2];
            img.Channels[m + (permutation % 3)] = ch0;
            img.Channels[m + ((permutation + 1 + (permutation / 3)) % 3)] = ch1;
            img.Channels[m + ((permutation + 2 - (permutation / 3)) % 3)] = ch2;
            return;
        }

        _ = Parallel.For(0, h, configuration.GetParallelOptions(), y =>
        {
            Span<int> in0 = img.Channels[m].GetRow(y);
            Span<int> in1 = img.Channels[m + 1].GetRow(y);
            Span<int> in2 = img.Channels[m + 2].GetRow(y);

            Span<int> out0 = img.Channels[m + (permutation % 3)].GetRow(y);
            Span<int> out1 = img.Channels[m + ((permutation + 1 + (permutation / 3)) % 3)].GetRow(y);
            Span<int> out2 = img.Channels[m + ((permutation + 2 - (permutation / 3)) % 3)].GetRow(y);

            InverseRctRow(custom, in0, in1, in2, out0, out1, out2);
        });
    }

    /// <summary>
    /// Performs Reversible Color Transform (RCT) on one row.
    /// </summary>
    /// <param name="transform">
    /// The type of RCT transform method.
    /// </param>
    /// <param name="in0">R</param>
    /// <param name="in1">G</param>
    /// <param name="in2">B</param>
    /// <param name="out0">Y</param>
    /// <param name="out1">Co</param>
    /// <param name="out2">Cg</param>
    private static void ForwardRctRow(int transform, Span<int> in0, Span<int> in1, Span<int> in2, Span<int> out0, Span<int> out1, Span<int> out2)
    {
        DebugGuard.MustBeLessThanOrEqualTo(transform, 6, nameof(transform));

        int second = transform >> 1;
        int third = transform & 1;

        int width = in0.Length; // All input & output channels have equal widths

        if (!Vector.IsHardwareAccelerated || Vector<int>.Count == 1)
        {
            // No SIMD support. Use scalar.
            if (transform == 6)
            {
                for (int x = 0; x < width; x++)
                {
                    int r = in0[x];
                    int g = in1[x];
                    int b = in2[x];
                    int o1 = r - b;
                    int tmp = b + (o1 >> 1);
                    int o2 = g - tmp;
                    out0[x] = tmp + (o2 >> 1);
                    out1[x] = o1;
                    out2[x] = o2;
                }
            }
            else
            {
                for (int x = 0; x < width; x++)
                {
                    int firstCoeff = in0[x];
                    int secondCoeff = in1[x];
                    int thirdCoeff = in2[x];

                    if (second == 1)
                    {
                        secondCoeff -= firstCoeff;
                    }
                    else if (second == 2)
                    {
                        secondCoeff -= (firstCoeff + thirdCoeff) >> 1;
                    }

                    if (third != 0)
                    {
                        thirdCoeff -= firstCoeff;
                    }

                    out0[x] = firstCoeff;
                    out1[x] = secondCoeff;
                    out2[x] = thirdCoeff;
                }
            }
        }
        else
        {
            // Have SIMD support
            int lanes = Vector<int>.Count;

            if (transform == 6)
            {
                for (int x = 0; x < width; x += lanes)
                {
                    Vector<int> r = new(in0[x..]);
                    Vector<int> g = new(in1[x..]);
                    Vector<int> b = new(in2[x..]);
                    Vector<int> o1 = r - b;
                    Vector<int> tmp = b + (o1 >> 1);
                    Vector<int> o2 = g - tmp;
                    Vector<int> o0 = tmp + (o2 >> 1);
                    o0.CopyTo(out0[x..]);
                    o1.CopyTo(out1[x..]);
                    o2.CopyTo(out2[x..]);
                }
            }
            else
            {
                for (int x = 0; x < width; x += lanes)
                {
                    Vector<int> i0 = new(in0[x..]);
                    Vector<int> i1 = new(in1[x..]);
                    Vector<int> i2 = new(in2[x..]);
                    Vector<int> o1 = i1;

                    // TODO: duplicate loops for second == 1, second == 2
                    // and otherwise? We should reduce the number of branches in
                    // loops.
                    if (second == 1)
                    {
                        o1 -= i0;
                    }
                    else if (second == 2)
                    {
                        o1 -= (i0 + i2) >> 1;
                    }

                    Vector<int> o2 = i2;

                    if (third != 0)
                    {
                        o2 -= i0;
                    }

                    i0.CopyTo(out0[x..]);
                    o1.CopyTo(out1[x..]);
                    o2.CopyTo(out2[x..]);
                }
            }
        }
    }

    private static void RctPermute(InlineArray3<JxlModularChannel> input, int permutation, ref InlineArray3<JxlModularChannel> output)
    {
        output[0] = input[permutation % 3];
        output[1] = input[(permutation + 1 + (permutation / 3)) % 3];
        output[2] = input[(permutation + 2 - (permutation / 3)) % 3];
    }

    /// <summary>
    /// Performs Forward Reversible Color Transform. (Internal method)
    /// </summary>
    /// <param name="configuration">
    /// Configuration for parallelism.
    /// </param>
    /// <param name="input">
    /// Input channels.
    /// </param>
    /// <param name="output">
    /// Output channels.
    /// </param>
    /// <param name="rctType">
    /// Kind of RCT.
    /// </param>
    private static void ForwardRctCore(Configuration configuration, InlineArray3<JxlModularChannel> input, InlineArray3<JxlModularChannel> output, int rctType)
    {
        int permutation = rctType / 7;
        int transform = rctType % 7;

        InlineArray3<JxlModularChannel> inp = default;
        RctPermute(input, permutation, ref inp);

        int width = output[0].Width;
        int height = output[0].Height;

        _ = Parallel.For(0, height, configuration.GetParallelOptions(), y =>
        {
            Span<int> i0 = inp[0].GetRow(y);
            Span<int> i1 = inp[1].GetRow(y);
            Span<int> i2 = inp[2].GetRow(y);

            Span<int> o0 = output[0].GetRow(y);
            Span<int> o1 = output[1].GetRow(y);
            Span<int> o2 = output[2].GetRow(y);

            ForwardRctRow(transform, i0, i1, i2, o0, o1, o2);
        });
    }

    /// <summary>
    /// Performs forward Reversible Color Transform (RCT).
    /// </summary>
    /// <param name="configuration">
    /// Configuration for parallelism.
    /// </param>
    /// <param name="image">
    /// Image to perform Reversible Color Transform.
    /// </param>
    /// <param name="beginC">
    /// Offset of the color channel.
    /// </param>
    /// <param name="rctType">
    /// Kind of RCT.
    /// </param>
    public static void ForwardRct(Configuration configuration, JxlModularImage image, int beginC, int rctType)
    {
        JxlTransform.CheckEqualChannels(image, beginC, beginC + 2);

        if (rctType == 0)
        {
            // No-op
            return;
        }

        InlineArray3<JxlModularChannel> channels = default;
        channels[0] = image.Channels[beginC];
        channels[1] = image.Channels[beginC + 1];
        channels[2] = image.Channels[beginC + 2];

        return ForwardRct(configuration, new JxlModularImage(channels[0], channels[1], channels[2]), beginC, rctType);
    }
}
