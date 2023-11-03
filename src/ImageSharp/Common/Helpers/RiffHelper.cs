// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Text;

namespace SixLabors.ImageSharp.Common.Helpers;

internal static class RiffHelper
{
    /// <summary>
    /// The header bytes identifying RIFF file.
    /// </summary>
    private const uint RiffFourCc = 0x52_49_46_46;

    public static void WriteRiffFile(Stream stream, string formType, Action<Stream> func) =>
        WriteChunk(stream, RiffFourCc, s =>
        {
            s.Write(Encoding.ASCII.GetBytes(formType));
            func(s);
        });

    public static void WriteChunk(Stream stream, uint fourCc, Action<Stream> func)
    {
        Span<byte> buffer = stackalloc byte[4];

        // write the fourCC
        BinaryPrimitives.WriteUInt32BigEndian(buffer, fourCc);
        stream.Write(buffer);

        long sizePosition = stream.Position;
        stream.Position += 4;

        func(stream);

        long position = stream.Position;

        uint dataSize = (uint)(position - sizePosition - 4);

        // padding
        if (dataSize % 2 == 1)
        {
            stream.WriteByte(0);
            position++;
        }

        BinaryPrimitives.WriteUInt32LittleEndian(buffer, dataSize);
        stream.Position = sizePosition;
        stream.Write(buffer);
        stream.Position = position;
    }

    public static void WriteChunk(Stream stream, uint fourCc, ReadOnlySpan<byte> data)
    {
        Span<byte> buffer = stackalloc byte[4];

        // write the fourCC
        BinaryPrimitives.WriteUInt32BigEndian(buffer, fourCc);
        stream.Write(buffer);
        uint size = (uint)data.Length;
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, size);
        stream.Write(buffer);
        stream.Write(data);

        // padding
        if (size % 2 is 1)
        {
            stream.WriteByte(0);
        }
    }

    public static unsafe void WriteChunk<TStruct>(Stream stream, uint fourCc, in TStruct chunk)
        where TStruct : unmanaged
    {
        fixed (TStruct* ptr = &chunk)
        {
            WriteChunk(stream, fourCc, new Span<byte>(ptr, sizeof(TStruct)));
        }
    }

    public static long BeginWriteChunk(Stream stream, uint fourCc)
    {
        Span<byte> buffer = stackalloc byte[4];

        // write the fourCC
        BinaryPrimitives.WriteUInt32BigEndian(buffer, fourCc);
        stream.Write(buffer);

        long sizePosition = stream.Position;
        stream.Position += 4;

        return sizePosition;
    }

    public static void EndWriteChunk(Stream stream, long sizePosition)
    {
        Span<byte> buffer = stackalloc byte[4];

        long position = stream.Position;

        uint dataSize = (uint)(position - sizePosition - 4);

        // padding
        if (dataSize % 2 is 1)
        {
            stream.WriteByte(0);
            position++;
        }

        BinaryPrimitives.WriteUInt32LittleEndian(buffer, dataSize);
        stream.Position = sizePosition;
        stream.Write(buffer);
        stream.Position = position;
    }

    public static long BeginWriteRiffFile(Stream stream, string formType)
    {
        long sizePosition = BeginWriteChunk(stream, RiffFourCc);
        stream.Write(Encoding.ASCII.GetBytes(formType));
        return sizePosition;
    }

    public static void EndWriteRiffFile(Stream stream, long sizePosition) => EndWriteChunk(stream, sizePosition);
}
