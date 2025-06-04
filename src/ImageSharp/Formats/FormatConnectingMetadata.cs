// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats;

/// <summary>
/// A metadata format designed to allow conversion between different image formats.
/// </summary>
public class FormatConnectingMetadata
{
    /// <summary>
    /// Gets the encoding type.
    /// </summary>
    public EncodingType EncodingType { get; init; }

    /// <summary>
    /// Gets the quality to use when <see cref="EncodingType"/> is <see cref="EncodingType.Lossy"/>.
    /// </summary>
    /// <remarks>
    /// The value is usually between 1 and 100. Defaults to 100.
    /// </remarks>
    public int Quality { get; init; } = 100;

    /// <summary>
    /// Gets information about the encoded pixel type.
    /// </summary>
    public PixelTypeInfo PixelTypeInfo { get; init; }

    /// <summary>
    /// Gets the shared color table mode.
    /// </summary>
    /// <remarks>
    /// Defaults to <see cref="FrameColorTableMode.Global"/>.
    /// </remarks>
    public FrameColorTableMode ColorTableMode { get; init; } = FrameColorTableMode.Global;

    /// <summary>
    /// Gets the default background color of the canvas when animating.
    /// This color may be used to fill the unused space on the canvas around the frames,
    /// as well as the transparent pixels of the first frame.
    /// The background color is also used when a frame disposal mode is <see cref="FrameDisposalMode.RestoreToBackground"/>.
    /// </summary>
    /// <remarks>
    /// Defaults to <see cref="Color.Transparent"/>.
    /// </remarks>
    public Color BackgroundColor { get; init; } = Color.Transparent;

    /// <summary>
    /// Gets the number of times any animation is repeated.
    /// </summary>
    /// <remarks>
    /// 0 means to repeat indefinitely, count is set as repeat n-1 times. Defaults to 1.
    /// </remarks>
    public ushort RepeatCount { get; init; } = 1;

    /// <summary>
    /// Gets a value indicating whether the root frame is shown as part of the animated sequence.
    /// </summary>
    /// <remarks>
    /// Defaults to <see langword="true"/>.
    /// </remarks>
    public bool AnimateRootFrame { get; init; } = true;
}
