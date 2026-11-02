// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Jxl.Fields;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Ans;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

/// <summary>
/// Reconstructs the ICC profile bytes stored in a JPEG XL entropy stream.
/// </summary>
/// <remarks>
/// This reader reverses the codec's byte predictions. The shared ICC reader owns profile validation and interpretation.
/// </remarks>
internal static class JxlIccReader
{
    private const int ContextCount = 41;
    private const int HeaderSize = 128;
    private const int PreambleSize = 20;
    private const int MaximumEncodedSize = 1 << 28;
    private const int MaximumEncodedOverhead = 65536;
    private const int ExpansionCheckInterval = 65536;
    private const int MaximumEncodedBytesPerInputByte = 256;
    private const int BitsPerByte = 8;
    private const uint BlackMatrixColumn = 0x6B58595A;
    private const uint GamutBoundaryDescription = 0x67626420;

    /// <summary>
    /// Gets tag signatures in the order assigned by the compressed ICC syntax.
    /// </summary>
    private static ReadOnlySpan<uint> TagSignatures =>
    [
        (uint)IccProfileTag.Copyright,
        (uint)IccProfileTag.MediaWhitePoint,
        (uint)IccProfileTag.MediaBlackPoint,
        (uint)IccProfileTag.RedMatrixColumn,
        (uint)IccProfileTag.GreenMatrixColumn,
        (uint)IccProfileTag.BlueMatrixColumn,
        BlackMatrixColumn,
        (uint)IccProfileTag.RedTrc,
        (uint)IccProfileTag.GreenTrc,
        (uint)IccProfileTag.BlueTrc,
        (uint)IccProfileTag.GrayTrc,
        (uint)IccProfileTag.ChromaticAdaptation,
        (uint)IccProfileTag.ProfileDescription,
        (uint)IccProfileTag.Chromaticity,
        (uint)IccProfileTag.DeviceManufacturerDescription,
        (uint)IccProfileTag.DeviceModelDescription,
        (uint)IccProfileTag.Luminance
    ];

    /// <summary>
    /// Gets type signatures in the order assigned by the compressed ICC syntax.
    /// </summary>
    private static ReadOnlySpan<uint> TypeSignatures =>
    [
        (uint)IccTypeSignature.Xyz,
        (uint)IccTypeSignature.TextDescription,
        (uint)IccTypeSignature.Text,
        (uint)IccTypeSignature.MultiLocalizedUnicode,
        (uint)IccTypeSignature.ParametricCurve,
        (uint)IccTypeSignature.Curve,
        (uint)IccTypeSignature.S15Fixed16Array,
        GamutBoundaryDescription
    ];

