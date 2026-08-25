// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <summary>
/// Describes one component rectangle within an HEVC transform-tree node.
/// </summary>
internal readonly struct HevcTransformComponentGeometry
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HevcTransformComponentGeometry"/> struct.
    /// </summary>
    /// <param name="x">The component rectangle left coordinate.</param>
    /// <param name="y">The component rectangle top coordinate.</param>
    /// <param name="width">The component rectangle width.</param>
    /// <param name="height">The component rectangle height.</param>
    /// <param name="process">Whether this transform-tree section owns the component rectangle.</param>
    /// <param name="processesAllQuadrants">Whether every child section owns a distinct component rectangle.</param>
    public HevcTransformComponentGeometry(int x, int y, int width, int height, bool process, bool processesAllQuadrants)
    {
        this.X = x;
        this.Y = y;
        this.Width = width;
        this.Height = height;
        this.Process = process;
        this.ProcessesAllQuadrants = processesAllQuadrants;
    }

    /// <summary>
    /// Gets the component rectangle left coordinate.
    /// </summary>
    public int X { get; }

    /// <summary>
    /// Gets the component rectangle top coordinate.
    /// </summary>
    public int Y { get; }

    /// <summary>
    /// Gets the component rectangle width.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the component rectangle height.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Gets a value indicating whether this transform-tree section owns the component rectangle.
    /// </summary>
    public bool Process { get; }

    /// <summary>
    /// Gets a value indicating whether each child section owns a distinct component rectangle.
    /// </summary>
    public bool ProcessesAllQuadrants { get; }
}
