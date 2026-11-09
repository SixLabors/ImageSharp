// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl;

/// <summary>
/// Supplies JPEG XL extra-channel samples for each frame of an image.
/// </summary>
/// <remarks>
/// <para>
/// Integer samples use normalized values from zero through one. Floating-point samples use their stored numeric values.
/// The encoder does not apply ICC conversion to these samples.
/// Black samples store remaining light: zero means full black ink and one means no black ink.
/// </para>
/// <para>
/// The caller owns the buffers and must keep them available until encoding completes.
/// The encoder reads the samples without changing or disposing the buffers.
/// </para>
/// </remarks>
public sealed class JxlExtraChannel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JxlExtraChannel"/> class.
    /// </summary>
    /// <param name="metadata">The channel type, precision, dimensions and rendering properties.</param>
    /// <param name="frames">The caller-owned sample buffers in source frame order.</param>
    /// <exception cref="ArgumentNullException">The metadata or buffer list is null.</exception>
    /// <exception cref="ArgumentException">The buffer list contains a null buffer.</exception>
    public JxlExtraChannel(JxlExtraChannelMetadata metadata, IReadOnlyList<Buffer2D<float>> frames)
    {
        Guard.NotNull(metadata, nameof(metadata));
        Guard.NotNull(frames, nameof(frames));

        // As with HEIF layers, retain a stable list of references. Sample storage stays with the caller;
        // changing the supplied list later must not change which image frame supplies a channel's samples.
        Buffer2D<float>[] copy = new Buffer2D<float>[frames.Count];

        for (int i = 0; i < copy.Length; i++)
        {
            copy[i] = frames[i] ?? throw new ArgumentException("The frame buffers cannot contain a null buffer.", nameof(frames));
        }

        this.Metadata = metadata;
        this.Frames = Array.AsReadOnly(copy);
    }

    /// <summary>
    /// Gets the channel type, precision, dimensions and rendering properties shared by all frames.
    /// </summary>
    public JxlExtraChannelMetadata Metadata { get; }

    /// <summary>
    /// Gets the caller-owned sample buffers in source frame order.
    /// </summary>
    /// <remarks>
    /// One buffer is required for each source image frame, in the same order.
    /// Each buffer has the image frame dimensions divided by two to the power of
    /// <see cref="JxlExtraChannelMetadata.DimensionShift"/>, rounded up to whole samples.
    /// Later changes to the list supplied to the constructor do not change this list.
    /// </remarks>
    public IReadOnlyList<Buffer2D<float>> Frames { get; }
}
