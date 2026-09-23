// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats.Jxl.Fields;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Ans;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Modular;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Transforms;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Quantization;
using Tree = System.Collections.Generic.List<SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding.JxlPropertyDecisionNode>;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Modular;

internal sealed class JxlModularFrameDecoder(Configuration configuration, JxlFrameDimensions fd)
{
    private const float AlmostZero = 1e-8f;

    private readonly Configuration configuration = configuration;
    private readonly JxlModularImage fullImage = new();
    private readonly List<JxlTransform> globalTransform = [];
    private readonly JxlFrameDimensions frameDimensions = fd;
    private bool doColor;
    private bool haveSomething;
    private bool useFullImage = true;
    private bool allSameShift;
    private readonly Tree tree = [];
    private JxlAnsCode code = new();
    private readonly List<byte> contextMap = [];
    private JxlGroupHeader groupHeader = new();

    public Configuration Configuration => this.configuration;

    public bool UsesFullImage => this.useFullImage;

    public bool ContainsDc => this.haveSomething;

    private static void MultiplySum(int xSize, ReadOnlySpan<int> rowIn, ReadOnlySpan<int> rowInY, float factor, Span<float> rowOut)
    {
        Vector<float> factorV = new(factor);

        int lanes = Vector<float>.Count;

        for (int x = 0; x < xSize; x += lanes)
        {
            Vector<float> inV = Vector.ConvertToSingle(Vector.Create(rowIn[x..]));
            inV += Vector.ConvertToSingle(Vector.Create(rowInY[x..]));

            Vector<float> outV = inV * factorV;
            outV.CopyTo(rowOut[x..]);
        }
    }

    private static void RgbFromSingle(int xSize, ReadOnlySpan<int> rowIn, float factor, Span<float> outR, Span<float> outG, Span<float> outB)
    {
        Vector<float> factorV = new(factor);

        int lanes = Vector<float>.Count;

        for (int x = 0; x < xSize; x += lanes)
        {
            Vector<float> inV = Vector.ConvertToSingle(Vector.Create(rowIn[x..]));
            Vector<float> outV = inV * factorV;

            outV.CopyTo(outR[x..]);
            outV.CopyTo(outG[x..]);
            outV.CopyTo(outB[x..]);
        }
    }

    private static void SingleFromSingle(int xSize, ReadOnlySpan<int> rowIn, float factor, Span<float> rowOut)
    {
        Vector<float> factorV = new(factor);

        int lanes = Vector<float>.Count;

        for (int x = 0; x < xSize; x += lanes)
        {
            Vector<float> inV = Vector.ConvertToSingle(Vector.Create(rowIn[x..]));
            Vector<float> outV = inV * factorV;

            outV.CopyTo(rowOut[x..]);
        }
    }

    private static void SingleFromSingleAccurate(int xSize, Span<int> rowIn, float factor, Span<float> rowOut)
    {
        // TensorPrimitives doesn't have a .ConvertToSingle
        // with Span<int> as input.
        for (int x = 0; x < xSize; x++)
        {
            rowOut[x] = rowIn[x] * factor;
        }
    }

    private static void IntToFloat(ReadOnlySpan<float> rowIn, Span<float> rowOut, int xSize, int bits, int expBits)
    {
        if (bits == 32)
        {
            if (expBits != 8)
            {
                throw new InvalidOperationException("Exponent bits must be equal to 8");
            }

            rowIn[..xSize].CopyTo(rowOut);
            return;
        }

        int expBias = (1 << (expBits - 1)) - 1;
        int signShift = bits - 1;
        int mantBits = bits - expBits - 1;
        int mantShift = 23 - mantBits;

        for (int x = 0; x < xSize; x++)
        {
            uint f = unchecked((uint)BitConverter.SingleToInt32Bits(rowIn[x]));
            uint signBit = f >> signShift;

            f &= (1u << signShift) - 1;

            if (f == 0)
            {
                rowOut[x] = signBit != 0 ? -0f : 0f;
                continue;
            }

            int exp = (int)(f >> mantBits);
            int mantissa = (int)(f & ((1u << mantBits) - 1));

            if (exp == (1 << expBits) - 1)
            {
                // NaN or infinity.
                uint result = signBit != 0 ? 0x80000000u : 0u;
                result |= 0xFFu << 23;
                result |= (uint)(mantissa << mantShift);

                rowOut[x] = BitConverter.Int32BitsToSingle(unchecked((int)result));
                continue;
            }

            mantissa <<= mantShift;

            // Try to normalize only if there is space for maneuver.
            if (exp == 0 && expBits < 8)
            {
                // Subnormal number.
                while ((mantissa & 0x800000) == 0)
                {
                    mantissa <<= 1;
                    exp--;
                }

                exp++;

                // Remove leading 1 because it is implicit now.
                mantissa &= 0x7FFFFF;
            }

            exp -= expBias;

            // Break up the arbitrary float into its parts, then reassemble
            // into binary32.
            exp += 127;

            if (exp < 0)
            {
                throw new InvalidOperationException("Exponent cannot be negative");
            }

            uint resultBits = signBit != 0 ? 0x80000000u : 0u;
            resultBits |= (uint)(exp << 23);
            resultBits |= (uint)mantissa;

            rowOut[x] = BitConverter.Int32BitsToSingle(unchecked((int)resultBits));
        }
    }

    public void DecodeQuantizerTable(int requiredSizeX, int requiredSizeY, JxlBitReader reader, JxlQuantizerEncoding encoding, int index, JxlModularFrameDecoder? decoder)
    {
        float qtableDen = 0;
        if (!JxlF16Coder.Read(reader, ref qtableDen))
        {
            throw new InvalidOperationException("Quantizer table denominator is too small");
        }

        encoding.QuantizationTableDenominator = qtableDen;

        using JxlModularImage img = JxlModularImage.Create(this.configuration, requiredSizeX, requiredSizeY, 8, 3);
        JxlModularOptions options = new();

        if (decoder is not null)
        {
            JxlModularStreamId qt = JxlModularStreamId.CreateQuantizerTable(index);
            ModularGenericDecompress(
                reader,
                img,
                null,
                qt.GetId(decoder.frameDimensions),
                options,
                true,
                decoder.tree,
                decoder.code,
                decoder.contextMap);
        }
        else
        {
            ModularGenericDecompress(
                reader,
                img,
                null,
                0,
                options,
                true);
        }

        if (encoding.QuantizationTable is null)
        {
            encoding.QuantizationTable = new int[requiredSizeX * requiredSizeY * 3];
        }
        else
        {
            if (encoding.QuantizationTable.Length != requiredSizeX * requiredSizeY * 3)
            {
                throw new InvalidOperationException("The length of the quantization table doesn't match");
            }
        }

        Span<int> qtable = encoding.QuantizationTable.AsSpan();

        for (int c = 0; c < 3; c++)
        {
            for (int y = 0; y < requiredSizeY; y++)
            {
                Span<int> row = img.Channels[c].GetRow(y);

                for (int x = 0; x < requiredSizeX; x++)
                {
                    qtable[(c * requiredSizeX * requiredSizeY) + (y * requiredSizeX) + x] = row[x];

                    if (row[x] <= 0)
                    {
                        throw new InvalidOperationException("Invalid raw quantization table");
                    }
                }
            }
        }
    }
}
