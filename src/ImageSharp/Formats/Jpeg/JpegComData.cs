// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jpeg;

/// <summary>
/// Contains JPEG comment
/// </summary>
public readonly struct JpegComData
{
    /// <summary>
    /// Converts string to <see cref="JpegComData"/>
    /// </summary>
    /// <param name="value">The comment string.</param>
    /// <returns>The <see cref="JpegComData"/></returns>
    public static JpegComData FromString(string value) => new(value.AsMemory());

    /// <summary>
    /// Initializes a new instance of the <see cref="JpegComData"/> struct.
    /// </summary>
    /// <param name="value">The comment ReadOnlyMemory of chars.</param>
    public JpegComData(ReadOnlyMemory<char> value)
        => this.Value = value;

    public ReadOnlyMemory<char> Value { get; }

    /// <summary>
    /// Converts Value to string
    /// </summary>
    /// <returns>The comment string.</returns>
    public override string ToString() => this.Value.ToString();
}
