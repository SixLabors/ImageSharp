// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder;

/// <summary>
/// Interface for a JPEG scan decoder.
/// </summary>
internal interface IJpegScanDecoder
{
    /// <summary>
    /// Sets the reset interval.
    /// </summary>
    public int ResetInterval { set; }

    /// <summary>
    /// Gets or sets the spectral selection start.
    /// </summary>
    public int SpectralStart { get; set; }

    /// <summary>
    /// Gets or sets the spectral selection end.
    /// </summary>
    public int SpectralEnd { get; set; }

    /// <summary>
    /// Gets or sets the successive approximation high bit end.
    /// </summary>
    public int SuccessiveHigh { get; set; }

    /// <summary>
    /// Gets or sets the successive approximation low bit end.
    /// </summary>
    public int SuccessiveLow { get; set; }

    /// <summary>
    /// Decodes the entropy coded data.
    /// </summary>
    /// <param name="scanComponentCount">Component count in the current scan.</param>
    /// <param name="iccProfile">
    /// The ICC profile to use for color conversion. If null, the default color space.
    /// </param>
    public void ParseEntropyCodedData(int scanComponentCount, IccProfile? iccProfile);

    /// <summary>
    /// Sets the JpegFrame and its components and injects the frame data into the spectral converter.
    /// </summary>
    /// <param name="frame">The frame.</param>
    /// <param name="jpegData">The raw JPEG data.</param>
    public void InjectFrameData(JpegFrame frame, IRawJpegData jpegData);
}
