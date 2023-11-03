// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Webp.Chunks;

internal readonly struct WebpAnimationParameter
{
    public WebpAnimationParameter(uint background, ushort loopCount)
    {
        this.Background = background;
        this.LoopCount = loopCount;
    }

    /// <summary>
    /// Gets default background color of the canvas in [Blue, Green, Red, Alpha] byte order.
    /// This color MAY be used to fill the unused space on the canvas around the frames,
    /// as well as the transparent pixels of the first frame.
    /// The background color is also used when the Disposal method is 1.
    /// </summary>
    public uint Background { get; }

    /// <summary>
    /// Gets number of times to loop the animation. If it is 0, this means infinitely.
    /// </summary>
    public ushort LoopCount { get; }

    public void WriteTo(Stream stream)
    {
        Span<byte> buffer = stackalloc byte[6];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer[..4], this.Background);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer[4..], this.LoopCount);
        RiffHelper.WriteChunk(stream, (uint)WebpChunkType.AnimationParameter, buffer);
    }
}
