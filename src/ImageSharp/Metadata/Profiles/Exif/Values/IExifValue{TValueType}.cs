// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif
{
    /// <summary>
    /// A value of the exif profile.
    /// </summary>
    /// <typeparam name="TValueType">The type of the value.</typeparam>
    public interface IExifValue<TValueType> : IExifValue
    {
        /// <summary>
        /// Gets or sets the value.
        /// </summary>
        TValueType Value { get; set; }
    }
}