    /// <summary>
    /// Reads and reconstructs one compressed ICC profile without interpreting its tags.
    /// </summary>
    /// <param name="configuration">The configuration whose allocator owns temporary encoded-byte storage.</param>
    /// <param name="bitReader">The reader positioned at the compressed profile's encoded size.</param>
    /// <returns>The reconstructed profile bytes.</returns>
    /// <exception cref="InvalidImageContentException">The entropy stream or compressed profile commands are invalid.</exception>
    public static byte[] Read(Configuration configuration, JxlBitReader bitReader)
    {
        long initialBitPosition = bitReader.TotalBitsConsumed;
        ulong encodedSize = JxlU64Coder.Read(bitReader);
        if (encodedSize > MaximumEncodedSize)
        {
            throw new InvalidImageContentException("JPEG XL compressed ICC data is too large.");
        }

        JxlAnsCode code = new();
        List<byte> contextMap = [];
        JxlAnsReader.DecodeHistograms(configuration, bitReader, ContextCount, code, ref contextMap, false);

        using JxlAnsSymbolReader reader = JxlAnsSymbolReader.Create(configuration, code, bitReader, 0);
        Span<int> window = reader.Lz77Window;
        ReadOnlySpan<byte> contexts = CollectionsMarshal.AsSpan(contextMap);

        // Two UInt64 varints occupy at most twenty bytes. Decode them before trusting either allocation size.
        // These first symbols all use context zero, regardless of their previous bytes.
        Span<byte> preamble = stackalloc byte[PreambleSize];
        int preambleLength = Math.Min((int)encodedSize, PreambleSize);
        for (int i = 0; i < preambleLength; i++)
        {
            preamble[i] = unchecked((byte)reader.ReadHybridUInt(0, bitReader, contexts, window));
        }

        int preamblePosition = 0;
        ulong outputSize = ReadVarInt(preamble[..preambleLength], ref preamblePosition);
        ulong commandSize = ReadVarInt(preamble[..preambleLength], ref preamblePosition);
        if (outputSize >= IccProfile.MaxProfileSize || commandSize > encodedSize - (ulong)preamblePosition)
        {
            throw new InvalidImageContentException("JPEG XL compressed ICC sizes are invalid.");
        }

        // Every header byte needs one residual byte. Reject an impossible header before allocating either declared buffer.
        ulong dataSize = encodedSize - (ulong)preamblePosition - commandSize;
        if (Math.Min(outputSize, HeaderSize) > dataSize)
        {
            throw new InvalidImageContentException("JPEG XL compressed ICC header is truncated.");
        }

        // Encoded data can exceed reconstructed data by at most 65,536 bytes.
        // This check bounds temporary storage before the allocator receives the encoded size.
        if (outputSize + MaximumEncodedOverhead < encodedSize)
        {
            throw new InvalidImageContentException("JPEG XL compressed ICC data exceeds its reconstructed size limit.");
        }

        using IMemoryOwner<byte> encodedOwner = configuration.MemoryAllocator.Allocate<byte>((int)encodedSize);
        Span<byte> encoded = encodedOwner.GetSpan();
        preamble[..preambleLength].CopyTo(encoded);

        // Keep the allocator span and LZ77 span outside the symbol loop. Each context depends on the last two encoded bytes.
        for (int i = preambleLength; i < encoded.Length; i++)
        {
            if ((i & (ExpansionCheckInterval - 1)) == 0)
            {
                // The native corruption limit includes length and entropy headers. Compare in bits to retain partial input bytes.
                // Both sides use Int64: the encoded-byte limit bounds these products well below its maximum.
                long consumedInputBits = bitReader.TotalBitsConsumed - initialBitPosition;
                if ((long)i * BitsPerByte > consumedInputBits * MaximumEncodedBytesPerInputByte)
                {
                    throw new InvalidImageContentException("JPEG XL compressed ICC entropy expansion exceeds its limit.");
                }
            }

            int context = i <= HeaderSize ? 0 : 1 + GetByteKind(encoded[i - 1], false) + (8 * GetByteKind(encoded[i - 2], true));
            encoded[i] = unchecked((byte)reader.ReadHybridUInt(context, bitReader, contexts, window));
        }

        if (!reader.CheckAnsFinalState())
        {
            throw new InvalidImageContentException("JPEG XL compressed ICC entropy state is invalid.");
        }

        byte[] output = new byte[(int)outputSize];
        Reconstruct(encoded, preamblePosition, (int)commandSize, output);
        return output;
    }

    /// <summary>
    /// Classifies one preceding byte for the first or second ICC entropy context component.
    /// </summary>
    /// <param name="value">The preceding encoded byte.</param>
    /// <param name="second">Whether this is the second preceding byte.</param>
    /// <returns>The context component assigned by the compressed ICC syntax.</returns>
    private static int GetByteKind(byte value, bool second)
    {
        if (value is (>= (byte)'a' and <= (byte)'z') or (>= (byte)'A' and <= (byte)'Z'))
        {
            return 0;
        }

        if (value is (>= (byte)'0' and <= (byte)'9') or (byte)'.' or (byte)',')
        {
            return 1;
        }

        if (second)
        {
            return value < 16 ? 2 : value > 240 ? 3 : 4;
        }

        return value switch
        {
            0 => 2,
            1 => 3,
            < 16 => 4,
            255 => 6,
            > 240 => 5,
            _ => 7
        };
    }

