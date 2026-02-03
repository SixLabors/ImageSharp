// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif;

/// <summary>
/// A value of the Exif profile.
/// </summary>
public interface IExifValue : IDeepCloneable<IExifValue>
{
    /// <summary>
    /// Gets the data type of the Exif value.
    /// </summary>
    public ExifDataType DataType { get; }

    /// <summary>
    /// Gets a value indicating whether the value is an array.
    /// </summary>
    public bool IsArray { get; }

    /// <summary>
    /// Gets the tag of the Exif value.
    /// </summary>
    public ExifTag Tag { get; }

    /// <summary>
    /// Gets the value of this Exif value.
    /// </summary>
    /// <returns>The value of this Exif value.</returns>
    public object? GetValue();

    /// <summary>
    /// Sets the value of this Exif value.
    /// </summary>
    /// <param name="value">The value of this Exif value.</param>
    /// <returns>A value indicating whether the value could be set.</returns>
    public bool TrySetValue(object? value);
}
