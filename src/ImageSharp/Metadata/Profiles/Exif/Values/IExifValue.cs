// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif
{
    /// <summary>
    /// A value of the exif profile.
    /// </summary>
    public interface IExifValue : IDeepCloneable<IExifValue>
    {
        /// <summary>
        /// Gets the data type of the exif value.
        /// </summary>
        ExifDataType DataType { get; }

        /// <summary>
        /// Gets a value indicating whether the value is an array.
        /// </summary>
        bool IsArray { get; }

        /// <summary>
        /// Gets the tag of the exif value.
        /// </summary>
        ExifTag Tag { get; }

        /// <summary>
        /// Gets the value of this exif value.
        /// </summary>
        /// <returns>The value of this exif value.</returns>
        object GetValue();

        /// <summary>
        /// Sets the value of this exif value.
        /// </summary>
        /// <param name="value">The value of this exif value.</param>
        /// <returns>A value indicating whether the value could be set.</returns>
        bool TrySetValue(object value);
    }
}
