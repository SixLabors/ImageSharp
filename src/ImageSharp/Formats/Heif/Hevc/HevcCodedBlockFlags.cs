// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <summary>
/// Contains one or two coded-block flags for a square or vertically split HEVC component transform section.
/// </summary>
internal readonly struct HevcCodedBlockFlags
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HevcCodedBlockFlags"/> struct for one square block.
    /// </summary>
    /// <param name="first">The square block's coded-block flag.</param>
    public HevcCodedBlockFlags(bool first)
    {
        this.First = first;
        this.Second = false;
        this.IsSplit = false;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HevcCodedBlockFlags"/> struct for two rectangular sub-blocks.
    /// </summary>
    /// <param name="first">The first square sub-block's coded-block flag.</param>
    /// <param name="second">The second square sub-block's coded-block flag.</param>
    public HevcCodedBlockFlags(bool first, bool second)
    {
        this.First = first;
        this.Second = second;
        this.IsSplit = true;
    }

    /// <summary>
    /// Gets a value indicating whether the first or only coefficient block contains coded residual data.
    /// </summary>
    public bool First { get; }

    /// <summary>
    /// Gets a value indicating whether the second rectangular sub-block contains coded residual data.
    /// </summary>
    public bool Second { get; }

    /// <summary>
    /// Gets a value indicating whether two square sub-block flags are present.
    /// </summary>
    public bool IsSplit { get; }

    /// <summary>
    /// Gets a value indicating whether either governed coefficient block contains coded residual data.
    /// </summary>
    public bool Any => this.First || this.Second;
}
