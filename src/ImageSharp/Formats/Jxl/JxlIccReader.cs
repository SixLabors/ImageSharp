// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Formats.Jxl;

/// <summary>
/// Reconstructs embedded ICC bytes from JPEG XL's entropy-coded prediction commands.
/// </summary>
/// <remarks>
/// The two stages follow libjxl's icc_codec.cc and icc_codec_common.cc.
/// Scratch storage uses the decoder allocator. The final array belongs to the shared ICC profile.
/// </remarks>
internal static class JxlIccReader
{
    private const int HeaderSize = 128;
    private const int ContextCount = 41;

    // libjxl's CheckPreamble rejects a prediction stream more than 65536 bytes larger than its
    // reconstructed profile. Combine that bound with the shared profile-size limit before allocating scratch.
    private const int MaximumPredictionOverhead = 65536;

    // JPEG XL's prediction dictionary includes kXYZ and gbd keywords even though the shared ICC enums do not name them.
    private const uint BlackMatrixColumnSignature = 0x6B58595A;
    private const uint GamutBoundaryDescriptionSignature = 0x67626420;

    /// <summary>
    /// Reads and reconstructs one embedded profile without changing its sample tables or numeric precision.
    /// </summary>
    /// <param name="reader">The bit reader positioned at the compressed profile length.</param>
    /// <param name="allocator">The decoder's scratch allocator.</param>
    /// <returns>The shared profile owning the exact reconstructed bytes.</returns>
    public static IccProfile Read(ref JxlBitReader reader, MemoryAllocator allocator)
    {
        ulong size = reader.ReadU64();
        if (size > IccProfile.MaxProfileSize + MaximumPredictionOverhead || size < 2)
        {
            throw new InvalidImageContentException("The JPEG XL encoded ICC length is invalid.");
        }

        using JxlEntropyDecoder codes = JxlEntropyDecoder.Read(ref reader, allocator, ContextCount, true);
        JxlEntropyDecoder.Reader symbols = codes.CreateReader(ref reader);
        InlineArray32<byte> prefixStorage = default;
        Span<byte> prefix = prefixStorage;
        int prefixSize = Math.Min((int)size, 20);
        for (int i = 0; i < prefixSize; i++)
        {
            prefix[i] = symbols.ReadByte(ref reader, 0, prefix[..i]);
        }

        // Two base-128 integers require at most twenty bytes. Validate the decoded allocation sizes
        // before renting the complete prediction stream or allocating the final profile.
        ReadOnlySpan<byte> preamble = prefix[..prefixSize];
        ulong outputLength = ReadVarUInt(ref preamble);
        ulong commandLength = ReadVarUInt(ref preamble);
        int preambleSize = prefixSize - preamble.Length;
        if (outputLength < HeaderSize || outputLength >= IccProfile.MaxProfileSize || commandLength > size - (uint)preambleSize
            || outputLength + MaximumPredictionOverhead < size)
        {
            throw new InvalidImageContentException("The JPEG XL ICC prediction preamble is invalid.");
        }

        using IMemoryOwner<byte> storage = allocator.Allocate<byte>((int)size);
        Span<byte> encoded = storage.Memory.Span;
        prefix[..prefixSize].CopyTo(encoded);
        for (int i = prefixSize; i < encoded.Length; i++)
        {
            // The first 129 bytes use context zero. Later contexts classify the two preceding bytes;
            // the stored output also supplies LZ77 history, without an additional one-megavalue buffer.
            int context = i <= HeaderSize ? 0 : 1 + ByteKind1(encoded[i - 1]) + (8 * ByteKind2(encoded[i - 2]));
            encoded[i] = symbols.ReadByte(ref reader, context, encoded[..i]);
        }

        symbols.CheckFinalState();
        return new IccProfile(Reconstruct(encoded, (int)outputLength, (int)commandLength, preambleSize));
    }

