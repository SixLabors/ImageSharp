// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.IO;

namespace SixLabors.ImageSharp.Formats.Webp;

internal struct AnimationFrameData
{
    /// <summary>
    /// The animation chunk size.
    /// </summary>
    public uint DataSize;

    /// <summary>
    /// X(3) + Y(3) + Width(3) + Height(3) + Duration(3) + 1 byte for flags.
    /// </summary>
    public const uint HeaderSize = 16;

    /// <summary>
    /// The X coordinate of the upper left corner of the frame is Frame X * 2.
    /// </summary>
    public uint X;

    /// <summary>
    /// The Y coordinate of the upper left corner of the frame is Frame Y * 2.
    /// </summary>
    public uint Y;

    /// <summary>
    /// The width of the frame.
    /// </summary>
    public uint Width;

    /// <summary>
    /// The height of the frame.
    /// </summary>
    public uint Height;

    /// <summary>
    /// The time to wait before displaying the next frame, in 1 millisecond units.
    /// Note the interpretation of frame duration of 0 (and often smaller then 10) is implementation defined.
    /// </summary>
    public uint Duration;

    /// <summary>
    /// Indicates how transparent pixels of the current frame are to be blended with corresponding pixels of the previous canvas.
    /// </summary>
    public AnimationBlendingMethod BlendingMethod;

    /// <summary>
    /// Indicates how the current frame is to be treated after it has been displayed (before rendering the next frame) on the canvas.
    /// </summary>
    public AnimationDisposalMethod DisposalMethod;

    /// <summary>
    /// Reads the animation frame header.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <returns>Animation frame data.</returns>
    public static AnimationFrameData Parse(BufferedReadStream stream)
    {
        Span<byte> buffer = stackalloc byte[4];

        AnimationFrameData data = new AnimationFrameData
        {
            DataSize = WebpChunkParsingUtils.ReadChunkSize(stream, buffer),

            // 3 bytes for the X coordinate of the upper left corner of the frame.
            X = WebpChunkParsingUtils.ReadUInt24LittleEndian(stream, buffer),

            // 3 bytes for the Y coordinate of the upper left corner of the frame.
            Y = WebpChunkParsingUtils.ReadUInt24LittleEndian(stream, buffer),

            // Frame width Minus One.
            Width = WebpChunkParsingUtils.ReadUInt24LittleEndian(stream, buffer) + 1,

            // Frame height Minus One.
            Height = WebpChunkParsingUtils.ReadUInt24LittleEndian(stream, buffer) + 1,

            // Frame duration.
            Duration = WebpChunkParsingUtils.ReadUInt24LittleEndian(stream, buffer)
        };

        byte flags = (byte)stream.ReadByte();
        data.DisposalMethod = (flags & 1) == 1 ? AnimationDisposalMethod.Dispose : AnimationDisposalMethod.DoNotDispose;
        data.BlendingMethod = (flags & (1 << 1)) != 0 ? AnimationBlendingMethod.DoNotBlend : AnimationBlendingMethod.AlphaBlending;

        return data;
    }
}
