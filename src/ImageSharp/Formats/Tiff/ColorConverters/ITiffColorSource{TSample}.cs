// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Tiff.ColorConverters;

/// <summary>
/// Reads byte-aligned TIFF pixels or separate component planes.
/// </summary>
/// <typeparam name="TSample">The decoded component type.</typeparam>
internal interface ITiffColorSource<TSample> : ITiffComponentSource<TSample>
    where TSample : unmanaged
{
    /// <summary>
    /// Gets the source byte count for one pixel in each supplied span: all components when packed, one component when planar.
    /// </summary>
    int BytesPerPixel { get; }
}