    /// <summary>
    /// Reads a base-128 UInt64 without accepting truncated, unterminated, or overflowing encodings.
    /// </summary>
    /// <param name="source">The bounded preamble or command stream.</param>
    /// <param name="position">The next byte, updated to the first byte after the integer.</param>
    /// <returns>The decoded integer.</returns>
    private static ulong ReadVarInt(ReadOnlySpan<byte> source, ref int position)
    {
        ulong value = 0;
        for (int i = 0; i < 10; i++)
        {
            if (position >= source.Length)
            {
                throw new InvalidImageContentException("JPEG XL compressed ICC integer is truncated.");
            }

            byte next = source[position++];
            if (i == 9 && next > 1)
            {
                throw new InvalidImageContentException("JPEG XL compressed ICC integer exceeds UInt64.");
            }

            value |= (ulong)(next & 127) << (7 * i);
            if (next < 128)
            {
                return value;
            }
        }

        throw new InvalidImageContentException("JPEG XL compressed ICC integer is unterminated.");
    }

    /// <summary>
    /// Reverses header, tag-table, shuffle, and integer predictions into the final byte array.
    /// </summary>
    /// <param name="encoded">The complete entropy-decoded payload.</param>
    /// <param name="commandStart">The first command byte, after the two preamble integers.</param>
    /// <param name="commandSize">The command-stream length.</param>
    /// <param name="output">The final profile storage.</param>
    private static void Reconstruct(ReadOnlySpan<byte> encoded, int commandStart, int commandSize, Span<byte> output)
    {
        ReadOnlySpan<byte> commands = encoded.Slice(commandStart, commandSize);
        int commandPosition = 0;
        int dataPosition = commandStart + commandSize;
        int outputPosition = 0;

        // This is the codec's header prediction, not a default ICC profile. All unspecified bytes are zero.
        Span<byte> header = stackalloc byte[HeaderSize];
        header.Clear();
        BinaryPrimitives.WriteUInt32BigEndian(header, (uint)output.Length);
        header[8] = 4;
        BinaryPrimitives.WriteUInt32BigEndian(header[12..], (uint)IccProfileClass.DisplayDevice);
        BinaryPrimitives.WriteUInt32BigEndian(header[16..], (uint)IccColorSpaceType.Rgb);
        BinaryPrimitives.WriteUInt32BigEndian(header[20..], (uint)IccColorSpaceType.CieXyz);
        "acsp"u8.CopyTo(header[36..]);

        // These D50 words are exact header-prediction bytes from the format. Do not recompute them from floating-point primaries.
        BinaryPrimitives.WriteUInt32BigEndian(header[68..], 0x0000F6D6);
        BinaryPrimitives.WriteUInt32BigEndian(header[72..], 0x00010000);
        BinaryPrimitives.WriteUInt32BigEndian(header[76..], 0x0000D32D);

        int headerLength = Math.Min(HeaderSize, output.Length);
        for (; outputPosition < headerLength; outputPosition++)
        {
            // The decoded CMM and platform bytes predict later header fields. Read only bytes already reconstructed.
            if (outputPosition == 8)
            {
                output.Slice(4, 4).CopyTo(header[80..]);
            }
            else if (outputPosition == 41)
            {
                if (output[40] == (byte)'A')
                {
                    "PPL"u8.CopyTo(header[41..]);
                }
                else if (output[40] == (byte)'M')
                {
                    "SFT"u8.CopyTo(header[41..]);
                }
            }
            else if (outputPosition == 42 && output[40] == (byte)'S')
            {
                if (output[41] == (byte)'G')
                {
                    "I "u8.CopyTo(header[42..]);
                }
                else if (output[41] == (byte)'U')
                {
                    "NW"u8.CopyTo(header[42..]);
                }
            }

            output[outputPosition] = unchecked((byte)(encoded[dataPosition++] + header[outputPosition]));
        }

        if (outputPosition != output.Length)
        {
            ReadTagTable(commands, ref commandPosition, encoded, ref dataPosition, output, ref outputPosition);
            ReadCommands(commands, ref commandPosition, encoded, ref dataPosition, output, ref outputPosition);
        }

        if (commandPosition != commands.Length || dataPosition != encoded.Length || outputPosition != output.Length)
        {
            throw new InvalidImageContentException("JPEG XL compressed ICC commands do not match their declared sizes.");
        }
    }