    /// <summary>
    /// Reconstructs the profile into one final array. Prediction and byte shuffles write directly to that array.
    /// </summary>
    /// <param name="encoded">The complete entropy-decoded prediction stream.</param>
    /// <param name="outputLength">The validated profile size.</param>
    /// <param name="commandLength">The validated command-stream size.</param>
    /// <param name="preambleSize">The number of bytes occupied by both size fields.</param>
    /// <returns>The owned profile bytes.</returns>
    private static byte[] Reconstruct(ReadOnlySpan<byte> encoded, int outputLength, int commandLength, int preambleSize)
    {
        ReadOnlySpan<byte> commands = encoded.Slice(preambleSize, commandLength);
        ReadOnlySpan<byte> data = encoded[(preambleSize + commandLength)..];
        byte[] result = new byte[outputLength];
        Span<byte> output = result;
        ReadOnlySpan<byte> headerData = Take(ref data, HeaderSize);
        for (int i = 0; i < HeaderSize; i++)
        {
            byte prediction = i switch
            {
                <= 3 => (byte)(outputLength >> (24 - (8 * i))),
                8 => 4,
                >= 12 and < 24 => "mntrRGB XYZ "u8[i - 12],
                >= 36 and < 40 => "acsp"u8[i - 36],
                41 or 42 when output[40] == (byte)'A' => (byte)'P',
                43 when output[40] == (byte)'A' => (byte)'L',
                41 when output[40] == (byte)'M' => (byte)'S',
                42 when output[40] == (byte)'M' => (byte)'F',
                43 when output[40] == (byte)'M' => (byte)'T',
                42 when output[40] == (byte)'S' && output[41] == (byte)'G' => (byte)'I',
                43 when output[40] == (byte)'S' && output[41] == (byte)'G' => (byte)' ',
                42 when output[40] == (byte)'S' && output[41] == (byte)'U' => (byte)'N',
                43 when output[40] == (byte)'S' && output[41] == (byte)'U' => (byte)'W',
                70 => 246,
                71 => 214,
                73 => 1,
                78 => 211,
                79 => 45,
                >= 80 and < 84 => output[i - 76],
                _ => 0
            };

            output[i] = unchecked((byte)(prediction + headerData[i]));
        }

        int written = HeaderSize;
        if (written < output.Length)
        {
            ReadTags(ref commands, ref data, output, ref written);
        }

        ReadOnlySpan<uint> types =
        [
            (uint)IccTypeSignature.Xyz, (uint)IccTypeSignature.TextDescription, (uint)IccTypeSignature.Text,
            (uint)IccTypeSignature.MultiLocalizedUnicode, (uint)IccTypeSignature.ParametricCurve, (uint)IccTypeSignature.Curve,
            (uint)IccTypeSignature.S15Fixed16Array, GamutBoundaryDescriptionSignature
        ];

        while (!commands.IsEmpty)
        {
            byte command = Take(ref commands, 1)[0];
            if (command is >= 1 and <= 4)
            {
                int width = command == 2 ? 2 : command == 3 ? 4 : 1;
                int order = -1;
                int stride = width;
                if (command == 4)
                {
                    byte flags = Take(ref commands, 1)[0];
                    width = (flags & 3) + 1;
                    order = (flags >> 2) & 3;
                    ulong encodedStride = (flags & 16) != 0 ? ReadVarUInt(ref commands) : (uint)width;
                    if (width == 3 || order == 3 || encodedStride < (uint)width || encodedStride > (uint)((written - 1) / 4))
                    {
                        throw new InvalidImageContentException("The JPEG XL ICC predictor parameters are invalid.");
                    }

                    stride = (int)encodedStride;
                }

                ulong count = ReadVarUInt(ref commands);
                if (count > (ulong)(output.Length - written))
                {
                    throw new InvalidImageContentException("The JPEG XL ICC command exceeds the declared profile size.");
                }

                Span<byte> destination = output.Slice(written, (int)count);
                ReadOnlySpan<byte> residuals = Take(ref data, (int)count);
                Unshuffle(residuals, destination, width);
                if (order >= 0)
                {
                    ApplyPrediction(output, written, (int)count, width, stride, order);
                }

                written += (int)count;
            }
            else if (command == 10 || command is >= 16 and < 24)
            {
                int count = command == 10 ? 20 : 8;
                if (count > output.Length - written)
                {
                    throw new InvalidImageContentException("The JPEG XL ICC type command exceeds the profile size.");
                }

                uint signature = command == 10 ? (uint)IccTypeSignature.Xyz : types[command - 16];
                BinaryPrimitives.WriteUInt32BigEndian(output[written..], signature);
                output.Slice(written + 4, 4).Clear();
                if (command == 10)
                {
                    Take(ref data, 12).CopyTo(output[(written + 8)..]);
                }

                written += count;
            }
            else
            {
                throw new InvalidImageContentException("The JPEG XL ICC prediction command is invalid.");
            }
        }

        if (written != output.Length || !data.IsEmpty)
        {
            throw new InvalidImageContentException("The JPEG XL ICC prediction stream does not match its declared size.");
        }

        return result;
    }

