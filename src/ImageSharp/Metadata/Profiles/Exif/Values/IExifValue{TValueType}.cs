// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

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