    /// <summary>
    /// Restores predicted tag-table entries, including shared TRCs and consecutive XYZ tags.
    /// </summary>
    /// <param name="commands">The bounded command stream.</param>
    /// <param name="commandPosition">The next command byte.</param>
    /// <param name="encoded">The complete encoded payload.</param>
    /// <param name="dataPosition">The next residual-data byte.</param>
    /// <param name="output">The final profile storage.</param>
    /// <param name="outputPosition">The next output byte.</param>
    private static void ReadTagTable(
        ReadOnlySpan<byte> commands,
        ref int commandPosition,
        ReadOnlySpan<byte> encoded,
        ref int dataPosition,
        Span<byte> output,
        ref int outputPosition)
    {
        ulong tagCount = ReadVarInt(commands, ref commandPosition);
        if (tagCount == 0)
        {
            return;
        }

        tagCount--;
        WriteUInt32(output, ref outputPosition, tagCount);

        // The syntax predicts the first offset from 128, not from the 132-byte start of the actual tag table.
        ulong previousStart = HeaderSize + (tagCount * 12);
        ulong previousSize = 0;
        while (commandPosition < commands.Length)
        {
            byte command = commands[commandPosition++];
            int tagCode = command & 63;
            if (tagCode == 0)
            {
                break;
            }

            uint tag;
            if (tagCode == 1)
            {
                RequireLength(4, encoded.Length - dataPosition);
                tag = BinaryPrimitives.ReadUInt32BigEndian(encoded[dataPosition..]);
                dataPosition += 4;
            }
            else if (tagCode == 2)
            {
                tag = (uint)IccProfileTag.RedTrc;
            }
            else if (tagCode == 3)
            {
                tag = (uint)IccProfileTag.RedMatrixColumn;
            }
            else if (tagCode - 4 < TagSignatures.Length)
            {
                tag = TagSignatures[tagCode - 4];
            }
            else
            {
                throw new InvalidImageContentException("JPEG XL compressed ICC tag code is invalid.");
            }

            ulong size = tag is (uint)IccProfileTag.RedMatrixColumn or (uint)IccProfileTag.GreenMatrixColumn
                or (uint)IccProfileTag.BlueMatrixColumn or BlackMatrixColumn or (uint)IccProfileTag.MediaWhitePoint
                or (uint)IccProfileTag.MediaBlackPoint or (uint)IccProfileTag.Luminance ? 20UL : previousSize;

            ulong start = (command & 64) != 0 ? ReadVarInt(commands, ref commandPosition) : previousStart + previousSize;
            if ((command & 128) != 0)
            {
                size = ReadVarInt(commands, ref commandPosition);
            }

            WriteUInt32(output, ref outputPosition, tag);
            WriteUInt32(output, ref outputPosition, start);
            WriteUInt32(output, ref outputPosition, size);
            previousStart = start;
            previousSize = size;

            if (tagCode is 2 or 3)
            {
                // TRCs share one payload. XYZ tags instead occupy three consecutive payloads of equal size.
                bool xyz = tagCode == 3;
                WriteUInt32(output, ref outputPosition, xyz ? (uint)IccProfileTag.GreenMatrixColumn : (uint)IccProfileTag.GreenTrc);
                WriteUInt32(output, ref outputPosition, xyz ? start + size : start);
                WriteUInt32(output, ref outputPosition, size);
                WriteUInt32(output, ref outputPosition, xyz ? (uint)IccProfileTag.BlueMatrixColumn : (uint)IccProfileTag.BlueTrc);
                WriteUInt32(output, ref outputPosition, xyz ? start + (2 * size) : start);
                WriteUInt32(output, ref outputPosition, size);
            }
        }
    }

