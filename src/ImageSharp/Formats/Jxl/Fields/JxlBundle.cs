// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.IO.FrameHeader;
using SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;
using SixLabors.ImageSharp.Formats.Jxl.Processing;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.AuxiliaryOutput;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Quantization;

namespace SixLabors.ImageSharp.Formats.Jxl.Fields;

/// <summary>
/// An <see cref="IJxlFields"/> helper.
/// </summary>
internal static class JxlBundle
{
    /// <summary>
    /// Initializes the specified JXL fields.
    /// </summary>
    /// <param name="fields">The JXL fields.</param>
    public static void Init(IJxlFields fields)
    {
        JxlInitVisitor initVisitor = new();

        if (!initVisitor.Visit(fields))
        {
            DebugGuard.IsTrue(false, "Init should never fail");
        }
    }

    /// <summary>
    /// Sets all JXL fields provided by the input value to their defaults.
    /// </summary>
    /// <param name="fields">The JXL fields.</param>
    public static void SetDefault(IJxlFields fields)
    {
        JxlSetDefaultVisitor visitor = new();

        if (!visitor.Visit(fields))
        {
            DebugGuard.IsTrue(false, "SetDefault should never fail");
        }
    }

    /// <summary>
    /// Returns a value indicating whether every value provided by this
    /// field is a default value. If at least one field isn't a default
    /// value, the method returns false.
    /// </summary>
    /// <param name="fields">The JXL fields.</param>
    /// <returns>A boolean indicating whether or not are all values initialized to their default values.</returns>
    public static bool AllDefault(IJxlFields fields)
    {
        JxlAllDefaultVisitor allDefaultVisitor = new();

        if (!allDefaultVisitor.Visit(fields))
        {
            DebugGuard.IsTrue(false, "AllDefault should never fail");
        }

        return allDefaultVisitor.IsAllDefault;
    }

    /// <summary>
    /// Reads the fields from a bit-reader.
    /// </summary>
    /// <param name="reader">The bit-reader.</param>
    /// <param name="fields">The fields.</param>
    /// <returns>Status of the read operation.</returns>
    public static bool Read(JxlBitReader reader, IJxlFields fields)
    {
        JxlReadVisitor visitor = new(reader);
        if (!visitor.Visit(fields))
        {
            return false;
        }

        return visitor.OK;
    }

    public static bool CanEncode(IJxlFields fields, ref int extensionBits, ref long totalBits)
    {
        JxlCanEncodeVisitor canEncodeVisitor = new();

        if (!canEncodeVisitor.Visit(fields))
        {
            return false;
        }

        if (!canEncodeVisitor.GetSizes(ref extensionBits, ref totalBits))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Tries to read the fields from a bit-reader.
    /// </summary>
    /// <param name="reader">The bit-reader.</param>
    /// <param name="fields">The fields.</param>
    /// <returns>Status of the read operation.</returns>
    public static bool CanRead(JxlBitReader reader, IJxlFields fields)
    {
        JxlReadVisitor visitor = new(reader);
        _ = visitor.Visit(fields);
        return visitor.OK;
    }

    public static bool Write(IJxlFields fields, JxlBitWriter writer, JxlLayerType layer, JxlAuxiliaryOutput auxOutput)
    {
        int extensionBits = 0;
        long totalBits = 0;
        if (!CanEncode(fields, ref extensionBits, ref totalBits))
        {
            return false;
        }

        return writer.WithMaxBits(totalBits, () =>
        {
            JxlWriteVisitor visitor = new(extensionBits, writer);

            if (!visitor.Visit(fields))
            {
                return false;
            }

            return visitor.OK();
        });
    }

    public static bool WriteCodestreamHeaders(JxlCodecMetadata metadata, JxlBitWriter writer, JxlAuxiliaryOutput auxOut)
    {
        // Marker/signature
        if (!writer.WithMaxBits(16, () =>
        {
            writer.Write(8, 0xFF);
            writer.Write(8, JxlShared.CodestreamMarker);
            return true;
        }))
        {
            return false;
        }

        if (!WriteSizeHeader(metadata.Size!, writer, JxlLayerType.Header, auxOut))
        {
            return false;
        }

        if (!WriteImageMetadata(metadata.ImageMetadata!, writer, JxlLayerType.Header, auxOut))
        {
            return false;
        }

        metadata.CustomTransformData!.NonserializedXybEncoded = metadata.ImageMetadata!.XybEncoded;

        return Write(metadata.CustomTransformData!, writer, JxlLayerType.Header, auxOut);
    }

    public static bool WriteFrameHeader(JxlFrameHeader frame, JxlBitWriter writer, JxlAuxiliaryOutput auxOut)
        => Write(frame, writer, JxlLayerType.Header, auxOut);

    public static bool WriteImageMetadata(JxlImageMetadata metadata, JxlBitWriter writer, JxlLayerType layer, JxlAuxiliaryOutput auxOut)
        => Write(metadata, writer, layer, auxOut);

    public static bool WriteQuantizerParameters(JxlQuantizerParameters metadata, JxlBitWriter writer, JxlLayerType layer, JxlAuxiliaryOutput auxOut)
        => Write(metadata, writer, layer, auxOut);

    public static bool WriteSizeHeader(JxlSizeHeader header, JxlBitWriter writer, JxlLayerType layer, JxlAuxiliaryOutput auxOut)
        => Write(header, writer, layer, auxOut);
}
