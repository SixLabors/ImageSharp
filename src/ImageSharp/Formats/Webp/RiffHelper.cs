// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Webp.Chunks;

namespace SixLabors.ImageSharp.Formats.Webp;

internal static class RiffHelper
{
    public static void WriteChunk(Stream stream, uint fourCc, ReadOnlySpan<byte> data)
    {
        long pos = BeginWriteChunk(stream, fourCc);
        stream.Write(data);
        EndWriteChunk(stream, pos);
    }

    public static void WriteChunk<TStruct>(Stream stream, uint fourCc, in TStruct chunk)
        where TStruct : unmanaged =>
        WriteChunk(stream, fourCc, MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(in chunk, 1)));

    public static long BeginWriteChunk(Stream stream, ReadOnlySpan<byte> fourCc)
    {
        // write the fourCC
        stream.Write(fourCc);

        long sizePosition = stream.Position;

        // Leaving the place for the size
        stream.Position += 4;

        return sizePosition;
    }

    public static long BeginWriteChunk(Stream stream, uint fourCc)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(buffer, fourCc);
        return BeginWriteChunk(stream, buffer);
    }

    public static long BeginWriteRiff(Stream stream, ReadOnlySpan<byte> formType)
    {
        long sizePosition = BeginWriteChunk(stream, "RIFF"u8);
        stream.Write(formType);
        return sizePosition;
    }

    public static long BeginWriteList(Stream stream, ReadOnlySpan<byte> listType)
    {
        long sizePosition = BeginWriteChunk(stream, "LIST"u8);
        stream.Write(listType);
        return sizePosition;
    }

    public static void EndWriteChunk(Stream stream, long sizePosition, int alignment = 1)
    {
        Guard.MustBeGreaterThan(alignment, 0, nameof(alignment));

        long currentPosition = stream.Position;

        uint dataSize = (uint)(currentPosition - sizePosition - 4);

        // Add padding
        while (dataSize % alignment is not 0)
        {
            stream.WriteByte(0);
            dataSize++;
            currentPosition++;
        }

        // Add the size of the encoded file to the Riff header.
        stream.Position = sizePosition;
        stream.Write(MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<uint, byte>(ref dataSize), sizeof(uint)));
        stream.Position = currentPosition;
    }

    public static void EndWriteVp8X(Stream stream, in WebpVp8X vp8X, bool updateVp8X, long initPosition)
    {
        // Jump through "RIFF" fourCC
        EndWriteChunk(stream, initPosition + 4, 2);

        // Write the VP8X chunk if necessary.
        if (updateVp8X)
        {
            long position = stream.Position;

            stream.Position = initPosition + 12;
            vp8X.WriteTo(stream);
            stream.Position = position;
        }
    }
}