    /// <summary>
    /// Restores profile payload bytes from the remaining commands.
    /// </summary>
    /// <param name="commands">The bounded command stream.</param>
    /// <param name="commandPosition">The next command byte.</param>
    /// <param name="encoded">The complete encoded payload.</param>
    /// <param name="dataPosition">The next residual-data byte.</param>
    /// <param name="output">The final profile storage.</param>
    /// <param name="outputPosition">The next output byte.</param>
    private static void ReadCommands(
        ReadOnlySpan<byte> commands,
        ref int commandPosition,
        ReadOnlySpan<byte> encoded,
        ref int dataPosition,
        Span<byte> output,
        ref int outputPosition)
    {
        while (commandPosition < commands.Length)
        {
            byte command = commands[commandPosition++];
            if (command is 1 or 2 or 3)
            {
                ulong length = ReadVarInt(commands, ref commandPosition);
                RequireLength(length, encoded.Length - dataPosition);
                RequireLength(length, output.Length - outputPosition);
                ReadOnlySpan<byte> source = encoded.Slice(dataPosition, (int)length);
                Span<byte> destination = output.Slice(outputPosition, (int)length);
                if (command == 1)
                {
                    source.CopyTo(destination);
                }
                else
                {
                    Shuffle(source, destination, command == 2 ? 2 : 4);
                }

                dataPosition += (int)length;
                outputPosition += (int)length;
            }
            else if (command == 4)
            {
                RequireLength(2, commands.Length - commandPosition);
                byte flags = commands[commandPosition++];
                int width = (flags & 3) + 1;
                int order = (flags & 12) >> 2;
                if (width == 3 || order == 3)
                {
                    throw new InvalidImageContentException("JPEG XL compressed ICC predictor is invalid.");
                }

                ulong stride = (flags & 16) != 0 ? ReadVarInt(commands, ref commandPosition) : (ulong)width;
                if (stride < (ulong)width || outputPosition == 0 || stride > (ulong)((outputPosition - 1) >> 2))
                {
                    throw new InvalidImageContentException("JPEG XL compressed ICC predictor stride is invalid.");
                }

                ulong length = ReadVarInt(commands, ref commandPosition);
                RequireLength(length, encoded.Length - dataPosition);
                RequireLength(length, output.Length - outputPosition);
                Predict(encoded.Slice(dataPosition, (int)length), output, outputPosition, (int)stride, width, order);
                dataPosition += (int)length;
                outputPosition += (int)length;
            }
            else if (command == 10)
            {
                RequireLength(12, encoded.Length - dataPosition);
                RequireLength(20, output.Length - outputPosition);
                WriteUInt32(output, ref outputPosition, (uint)IccTypeSignature.Xyz);
                WriteUInt32(output, ref outputPosition, 0);
                encoded.Slice(dataPosition, 12).CopyTo(output[outputPosition..]);
                dataPosition += 12;
                outputPosition += 12;
            }
            else if (command >= 16 && command - 16 < TypeSignatures.Length)
            {
                WriteUInt32(output, ref outputPosition, TypeSignatures[command - 16]);
                WriteUInt32(output, ref outputPosition, 0);
            }
            else
            {
                throw new InvalidImageContentException("JPEG XL compressed ICC command is invalid.");
            }
        }
    }

    /// <summary>
    /// Interleaves two or four byte planes, including incomplete columns at the end of the encoded data.
    /// </summary>
    /// <param name="source">The consecutive encoded byte planes.</param>
    /// <param name="destination">The equally sized interleaved output.</param>
    /// <param name="width">The component count, either two or four.</param>
    private static void Shuffle(ReadOnlySpan<byte> source, Span<byte> destination, int width)
    {
        int height = (source.Length + width - 1) / width;
        int column = 0;
        if (width == 4)
        {
            // Each plane has 'height' bytes, except the final plane. SIMD reads only columns present in all four planes.
            // The shared helper writes consecutive four-byte groups and accepts unaligned input and output.
            int completeColumns = source.Length - (3 * height);
            ref byte input = ref MemoryMarshal.GetReference(source);
            if (Vector512.IsHardwareAccelerated)
            {
                for (; column <= completeColumns - Vector512<byte>.Count; column += Vector512<byte>.Count)
                {
                    SimdUtils.Interleave4(
                        Vector512.LoadUnsafe(ref input, (nuint)column),
                        Vector512.LoadUnsafe(ref input, (nuint)(column + height)),
                        Vector512.LoadUnsafe(ref input, (nuint)(column + (2 * height))),
                        Vector512.LoadUnsafe(ref input, (nuint)(column + (3 * height))),
                        ref destination[4 * column]);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; column <= completeColumns - Vector256<byte>.Count; column += Vector256<byte>.Count)
                {
                    SimdUtils.Interleave4(
                        Vector256.LoadUnsafe(ref input, (nuint)column),
                        Vector256.LoadUnsafe(ref input, (nuint)(column + height)),
                        Vector256.LoadUnsafe(ref input, (nuint)(column + (2 * height))),
                        Vector256.LoadUnsafe(ref input, (nuint)(column + (3 * height))),
                        ref destination[4 * column]);
                }
            }
        }

        // Walk the remaining matrix columns in native order. Missing cells are skipped, rather than padded or read.
        // Start after complete SIMD columns; a short or two-plane shuffle uses this same scalar traversal from column zero.
        int sourcePosition = column;
        for (int i = width * column; i < destination.Length; i++)
        {
            destination[i] = source[sourcePosition];
            sourcePosition += height;
            if (sourcePosition >= source.Length)
            {
                sourcePosition = ++column;
            }
        }
    }

