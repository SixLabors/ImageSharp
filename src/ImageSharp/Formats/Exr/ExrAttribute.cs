// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics;

namespace SixLabors.ImageSharp.Formats.Exr;

/// <summary>
/// Repressents an exr image attribute.
/// </summary>
[DebuggerDisplay("Name: {Name}, Type: {Type}, Length: {Length}")]
internal class ExrAttribute
{
    public static readonly ExrAttribute EmptyAttribute = new(string.Empty, string.Empty, 0);

    /// <summary>
    /// Initializes a new instance of the <see cref="ExrAttribute"/> class.
    /// </summary>
    /// <param name="name">The name of the attribute.</param>
    /// <param name="type">The type of the attribute.</param>
    /// <param name="length">The length in bytes.</param>
    public ExrAttribute(string name, string type, int length)
    {
        this.Name = name;
        this.Type = type;
        this.Length = length;
    }

    /// <summary>
    /// Gets the name of the attribute.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the type of the attribute.
    /// </summary>
    public string Type { get; }

    /// <summary>
    /// Gets the length in bytes of the attribute.
    /// </summary>
    public int Length { get; }
}
