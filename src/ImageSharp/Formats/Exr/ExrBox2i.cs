// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics;

namespace SixLabors.ImageSharp.Formats.Exr;

/// <summary>
/// Integer region definition.
/// </summary>
[DebuggerDisplay("xMin: {XMin}, yMin: {YMin}, xMax: {XMax}, yMax: {YMax}")]
internal readonly struct ExrBox2i
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExrBox2i"/> struct.
    /// </summary>
    /// <param name="xMin">The minimum x value.</param>
    /// <param name="yMin">The minimum y value.</param>
    /// <param name="xMax">The maximum x value.</param>
    /// <param name="yMax">The maximum y value.</param>
    public ExrBox2i(int xMin, int yMin, int xMax, int yMax)
    {
        this.XMin = xMin;
        this.YMin = yMin;
        this.XMax = xMax;
        this.YMax = yMax;
    }

    /// <summary>
    /// Gets the minimum x value.
    /// </summary>
    public int XMin { get; }

    /// <summary>
    /// Gets the minimum y value.
    /// </summary>
    public int YMin { get; }

    /// <summary>
    /// Gets the maximum x value.
    /// </summary>
    public int XMax { get; }

    /// <summary>
    /// Gets the maximum y value.
    /// </summary>
    public int YMax { get; }
}
