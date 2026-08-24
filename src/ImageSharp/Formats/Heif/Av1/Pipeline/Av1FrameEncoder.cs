// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Defines the work-in-progress AV1 still-image frame-encoding boundary.
/// </summary>
internal class Av1FrameEncoder
{
    /// <summary>
    /// The source plane samples supplied for frame encoding.
    /// </summary>
    private readonly Av1FrameBuffer<byte> frameBuffer;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1FrameEncoder"/> class.
    /// </summary>
    /// <param name="frameBuffer">The source frame samples to encode.</param>
    public Av1FrameEncoder(Av1FrameBuffer<byte> frameBuffer)
    {
        this.frameBuffer = frameBuffer;
    }

    /// <summary>
    /// Represents the not-yet-implemented entry point for encoding one AV1 still-image frame.
    /// </summary>
    /// <remarks>SVT-AV1: <c>svt_av1_enc_init</c>.</remarks>
    public static void Encode()
    {
        // Still-image encoding needs the normative analysis, transform, quantization, entropy, and packetization stages,
        // but it does not require SVT-AV1's application-level worker graph or video-sequence process orchestration.
    }
}
