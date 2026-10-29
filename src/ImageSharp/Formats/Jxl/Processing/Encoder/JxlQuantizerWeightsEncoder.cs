// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Fields;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Dct;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Modular;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Quantization;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder;

/// <summary>
/// Writes transform parameters, such as quantization and DCT,
/// into the bit-stream.
/// </summary>
internal static class JxlQuantizerWeightsEncoder
{
    /// <summary>Writes the active fixed distance bands without copying the parameter value.</summary>
    /// <param name="parameters">The borrowed channel bands and their active count.</param>
    /// <param name="writer">The destination bit writer.</param>
    /// <returns>Whether the band count and all F16 values were written successfully.</returns>
    public static bool EncodeDctParameters(in JxlDctQuantWeightParameters parameters, JxlBitWriter writer)
    {
        if (parameters.NumDistanceBands < 1)
        {
            // at least 1 distance band is required
            return false;
        }

        writer.Write(JxlDctQuantWeightParameters.Log2MaxDistanceBands, parameters.NumDistanceBands - 1);

        for (int c = 0; c < 3; c++)
        {
            ReadOnlySpan<float> bands = parameters.DistanceBands[c];

            for (int i = 0; i < parameters.NumDistanceBands; i++)
            {
                // each DCT distance band is a 16-bit half-precision floating-point number
                if (!JxlF16Coder.Write(bands[i] * (i == 0 ? (1 / 64.0f) : 1.0f), writer))
                {
                    return false;
                }
            }
        }

        return true;
    }

    public static bool EncodeQuant(Configuration configuration, JxlQuantizerEncoding encoding, int idx, int sizeX, int sizeY, JxlBitWriter writer, JxlModularFrameEncoder encoder)
    {
        writer.Write(JxlQuantizerConstants.Log2NumQuantModes, (int)encoding.Mode);

        sizeX *= JxlFrameDimensions.BlockDimensions;
        sizeY *= JxlFrameDimensions.BlockDimensions;

        switch (encoding.Mode)
        {
            case JxlQuantMode.Library:
                writer.Write(JxlQuantizerConstants.CeilLog2NumPredefinedTables, encoding.Predefined);
                break;

            case JxlQuantMode.Id:
                for (int c = 0; c < 3; c++)
                {
                    ReadOnlySpan<float> idWeight = encoding.ModeWeights[c];

                    for (int i = 0; i < 3; i++)
                    {
                        if (!JxlF16Coder.Write(idWeight[i] * (1.0f / 64), writer))
                        {
                            return false;
                        }
                    }
                }

                break;

            case JxlQuantMode.Dct2:
                for (int c = 0; c < 3; c++)
                {
                    ReadOnlySpan<float> dct2Weight = encoding.ModeWeights[c];

                    for (int i = 0; i < 6; i++)
                    {
                        if (!JxlF16Coder.Write(dct2Weight[i] * (1.0f / 64), writer))
                        {
                            return false;
                        }
                    }
                }

                break;

            case JxlQuantMode.Dct4x8:
                if (!JxlF16Coder.Write(encoding.ModeWeights[0][0], writer) ||
                    !JxlF16Coder.Write(encoding.ModeWeights[1][0], writer) ||
                    !JxlF16Coder.Write(encoding.ModeWeights[2][0], writer))
                {
                    return false;
                }

                if (!EncodeDctParameters(in encoding.DctParameters, writer))
                {
                    return false;
                }

                break;

            case JxlQuantMode.Dct4:
                for (int c = 0; c < 3; c++)
                {
                    ReadOnlySpan<float> dct4Mul = encoding.ModeWeights[c];

                    if (!JxlF16Coder.Write(dct4Mul[0], writer) || !JxlF16Coder.Write(dct4Mul[1], writer))
                    {
                        return false;
                    }
                }

                if (!EncodeDctParameters(in encoding.DctParameters, writer))
                {
                    return false;
                }

                break;

            case JxlQuantMode.Dct:
                if (!EncodeDctParameters(in encoding.DctParameters, writer))
                {
                    return false;
                }

                break;

            case JxlQuantMode.Raw:
                if (!JxlModularFrameEncoder.EncodeQuantTable(configuration, sizeX, sizeY, writer, encoding, idx, encoder))
                {
                    return false;
                }

                break;

            case JxlQuantMode.Afv:
                for (int c = 0; c < 3; c++)
                {
                    ReadOnlySpan<float> afvWeights = encoding.ModeWeights[c];

                    for (int i = 0; i < 9; i++)
                    {
                        if (!JxlF16Coder.Write(afvWeights[i] * (i < 6 ? 1.0f / 64 : 1.0f), writer))
                        {
                            return false;
                        }
                    }
                }

                if (!EncodeDctParameters(in encoding.DctParameters, writer))
                {
                    return false;
                }

                if (!EncodeDctParameters(in encoding.DctParametersAfv4x4, writer))
                {
                    return false;
                }

                break;
        }

        return true;
    }