    /// <summary>
    /// Reconstructs the ICC tag directory, including commands that describe all three matrix or curve tags at once.
    /// </summary>
    /// <param name="commands">The remaining command bytes.</param>
    /// <param name="data">The remaining literal bytes.</param>
    /// <param name="output">The complete destination profile.</param>
    /// <param name="written">The current output position.</param>
    private static void ReadTags(ref ReadOnlySpan<byte> commands, ref ReadOnlySpan<byte> data, Span<byte> output, ref int written)
    {
        ulong count = ReadVarUInt(ref commands);
        if (count == 0)
        {
            return;
        }

        count--;
        if (count > (ulong)((output.Length - HeaderSize) / 12) || output.Length - written < 4)
        {
            throw new InvalidImageContentException("The JPEG XL ICC tag directory exceeds the profile size.");
        }

        BinaryPrimitives.WriteUInt32BigEndian(output[written..], (uint)count);
        written += 4;
        ulong previousOffset = HeaderSize + (count * 12);
        ulong previousSize = 0;
        ReadOnlySpan<uint> tags =
        [
            (uint)IccProfileTag.Copyright, (uint)IccProfileTag.MediaWhitePoint, (uint)IccProfileTag.MediaBlackPoint,
            (uint)IccProfileTag.RedMatrixColumn, (uint)IccProfileTag.GreenMatrixColumn, (uint)IccProfileTag.BlueMatrixColumn,
            BlackMatrixColumnSignature, (uint)IccProfileTag.RedTrc, (uint)IccProfileTag.GreenTrc, (uint)IccProfileTag.BlueTrc,
            (uint)IccProfileTag.GrayTrc, (uint)IccProfileTag.ChromaticAdaptation, (uint)IccProfileTag.ProfileDescription,
            (uint)IccProfileTag.Chromaticity, (uint)IccProfileTag.DeviceManufacturerDescription,
            (uint)IccProfileTag.DeviceModelDescription, (uint)IccProfileTag.Luminance
        ];

        while (!commands.IsEmpty)
        {
            byte command = Take(ref commands, 1)[0];
            int code = command & 63;
            if (code == 0)
            {
                break;
            }

            if (code >= tags.Length + 4)
            {
                throw new InvalidImageContentException("The JPEG XL ICC tag command is invalid.");
            }

            uint tag = code switch
            {
                1 => BinaryPrimitives.ReadUInt32BigEndian(Take(ref data, 4)),
                2 => (uint)IccProfileTag.RedTrc,
                3 => (uint)IccProfileTag.RedMatrixColumn,
                _ => tags[code - 4]
            };

            ulong offset = (command & 64) != 0 ? ReadVarUInt(ref commands) : previousOffset + previousSize;
            bool xyz = tag is (uint)IccProfileTag.RedMatrixColumn or (uint)IccProfileTag.GreenMatrixColumn or (uint)IccProfileTag.BlueMatrixColumn
                or BlackMatrixColumnSignature or (uint)IccProfileTag.MediaWhitePoint or (uint)IccProfileTag.MediaBlackPoint or (uint)IccProfileTag.Luminance;

            ulong size = (command & 128) != 0 ? ReadVarUInt(ref commands) : xyz ? 20UL : previousSize;
            int entries = code is 2 or 3 ? 3 : 1;
            if (offset > uint.MaxValue || size > uint.MaxValue || (code == 3 && size > (uint.MaxValue - offset) / 2)
                || (12 * entries) > output.Length - written)
            {
                throw new InvalidImageContentException("The JPEG XL ICC tag entry is out of range.");
            }

            WriteTag(output.Slice(written, 12), tag, (uint)offset, (uint)size);
            written += 12;
            if (entries == 3)
            {
                uint green = code == 2 ? (uint)IccProfileTag.GreenTrc : (uint)IccProfileTag.GreenMatrixColumn;
                uint blue = code == 2 ? (uint)IccProfileTag.BlueTrc : (uint)IccProfileTag.BlueMatrixColumn;
                WriteTag(output.Slice(written, 12), green, (uint)(offset + (code == 3 ? size : 0)), (uint)size);
                WriteTag(output.Slice(written + 12, 12), blue, (uint)(offset + (code == 3 ? 2 * size : 0)), (uint)size);
                written += 24;
            }

            previousOffset = offset;
            previousSize = size;
        }
    }

