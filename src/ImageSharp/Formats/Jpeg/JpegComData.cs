// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jpeg;

/// <summary>
/// Represents a JPEG comment
/// </summary>
public readonly struct JpegComData
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JpegComData"/> struct.
    /// </summary>
    /// <param name="value">The comment buffer.</param>
    public JpegComData(ReadOnlyMemory<char> value)
        => this.Value = value;

    /// <summary>
    /// Gets the value.
    /// </summary>
    public ReadOnlyMemory<char> Value { get; }

    /// <summary>
    /// Converts string to <see cref="JpegComData"/>
    /// </summary>
    /// <param name="value">The comment string.</param>
    /// <returns>The <see cref="JpegComData"/></returns>
    public static JpegComData FromString(string value) => new(value.AsMemory());

    /// <inheritdoc/>
    public override string ToString() => this.Value.ToString();
}
