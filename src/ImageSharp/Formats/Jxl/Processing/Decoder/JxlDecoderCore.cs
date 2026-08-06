// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.IO;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

internal sealed class JxlDecoderCore : ImageDecoderCore
{
    /// <summary>
    /// Identifies the signature of the JPEG XL file.
    /// </summary>
    private enum JxlSignature : byte
    {
        /// <summary>
        /// Error status indicating not enough bytes to detect the signature.
        /// </summary>
        NotEnoughBytes,

        /// <summary>
        /// A JPEG XL code stream.
        /// </summary>
        CodeStream,

        /// <summary>
        /// The signature is invalid.
        /// </summary>
        Invalid,

        /// <summary>
        /// Container format.
        /// </summary>
        Container
    }

    /// <summary>
    /// Represents a data type.
    /// </summary>
    private enum JxlDataType : byte
    {
        /// <summary>
        /// <see cref="byte"/>
        /// </summary>
        UInt8,

        /// <summary>
        /// <see cref="ushort"/>
        /// </summary>
        UInt16,

        /// <summary>
        /// <see cref="float"/>
        /// </summary>
        Float,

        /// <summary>
        /// <see cref="Half"/>
        /// </summary>
        Float16
    }

    public JxlDecoderCore(DecoderOptions options)
        : base(options)
    {
    }

    /// <summary>
    /// Ensures that the coordinates are not out of bounds.
    /// </summary>
    /// <param name="a">First coordinate</param>
    /// <param name="b">Second coordinate</param>
    /// <param name="size">Image width</param>
    /// <returns>Boolean indicating whether the coordinates are out of bounds</returns>
    private static bool IsOutOfBounds(int a, int b, int size)
    {
        long position = a + b;

        return position > size || position < a;
    }

    private static int InitialBasicInfoSizeHint()
    {
        const int containerHeaderSize = 48;
        const int maxCodestreamBasicInfoSize = 50;
        return containerHeaderSize + maxCodestreamBasicInfoSize;
    }

    private static JxlSignature DetectSignature(ReadOnlySpan<byte> buffer, int length, ref int position)
    {
        if (position >= length)
        {
            return JxlSignature.NotEnoughBytes;
        }

        buffer = buffer[position..];
        length -= position;

        // 0xFF 0x0A represents a codestream
        if (length >= 1 && buffer[0] == 0xFF)
        {
            if (length < 2)
            {
                // We need at least two bytes for a valid codestream signature
                return JxlSignature.NotEnoughBytes;
            }
            else if (buffer[1] == CodestreamMarker)
            {
                position += 2;
                return JxlSignature.CodeStream;
            }
            else
            {
                return JxlSignature.Invalid;
            }
        }

        // Container?
        if (length >= 1 && buffer[0] == 0)
        {
            if (length < SignatureBox.Length)
            {
                return JxlSignature.NotEnoughBytes;
            }
            else if (buffer[SignatureBox.Length..].SequenceEqual(SignatureBox))
            {
                position += SignatureBox.Length;
                return JxlSignature.Container;
            }
            else
            {
                return JxlSignature.Invalid;
            }
        }

        // Signature is invalid
        return JxlSignature.Invalid;
    }

    private static JxlSignature DetectSignature(ReadOnlySpan<byte> buffer, int length)
    {
        int position = 0;
        return DetectSignature(buffer, length, ref position);
    }

    private static int BitsPerChannel(JxlDataType dataType)
        => dataType switch
        {
            JxlDataType.UInt8 => 8,
            JxlDataType.UInt16 or JxlDataType.Float16 => 16,
            JxlDataType.Float => 32,
            _ => 0
        };

    protected override Image<TPixel> Decode<TPixel>(BufferedReadStream stream, CancellationToken cancellationToken) => throw new NotImplementedException();

    protected override ImageInfo Identify(BufferedReadStream stream, CancellationToken cancellationToken) => throw new NotImplementedException();
}