    public static bool DequantMatricesEncode(Configuration configuration, JxlDequantMatrices matrices, JxlBitWriter writer, JxlModularFrameEncoder? encoder)
    {
        bool allDefault = true;

        ReadOnlySpan<JxlQuantizerEncoding> encodings = matrices.GetEncodings();

        for (int i = 0; i < encodings.Length; i++)
        {
            JxlQuantizerEncoding enc = encodings[i];

            if (enc.Mode != JxlQuantMode.Library || enc.Predefined != 0)
            {
                allDefault = false;
            }
        }

        return writer.WithMaxBits(512 * 1024, () =>
        {
            writer.Write(1, allDefault ? 1 : 0);

            if (!allDefault)
            {
                ReadOnlySpan<JxlQuantizerEncoding> tables = matrices.GetEncodings();
                for (int i = 0; i < tables.Length; i++)
                {
                    if (!EncodeQuant(configuration, tables[i], i, JxlDequantMatrices.RequiredSizeX[i], JxlDequantMatrices.RequiredSizeY[i], writer, encoder!))
                    {
                        return false;
                    }
                }
            }

            return true;
        });
    }

    public static bool DequantMatricesEncodeDc(JxlDequantMatrices matrices, JxlBitWriter writer)
    {
        Span<float> dcQuant = matrices.GetDcQuants();
        bool allDefault = dcQuant[0] == JxlQuantizerConstants.DcQuant[0]
            && dcQuant[1] == JxlQuantizerConstants.DcQuant[1]
            && dcQuant[2] == JxlQuantizerConstants.DcQuant[2];

        return writer.WithMaxBits(1 + (sizeof(float) * JxlMath.BitsPerByte * 3), () =>
        {
            ReadOnlySpan<float> values = matrices.GetDcQuants();
            writer.Write(1, allDefault ? 1 : 0);

            if (!allDefault)
            {
                if (!JxlF16Coder.Write(values[0] * 128.0f, writer) ||
                    !JxlF16Coder.Write(values[1] * 128.0f, writer) ||
                    !JxlF16Coder.Write(values[2] * 128.0f, writer))
                {
                    return false;
                }
            }

            return true;
        });
    }

    public static bool DequantMatricesSetCustomDc(JxlDequantMatrices matrices, InlineArray3<float> dc)
    {
        matrices.SetDcQuant(dc);

        // TODO: we might need a fixed-size memory stream to reduce GC
        // pressure on MemoryStream's underlying array resizes, the
        // encoded value should be < 12 bytes
        using MemoryStream ms = new();
        JxlBitWriter writer = new(ms);

        if (!DequantMatricesEncodeDc(matrices, writer))
        {
            return false;
        }

        writer.ZeroPadToByte();

        ms.Position = 0;
        JxlBitReader reader = new(ms);

        if (!matrices.DecodeDc(reader))
        {
            return false;
        }

        return true;
    }

    public static bool DequantMatricesScaleDc(JxlDequantMatrices matrices, float scale)
    {
        InlineArray3<float> dc = default;
        dc[0] = matrices.GetInverseDcQuant(0) * (1.0f / scale);
        dc[1] = matrices.GetInverseDcQuant(1) * (1.0f / scale);
        dc[2] = matrices.GetInverseDcQuant(2) * (1.0f / scale);

        return DequantMatricesSetCustomDc(matrices, dc);
    }

    public static bool DequantMatricesRoundtrip(Configuration configuration, JxlDequantMatrices matrices)
    {
        // Up to 64KB of data
        // TODO: pool 64KB of data & use it as fixed-size data for a custom stream?
        using MemoryStream ms = new();
        JxlBitWriter writer = new(ms);

        if (!DequantMatricesEncode(configuration, matrices, writer, null))
        {
            return false;
        }

        writer.ZeroPadToByte();

        ms.Position = 0;
        JxlBitReader reader = new(ms);

        if (!matrices.Decode(configuration, reader))
        {
            return false;
        }

        return true;
    }

    public static bool DequantMatricesSetCustom(Configuration configuration, JxlDequantMatrices matrices, JxlQuantizerEncoding[] encodings, JxlModularFrameEncoder encoder)
    {
        if (encodings.Length != JxlQuantizerConstants.NumberOfQuantizerTables)
        {
            return false;
        }

        matrices.SetEncodings(encodings);

        for (int i = 0; i < encodings.Length; i++)
        {
            if (encodings[i].Mode == JxlQuantMode.Raw)
            {
                if (!encoder.AddQuantTable(
                    JxlDequantMatrices.RequiredSizeX[i] * JxlFrameDimensions.BlockDimensions,
                    JxlDequantMatrices.RequiredSizeY[i] * JxlFrameDimensions.BlockDimensions,
                    encodings[i],
                    i))
                {
                    return false;
                }
            }
        }

        if (!DequantMatricesRoundtrip(configuration, matrices))
        {
            return false;
        }

        return true;
    }
}
