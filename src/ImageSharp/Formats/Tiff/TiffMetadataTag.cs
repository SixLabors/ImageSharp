// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// The tiff metadata tag structure.
    /// todo: temporary structure.
    /// </summary>
    public readonly struct TiffMetadataTag
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TiffMetadataTag" /> struct.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="value">The value.</param>
        public TiffMetadataTag(string name, string value)
        {
            Guard.NotNullOrWhiteSpace(name, nameof(name));

            this.Name = name;
            this.Value = value;
        }

        /// <summary>
        /// Gets the name of this <see cref="TiffMetadataTag"/> indicating which kind of
        /// information this property stores.
        /// </summary>
        /// <example>
        /// Typical properties are the author, copyright
        /// information or other meta information.
        /// </example>
        public string Name { get; }

        /// <summary>
        /// Gets the value of this <see cref="TiffMetadataTag"/>.
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Returns the fully qualified type name of this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.String"/> containing a fully qualified type name.
        /// </returns>
        public override string ToString()
        {
            return $"[ Name={this.Name}, Value={this.Value} ]";
        }
    }
}
