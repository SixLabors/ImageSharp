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
    /// Maximum extension count and nested extension-state depth. Both use one bit per entry in a UInt64.
    /// </summary>
    public const int MaxExtensions = 64;

    /// <summary>
    /// Initializes the specified JXL fields.
    /// </summary>
    /// <param name="fields">The JXL fields.</param>
    public static void Init(IJxlFields fields)
    {
        JxlSetDefaultVisitor initVisitor = new(false);

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
        JxlSetDefaultVisitor visitor = new(true);

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

    /// <summary>
    /// Measures the complete serialized length of a bundle without writing it.
    /// </summary>
    /// <param name="fields">The fields to measure.</param>
    /// <param name="extensionBits">The current bundle's extension-payload length in bits.</param>
    /// <param name="totalBits">The complete wire length, including nested bundles and extension-length fields.</param>
    /// <returns>Whether every field can be encoded.</returns>
    public static bool CanEncode(IJxlFields fields, ref int extensionBits, ref long totalBits)
    {
        JxlCanEncodeVisitor canEncodeVisitor = new();

        return canEncodeVisitor.Measure(fields, ref extensionBits, ref totalBits);
    }

    /// <summary>
    /// Reads fields and reports whether they are complete and valid.
    /// </summary>
    /// <param name="reader">The bit-reader.</param>
    /// <param name="fields">The fields.</param>
    /// <returns>Whether all fields are complete and valid.</returns>
    /// <remarks>The operation advances the reader and updates the supplied fields. It does not restore either on failure.</remarks>
    public static bool CanRead(JxlBitReader reader, IJxlFields fields)
    {
        JxlReadVisitor visitor = new(reader);

        try
        {
            return visitor.Visit(fields) && visitor.OK;
        }
        catch (EndOfStreamException)
        {
            return false;
        }
    }

    /// <summary>
    /// Writes one validated bundle and charges its actual bit count to the supplied encoder layer.
    /// </summary>
    /// <param name="fields">The fields to write.</param>
    /// <param name="writer">The output bit writer.</param>
    /// <param name="layer">The statistics layer for these fields.</param>
    /// <param name="auxOutput">The optional encoder statistics.</param>
    /// <returns>Whether all fields can be encoded and were written successfully.</returns>
    public static bool Write(IJxlFields fields, JxlBitWriter writer, JxlLayerType layer, JxlAuxiliaryOutput? auxOutput)
    {
        int extensionBits = 0;
        long totalBits = 0;
        JxlCanEncodeVisitor sizeVisitor = new();
        if (!sizeVisitor.Measure(fields, ref extensionBits, ref totalBits))
        {
            return false;
        }

        return writer.WithMaxBits((ulong)totalBits, layer, auxOutput, () =>
        {
            JxlWriteVisitor visitor = new(extensionBits, writer, sizeVisitor);

            if (!visitor.Visit(fields))
            {
                return false;
            }

            return visitor.OK;
        });
    }

    /// <summary>
    /// Writes the codestream signature, image dimensions, image metadata and custom transform fields.
    /// </summary>
    /// <param name="metadata">The codestream metadata.</param>
    /// <param name="writer">The output bit writer.</param>
    /// <param name="auxOut">The optional encoder statistics.</param>
    /// <returns>Whether all header fields can be encoded and were written successfully.</returns>
    public static bool WriteCodestreamHeaders(JxlCodecMetadata metadata, JxlBitWriter writer, JxlAuxiliaryOutput? auxOut)
    {
        // Marker/signature
        if (!writer.WithMaxBits(16, JxlLayerType.Header, auxOut, () =>
        {
            writer.Write(8, 0xFF);
            writer.Write(8, JxlShared.CodestreamMarker);
            return true;
        }))
        {
            return false;
        }

        if (!WriteSizeHeader(metadata.Size, writer, JxlLayerType.Header, auxOut))
        {
            return false;
        }

        if (!WriteImageMetadata(metadata.ImageMetadata, writer, JxlLayerType.Header, auxOut))
        {
            return false;
        }

        metadata.CustomTransformData.NonserializedXybEncoded = metadata.ImageMetadata.XybEncoded;

        return Write(metadata.CustomTransformData, writer, JxlLayerType.Header, auxOut);
    }

    /// <summary>
    /// Writes one frame header.
    /// </summary>
    /// <param name="frame">The frame fields.</param>
    /// <param name="writer">The output bit writer.</param>
    /// <param name="auxOut">The optional encoder statistics.</param>
    /// <returns>Whether the frame fields can be encoded and were written successfully.</returns>
    public static bool WriteFrameHeader(JxlFrameHeader frame, JxlBitWriter writer, JxlAuxiliaryOutput? auxOut)
        => Write(frame, writer, JxlLayerType.Header, auxOut);

    /// <summary>
    /// Writes the image metadata fields.
    /// </summary>
    /// <param name="metadata">The metadata fields.</param>
    /// <param name="writer">The output bit writer.</param>
    /// <param name="layer">The encoder statistics layer.</param>
    /// <param name="auxOut">The optional encoder statistics.</param>
    /// <returns>Whether the metadata fields can be encoded and were written successfully.</returns>
    public static bool WriteImageMetadata(JxlImageMetadata metadata, JxlBitWriter writer, JxlLayerType layer, JxlAuxiliaryOutput? auxOut)
        => Write(metadata, writer, layer, auxOut);

    /// <summary>
    /// Writes the quantizer parameters.
    /// </summary>
    /// <param name="metadata">The quantizer fields.</param>
    /// <param name="writer">The output bit writer.</param>
    /// <param name="layer">The encoder statistics layer.</param>
    /// <param name="auxOut">The optional encoder statistics.</param>
    /// <returns>Whether the quantizer fields can be encoded and were written successfully.</returns>
    public static bool WriteQuantizerParameters(JxlQuantizerParameters metadata, JxlBitWriter writer, JxlLayerType layer, JxlAuxiliaryOutput? auxOut)
        => Write(metadata, writer, layer, auxOut);

    /// <summary>
    /// Writes the image dimensions.
    /// </summary>
    /// <param name="header">The image dimension fields.</param>
    /// <param name="writer">The output bit writer.</param>
    /// <param name="layer">The encoder statistics layer.</param>
    /// <param name="auxOut">The optional encoder statistics.</param>
    /// <returns>Whether the dimensions can be encoded and were written successfully.</returns>
    public static bool WriteSizeHeader(JxlSizeHeader header, JxlBitWriter writer, JxlLayerType layer, JxlAuxiliaryOutput? auxOut)
        => Write(header, writer, layer, auxOut);
}
