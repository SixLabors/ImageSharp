// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Reads bounded ISO BMFF box headers and payloads used by the HEIF image container.
/// </summary>
internal readonly struct HeifBoxReader
{
    /// <summary>
    /// The allocator used for payloads that must be materialized while parsing.
    /// </summary>
    private readonly MemoryAllocator allocator;

    /// <summary>
    /// Initializes a new instance of the <see cref="HeifBoxReader"/> struct.
    /// </summary>
    /// <param name="allocator">The allocator used for bounded payload buffers.</param>
    public HeifBoxReader(MemoryAllocator allocator) => this.allocator = allocator;

    /// <summary>
    /// Reads an ISO BMFF box header into a caller-owned buffer and returns the validated payload length.
    /// </summary>
    /// <param name="stream">The stream positioned at the box size field.</param>
    /// <param name="parentEndPosition">The absolute end position of the containing box or file.</param>
    /// <param name="headerBuffer">A caller-owned buffer of at least eight bytes that receives the header.</param>
    /// <param name="boxType">Receives the box four-character code.</param>
    /// <param name="topLevel">Indicates whether a size-zero box can extend to the end of the file.</param>
    /// <returns>The number of payload bytes following the complete variable-length header.</returns>
    public static long ReadHeader(Stream stream, long parentEndPosition, Span<byte> headerBuffer, out Heif4CharCode boxType, bool topLevel)
    {
        long length = IsoBoxReader.ReadHeader(stream, parentEndPosition, headerBuffer, out uint type, topLevel);
        boxType = (Heif4CharCode)type;
        return length;
    }

    /// <summary>
    /// Reads an ISO BMFF box header into a caller-owned buffer. The payload can end after the containing box or file.
    /// </summary>
    /// <param name="stream">The stream positioned at the box size field.</param>
    /// <param name="parentEndPosition">The absolute end position of the containing box or file.</param>
    /// <param name="headerBuffer">A caller-owned buffer of at least eight bytes that receives the header.</param>
    /// <param name="boxType">Receives the box four-character code.</param>
    /// <param name="topLevel">Indicates whether a size-zero box can extend to the end of the file.</param>
    /// <param name="truncated">Receives a value that tells if the declared payload ends after the parent end.</param>
    /// <returns>The number of payload bytes that follow the complete variable-length header inside the parent.</returns>
    public static long ReadHeader(
        Stream stream,
        long parentEndPosition,
        Span<byte> headerBuffer,
        out Heif4CharCode boxType,
        bool topLevel,
        out bool truncated)
    {
        long length = IsoBoxReader.ReadHeader(stream, parentEndPosition, headerBuffer, out uint type, topLevel, out truncated);
        boxType = (Heif4CharCode)type;
        return length;
    }

    /// <summary>
    /// Parses an ISO BMFF child-box header from a bounded parent payload.
    /// </summary>
    /// <param name="buffer">The remaining bytes in the parent payload, beginning at the child size field.</param>
    /// <param name="length">Receives the validated child payload length.</param>
    /// <param name="boxType">Receives the child box four-character code.</param>
    /// <returns>The number of bytes occupied by the complete child header.</returns>
    public static int ParseHeader(ReadOnlySpan<byte> buffer, out long length, out Heif4CharCode boxType)
    {
        int headerLength = IsoBoxReader.ParseHeader(buffer, out length, out uint type);
        boxType = (Heif4CharCode)type;
        return headerLength;
    }

    /// <summary>
    /// Finds the unique children of one box and gives the payload range of each requested child type.
    /// </summary>
    /// <remarks>
    /// The method reads only the child box headers. A parent can store its children in any order. Thus, the parser of the parent reads the children
    /// after this method returns, in the order of their dependencies. ISO/IEC 14496-12 allows a maximum of one box of each of these types in its parent.
    /// Thus, a second box of a requested type is an error.
    /// </remarks>
    /// <param name="stream">The stream, with its position at the first child box.</param>
    /// <param name="length">The number of payload bytes from the stream position to the end of the parent.</param>
    /// <param name="headerBuffer">A caller-owned buffer of a minimum of eight bytes that receives each child header.</param>
    /// <param name="types">The requested child box types.</param>
    /// <param name="children">Receives the range of each requested type, at the index of the type in <paramref name="types"/>.</param>
    /// <param name="requiredCount">The number of leading types in <paramref name="types"/> that the parent must contain.</param>
    /// <param name="parentName">The parent box name for the error messages.</param>
    /// <exception cref="InvalidImageContentException">A requested child is a duplicate, or a required child is missing.</exception>
    public static void IndexChildren(
        Stream stream,
        long length,
        Span<byte> headerBuffer,
        ReadOnlySpan<Heif4CharCode> types,
        Span<HeifBoxRange> children,
        int requiredCount,
        string parentName)
    {
        DebugGuard.IsTrue(children.Length == types.Length, nameof(children), "Each requested type needs one range.");

        // The box reader validated the parent length against its own parent and against the stream. Thus, the sum cannot overflow.
        long end = stream.Position + length;
        children.Clear();
        while (stream.Position < end)
        {
            long childLength = ReadHeader(stream, end, headerBuffer, out Heif4CharCode childType, false);
            int index = types.IndexOf(childType);
            if (index >= 0)
            {
                if (children[index].IsPresent)
                {
                    throw new InvalidImageContentException($"The {parentName} box contains duplicate '{childType}' boxes.");
                }

                children[index] = new HeifBoxRange(stream.Position, childLength, childType);
            }

            Skip(stream, childLength);
        }

        for (int i = 0; i < requiredCount; i++)
        {
            if (!children[i].IsPresent)
            {
                throw new InvalidImageContentException($"The {parentName} box has no '{types[i]}' box.");
            }
        }
    }

    /// <summary>
    /// Reads a complete bounded box payload into allocator-owned memory.
    /// </summary>
    /// <param name="stream">The stream positioned at the payload start.</param>
    /// <param name="length">The validated payload length.</param>
    /// <returns>An owner containing exactly the requested payload bytes.</returns>
    public IMemoryOwner<byte> ReadPayload(Stream stream, long length)
    {
        if ((ulong)length > int.MaxValue)
        {
            throw new InvalidImageContentException("Box content is too large to buffer.");
        }

        int bufferLength = (int)length;
        IMemoryOwner<byte> memory = this.allocator.Allocate<byte>(bufferLength);
        try
        {
            ReadExactly(stream, memory.GetSpan(), "Stream length is not sufficient for box content.");
            return memory;
        }
        catch
        {
            memory.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Reads the complete payload of one child box into allocator-owned memory. <see cref="IndexChildren"/> found this child box.
    /// </summary>
    /// <param name="stream">The seekable stream.</param>
    /// <param name="box">The payload range of the child box.</param>
    /// <returns>An owner that holds the payload bytes and no other bytes.</returns>
    public IMemoryOwner<byte> ReadPayload(Stream stream, HeifBoxRange box)
    {
        stream.Position = box.Offset;
        return this.ReadPayload(stream, box.Length);
    }

    /// <summary>
    /// Advances over a validated box payload without narrowing its 64-bit length.
    /// </summary>
    /// <param name="stream">The seekable container stream.</param>
    /// <param name="length">The validated payload length.</param>
    public static void Skip(Stream stream, long length) => stream.Seek(length, SeekOrigin.Current);

    /// <summary>
    /// Reads exactly the requested number of bytes or rejects the truncated payload.
    /// </summary>
    /// <param name="stream">The source stream.</param>
    /// <param name="destination">The complete destination span.</param>
    /// <param name="message">The malformed-image message used when the stream ends early.</param>
    public static void ReadExactly(Stream stream, Span<byte> destination, string message)
        => IsoBoxReader.ReadExactly(stream, destination, message);
}