    /// <summary>
    /// Writes one big-endian ICC directory entry through the shared byte primitives.
    /// </summary>
    /// <param name="entry">The twelve-byte destination.</param>
    /// <param name="tag">The ICC tag signature.</param>
    /// <param name="offset">The tag's absolute byte offset.</param>
    /// <param name="size">The tag's byte length.</param>
    private static void WriteTag(Span<byte> entry, uint tag, uint offset, uint size)
    {
        BinaryPrimitives.WriteUInt32BigEndian(entry, tag);
        BinaryPrimitives.WriteUInt32BigEndian(entry[4..], offset);
        BinaryPrimitives.WriteUInt32BigEndian(entry[8..], size);
    }

    /// <summary>
    /// Restores byte order from the codec's planar predictor layout, including incomplete final columns.
    /// </summary>
    /// <param name="source">The residual byte planes.</param>
    /// <param name="destination">The interleaved residuals.</param>
    /// <param name="width">One, two or four bytes per predicted integer.</param>
    private static void Unshuffle(ReadOnlySpan<byte> source, Span<byte> destination, int width)
    {
        if (width == 1)
        {
            source.CopyTo(destination);
            return;
        }

        int height = (source.Length + width - 1) / width;
        int completeColumns = Math.Max(0, source.Length - ((width - 1) * height));
        int column = 0;
        ref byte first = ref MemoryMarshal.GetReference(source);
        ref byte output = ref MemoryMarshal.GetReference(destination);
        if (Vector512.IsHardwareAccelerated)
        {
            for (; column <= completeColumns - Vector512<byte>.Count; column += Vector512<byte>.Count)
            {
                Vector512<byte> x = Vector512.LoadUnsafe(ref first, (uint)column);
                Vector512<byte> y = Vector512.LoadUnsafe(ref first, (uint)(column + height));
                if (width == 2)
                {
                    SimdUtils.Interleave2(x, y, ref Unsafe.Add(ref output, column * width));
                }
                else
                {
                    Vector512<byte> z = Vector512.LoadUnsafe(ref first, (uint)(column + (2 * height)));
                    Vector512<byte> w = Vector512.LoadUnsafe(ref first, (uint)(column + (3 * height)));
                    SimdUtils.Interleave4(x, y, z, w, ref Unsafe.Add(ref output, column * width));
                }
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            for (; column <= completeColumns - Vector256<byte>.Count; column += Vector256<byte>.Count)
            {
                Vector256<byte> x = Vector256.LoadUnsafe(ref first, (uint)column);
                Vector256<byte> y = Vector256.LoadUnsafe(ref first, (uint)(column + height));
                if (width == 2)
                {
                    SimdUtils.Interleave2(x, y, ref Unsafe.Add(ref output, column * width));
                }
                else
                {
                    Vector256<byte> z = Vector256.LoadUnsafe(ref first, (uint)(column + (2 * height)));
                    Vector256<byte> w = Vector256.LoadUnsafe(ref first, (uint)(column + (3 * height)));
                    SimdUtils.Interleave4(x, y, z, w, ref Unsafe.Add(ref output, column * width));
                }
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            for (; column <= completeColumns - Vector128<byte>.Count; column += Vector128<byte>.Count)
            {
                Vector128<byte> x = Vector128.LoadUnsafe(ref first, (uint)column);
                Vector128<byte> y = Vector128.LoadUnsafe(ref first, (uint)(column + height));
                if (width == 2)
                {
                    SimdUtils.Interleave2(x, y, ref Unsafe.Add(ref output, column * width));
                }
                else
                {
                    Vector128<byte> z = Vector128.LoadUnsafe(ref first, (uint)(column + (2 * height)));
                    Vector128<byte> w = Vector128.LoadUnsafe(ref first, (uint)(column + (3 * height)));
                    SimdUtils.Interleave4(x, y, z, w, ref Unsafe.Add(ref output, column * width));
                }
            }
        }

        // Only complete columns enter SIMD. The format permits incomplete final columns, including
        // a command shorter than one four-byte sample. Continue its exact row-stride permutation here.
        int start = column;
        int index = column;
        for (int i = column * width; i < destination.Length; i++)
        {
            destination[i] = source[index];
            index += height;
            if (index >= source.Length)
            {
                index = ++start;
            }
        }
    }

    /// <summary>
    /// Adds first-, second- or third-sample finite-difference predictions modulo the encoded integer width.
    /// </summary>
    /// <param name="output">The complete output, including earlier reconstructed samples.</param>
    /// <param name="start">The beginning of this command's residuals.</param>
    /// <param name="count">The residual byte count.</param>
    /// <param name="width">The integer byte width.</param>
    /// <param name="stride">The byte distance between predicted samples.</param>
    /// <param name="order">Zero for the preceding value, one for linear prediction, or two for quadratic prediction.</param>
    private static void ApplyPrediction(Span<byte> output, int start, int count, int width, int stride, int order)
    {
        for (int i = 0; i < count; i += width)
        {
            int position = start + i;
            uint first = ReadPredictionSample(output, position - stride, width);
            uint second = ReadPredictionSample(output, position - (2 * stride), width);
            uint third = ReadPredictionSample(output, position - (3 * stride), width);
            uint prediction = unchecked(order switch
            {
                0 => first,
                1 => (2 * first) - second,
                _ => (3 * first) - (3 * second) + third
            });

            // Prediction works on big-endian integers, but residual addition works on individual bytes.
            // A carry from one residual byte must not change the next byte's predicted value.
            int bytes = Math.Min(width, count - i);
            for (int j = 0; j < bytes; j++)
            {
                int shift = (width - j - 1) * 8;
                output[position + j] = unchecked((byte)(output[position + j] + (byte)(prediction >> shift)));
            }
        }
    }

    /// <summary>
    /// Reads one prior big-endian predictor sample. The command parser validates its stride and available history.
    /// </summary>
    /// <param name="output">The reconstructed profile.</param>
    /// <param name="position">The sample offset.</param>
    /// <param name="width">The integer byte width.</param>
    /// <returns>The unsigned sample value.</returns>
    private static uint ReadPredictionSample(ReadOnlySpan<byte> output, int position, int width) => width switch
    {
        1 => output[position],
        2 => BinaryPrimitives.ReadUInt16BigEndian(output[position..]),
        _ => BinaryPrimitives.ReadUInt32BigEndian(output[position..])
    };

    /// <summary>
    /// Reads one base-128 unsigned integer with an exact ten-byte limit.
    /// </summary>
    /// <param name="source">The remaining command or preamble bytes.</param>
    /// <returns>The decoded value.</returns>
    private static ulong ReadVarUInt(ref ReadOnlySpan<byte> source)
    {
        ulong value = 0;
        for (int i = 0; i < 10; i++)
        {
            byte next = Take(ref source, 1)[0];
            if (i == 9 && next > 1)
            {
                throw new InvalidImageContentException("The JPEG XL ICC integer exceeds 64 bits.");
            }

            value |= (ulong)(next & 127) << (i * 7);
            if ((next & 128) == 0)
            {
                return value;
            }
        }

        throw new InvalidImageContentException("The JPEG XL ICC integer is not terminated.");
    }

    /// <summary>
    /// Takes a bounded range from one prediction stream and advances it without copying.
    /// </summary>
    /// <param name="source">The remaining bytes.</param>
    /// <param name="count">The validated requested length.</param>
    /// <returns>The borrowed range.</returns>
    private static ReadOnlySpan<byte> Take(ref ReadOnlySpan<byte> source, int count)
    {
        if (count > source.Length)
        {
            throw new InvalidImageContentException("The JPEG XL ICC command exceeds its prediction stream.");
        }

        ReadOnlySpan<byte> result = source[..count];
        source = source[count..];
        return result;
    }

    /// <summary>
    /// Classifies the immediately preceding prediction byte into one of eight entropy contexts.
    /// </summary>
    /// <param name="value">The preceding byte.</param>
    /// <returns>The first context contribution.</returns>
    private static int ByteKind1(byte value) => value switch
    {
        >= (byte)'a' and <= (byte)'z' or >= (byte)'A' and <= (byte)'Z' => 0,
        >= (byte)'0' and <= (byte)'9' or (byte)'.' or (byte)',' => 1,
        0 => 2,
        1 => 3,
        < 16 => 4,
        255 => 6,
        > 240 => 5,
        _ => 7
    };

    /// <summary>
    /// Classifies the second preceding prediction byte into one of five entropy contexts.
    /// </summary>
    /// <param name="value">The second preceding byte.</param>
    /// <returns>The second context contribution.</returns>
    private static int ByteKind2(byte value) => value switch
    {
        >= (byte)'a' and <= (byte)'z' or >= (byte)'A' and <= (byte)'Z' => 0,
        >= (byte)'0' and <= (byte)'9' or (byte)'.' or (byte)',' => 1,
        < 16 => 2,
        > 240 => 3,
        _ => 4
    };
}
