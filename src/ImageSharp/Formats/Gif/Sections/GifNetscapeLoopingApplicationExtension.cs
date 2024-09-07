// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;

namespace SixLabors.ImageSharp.Formats.Gif;

internal readonly struct GifNetscapeLoopingApplicationExtension : IGifExtension
{
    public GifNetscapeLoopingApplicationExtension(ushort repeatCount) => this.RepeatCount = repeatCount;

    public byte Label => GifConstants.ApplicationExtensionLabel;

    public int ContentLength => 16;

    /// <summary>
    /// Gets the repeat count.
    /// 0 means loop indefinitely. Count is set as play n + 1 times.
    /// </summary>
    public ushort RepeatCount { get; }

    public static GifNetscapeLoopingApplicationExtension Parse(ReadOnlySpan<byte> buffer)
    {
        ushort repeatCount = BinaryPrimitives.ReadUInt16LittleEndian(buffer[..2]);
        return new(repeatCount);
    }

    public int WriteTo(Span<byte> buffer)
    {
        buffer[0] = GifConstants.ApplicationBlockSize;

        // Write NETSCAPE2.0
        GifConstants.NetscapeApplicationIdentificationBytes.CopyTo(buffer.Slice(1, 11));

        // Application Data ----
        buffer[12] = 3; // Application block length (always 3)
        buffer[13] = 1; // Data sub-block identity (always 1)

        // 0 means loop indefinitely. Count is set as play n + 1 times.
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(14, 2), this.RepeatCount);

        return this.ContentLength; // Length - Introducer + Label + Terminator.
    }
}
