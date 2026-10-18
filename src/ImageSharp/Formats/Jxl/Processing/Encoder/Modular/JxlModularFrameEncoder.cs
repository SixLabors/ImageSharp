// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding.ContextPrediction;
using StaticPropertyRange = System.Runtime.CompilerServices.InlineArray2<System.Runtime.CompilerServices.InlineArray2<int>>;
using Tree = System.Collections.Generic.List<SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding.JxlPropertyDecisionNode>;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Modular;

internal static class JxlModularFrameEncoder
{
    public const float SqueezeQualityFactor = 0.35f;

    public const float SqueezeLumaFactor = 1.1f;

    public const float SqueezeQualityFactorXyb = 4.0f;

    public const float SqueezeQualityFactorY = 1.5f;

    /// <summary>
    /// Quantization table for the XYB Squeeze transform.
    /// </summary>
    public static readonly float[][] SqueezeXybQTable =
    [
        [
            163.84f, 81.92f, 40.96f, 20.48f, 10.24f, 5.12f, 2.56f, 1.28f, 0.64f, 0.32f, 0.16f,
            0.08f, 0.04f, 0.02f, 0.01f, 0.005f
        ],  // Y
        [
            1024, 512, 256, 128, 64, 32, 16, 8, 4, 2, 1, 0.5f, 0.5f, 0.5f, 0.5f,
            0.5f
        ],  // X
        [
            2048, 1024, 512, 256, 128, 64, 32, 16, 8, 4, 2, 1, 0.5f, 0.5f, 0.5f,
            0.5f
        ],  // B-Y
    ];

    /// <summary>
    /// Gets the quantization table for the Luma Squeeze transform.
    /// </summary>
    public static ReadOnlySpan<float> SqueezeLumaQTable =>
    [
        163.84f, 81.92f, 40.96f, 20.48f, 10.24f, 5.12f,
        2.56f,   1.28f,  0.64f,  0.32f,  0.16f,  0.08f,
        0.04f,   0.02f,  0.01f,  0.005f
    ];

    /// <summary>
    /// Gets the quantization table for the Chroma Squeeze transform.
    /// </summary>
    public static ReadOnlySpan<float> SqueezeChromaQTable =>
    [
        1024, 512, 256, 128, 64, 32, 16, 8, 4, 2, 1,
        0.5f, 0.5f, 0.5f, 0.5f, 0.5f
    ];

    private static bool MergeTrees(List<Tree> trees, IReadOnlyList<int> treeSplits, int begin, int end, Tree tree)
    {
        if (trees.Count + 1 != treeSplits.Count)
        {
            return false;
        }

        if (end <= begin || end > trees.Count)
        {
            return false;
        }

        if (end == begin + 1)
        {
            // Insert the tree, adding the appropriate offset to all child nodes.
            // This will make the leaf IDs wrong, but subsequent roundtripping will fix them.
            int sz = tree.Count;
            tree.AddRange(trees[begin]);

            for (int i = sz; i < tree.Count; i++)
            {
                JxlPropertyDecisionNode decisionNode = tree[i];
                tree[i] = decisionNode with
                {
                    LeftChild = decisionNode.LeftChild + sz,
                    RightChild = decisionNode.RightChild + sz
                };
            }

            return true;
        }

        int mid = (begin + end) / 2;
        int splitval = treeSplits[mid] - 1;
        int cur = tree.Count;

        tree.Add(new JxlPropertyDecisionNode(1, splitval, 0, 0, JxlPredictor.Zero, 0, 1));

        tree[cur] = tree[cur] with
        {
            LeftChild = tree.Count
        };

        if (!MergeTrees(trees, treeSplits, mid, end, tree))
        {
            return false;
        }

        tree[cur] = tree[cur] with
        {
            RightChild = tree.Count
        };

        if (!MergeTrees(trees, treeSplits, begin, mid, tree))
        {
            return false;
        }

        return true;
    }

    private static bool FloatToInt(ReadOnlySpan<float> rowIn, Span<int> rowOut, int xsize, uint bits, uint expBits, bool fp, double dfactor)
    {
        if (bits > sizeof(int) * 8)
        {
            return false;
        }

        if (!fp)
        {
            if (bits > 22)
            {
                for (int x = 0; x < xsize; x++)
                {
                    rowOut[x] = (int)((rowIn[x] * dfactor) + (rowIn[x] < 0 ? -0.5 : 0.5));
                }
            }
            else
            {
                float factor = (float)dfactor;

                for (int x = 0; x < xsize; x++)
                {
                    rowOut[x] = (int)((rowIn[x] * factor) + (rowIn[x] < 0 ? -0.5f : 0.5f));
                }
            }

            return true;
        }

        if (bits == 32)
        {
            if (expBits != 8)
            {
                return false;
            }

            MemoryMarshal.Cast<float, byte>(rowIn[..xsize]).CopyTo(MemoryMarshal.Cast<int, byte>(rowOut[..xsize]));
            return true;
        }

        if (bits == 0)
        {
            return false;
        }

        int expBias = (1 << ((int)expBits - 1)) - 1;
        int maxExp = (1 << (int)expBits) - 1;
        uint sign = 1u << ((int)bits - 1);
        int mantBits = (int)bits - (int)expBits - 1;
        int mantShift = 23 - mantBits;

        for (int x = 0; x < xsize; x++)
        {
            uint f = BitConverter.SingleToUInt32Bits(rowIn[x]);
            int signBit = (int)(f >> 31);
            f &= 0x7FFFFFFF;

            if (f == 0)
            {
                rowOut[x] = signBit != 0 ? (int)sign : 0;
                continue;
            }

            int exp = (int)(f >> 23) - 127;
            int mantissa = (int)(f & 0x007FFFFF);

            if (exp == 128)
            {
                f = signBit != 0 ? sign : 0;
                f |= ((1u << (int)expBits) - 1) << mantBits;
                f |= (uint)(mantissa >> mantShift);
                rowOut[x] = (int)f;
                continue;
            }

            exp += expBias;

            if (exp <= 0)
            {
                mantissa |= 0x00800000;

                if (exp < -mantBits)
                {
                    return false;
                }

                mantissa >>= 1 - exp;
                exp = 0;
            }

            if (exp >= maxExp)
            {
                return false;
            }

            if ((mantissa & ((1 << mantShift) - 1)) != 0)
            {
                return false;
            }

            mantissa >>= mantShift;

            f = signBit != 0 ? sign : 0;
            f |= (uint)(exp << mantBits);
            f |= (uint)mantissa;

            rowOut[x] = (int)f;
        }

        return true;
    }
}