    /// <summary>
    /// Adds shuffled residual bytes to wrapping integer predictions from previously reconstructed output.
    /// </summary>
    /// <param name="source">The encoded residual planes.</param>
    /// <param name="output">The profile storage, including the predictor's completed history.</param>
    /// <param name="start">The first output byte for this command.</param>
    /// <param name="stride">The byte distance between preceding predictor samples.</param>
    /// <param name="width">The integer width, one, two, or four bytes.</param>
    /// <param name="order">The predictor order, zero, one, or two.</param>
    private static void Predict(ReadOnlySpan<byte> source, Span<byte> output, int start, int stride, int width, int order)
    {
        int height = (source.Length + width - 1) / width;
        int column = 0;
        int sourcePosition = 0;
        for (int i = 0; i < source.Length; i++)
        {
            // Samples can depend on bytes produced earlier in this command. Preserve that dependency instead of predicting a full block at once.
            // Multi-byte samples are big-endian. Align within this command, not within the whole profile.
            int position = start + (i & -width);
            uint first;
            uint second;
            uint third;
            if (width == 1)
            {
                first = output[position - stride];
                second = output[position - (2 * stride)];
                third = output[position - (3 * stride)];
            }
            else if (width == 2)
            {
                first = BinaryPrimitives.ReadUInt16BigEndian(output[(position - stride)..]);
                second = BinaryPrimitives.ReadUInt16BigEndian(output[(position - (2 * stride))..]);
                third = BinaryPrimitives.ReadUInt16BigEndian(output[(position - (3 * stride))..]);
            }
            else
            {
                // The command boundary requires stride >= 4 and four prior samples. Every word ends before the current output byte.
                // Thus the native incomplete-word rule cannot apply after the stride check succeeds.
                first = BinaryPrimitives.ReadUInt32BigEndian(output[(position - stride)..]);
                second = BinaryPrimitives.ReadUInt32BigEndian(output[(position - (2 * stride))..]);
                third = BinaryPrimitives.ReadUInt32BigEndian(output[(position - (3 * stride))..]);
            }

            uint predicted = order switch
            {
                0 => first,
                1 => unchecked((2 * first) - second),
                _ => unchecked((3 * first) - (3 * second) + third)
            };

            int shift = (width - 1 - (i & (width - 1))) * 8;
            output[start + i] = unchecked((byte)((predicted >> shift) + source[sourcePosition]));

            // Consume shuffled residuals directly. This avoids allocating and copying a temporary array for each predictor command.
            sourcePosition += height;
            if (sourcePosition >= source.Length)
            {
                sourcePosition = ++column;
            }
        }
    }

    /// <summary>
    /// Checks an external command length before narrowing it or taking a source or destination slice.
    /// </summary>
    /// <param name="length">The encoded length.</param>
    /// <param name="available">The remaining source or destination bytes.</param>
    private static void RequireLength(ulong length, int available)
    {
        if (length > (ulong)available)
        {
            throw new InvalidImageContentException("JPEG XL compressed ICC command exceeds its data or output bounds.");
        }
    }

    /// <summary>
    /// Appends one big-endian UInt32 after checking its wire range and the declared output length.
    /// </summary>
    /// <param name="output">The final profile storage.</param>
    /// <param name="position">The next output byte, advanced by four.</param>
    /// <param name="value">The reconstructed signature, size, or offset.</param>
    private static void WriteUInt32(Span<byte> output, ref int position, ulong value)
    {
        if (value > uint.MaxValue)
        {
            throw new InvalidImageContentException("JPEG XL compressed ICC value exceeds UInt32.");
        }

        RequireLength(4, output.Length - position);
        BinaryPrimitives.WriteUInt32BigEndian(output[position..], (uint)value);
        position += 4;
    }
}
