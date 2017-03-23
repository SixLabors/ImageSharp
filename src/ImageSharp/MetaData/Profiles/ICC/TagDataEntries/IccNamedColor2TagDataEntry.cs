// <copyright file="IccNamedColor2TagDataEntry.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System.Linq;

    /// <summary>
    /// The namedColor2Type is a count value and array of structures
    /// that provide color coordinates for color names.
    /// </summary>
    internal sealed class IccNamedColor2TagDataEntry : IccTagDataEntry
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IccNamedColor2TagDataEntry"/> class.
        /// </summary>
        /// <param name="colors">The named colors</param>
        public IccNamedColor2TagDataEntry(IccNamedColor[] colors)
            : this(0, null, null, colors, IccProfileTag.Unknown)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccNamedColor2TagDataEntry"/> class.
        /// </summary>
        /// <param name="prefix">Prefix</param>
        /// <param name="suffix">Suffix</param>
        /// /// <param name="colors">The named colors</param>
        public IccNamedColor2TagDataEntry(string prefix, string suffix, IccNamedColor[] colors)
            : this(0, prefix, suffix, colors, IccProfileTag.Unknown)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccNamedColor2TagDataEntry"/> class.
        /// </summary>
        /// <param name="vendorFlags">Vendor specific flags</param>
        /// <param name="prefix">Prefix</param>
        /// <param name="suffix">Suffix</param>
        /// <param name="colors">The named colors</param>
        public IccNamedColor2TagDataEntry(int vendorFlags, string prefix, string suffix, IccNamedColor[] colors)
            : this(vendorFlags, prefix, suffix, colors, IccProfileTag.Unknown)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccNamedColor2TagDataEntry"/> class.
        /// </summary>
        /// <param name="colors">The named colors</param>
        /// <param name="tagSignature">Tag Signature</param>
        public IccNamedColor2TagDataEntry(IccNamedColor[] colors, IccProfileTag tagSignature)
            : this(0, null, null, colors, tagSignature)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccNamedColor2TagDataEntry"/> class.
        /// </summary>
        /// <param name="prefix">Prefix</param>
        /// <param name="suffix">Suffix</param>
        /// <param name="colors">The named colors</param>
        /// <param name="tagSignature">Tag Signature</param>
        public IccNamedColor2TagDataEntry(string prefix, string suffix, IccNamedColor[] colors, IccProfileTag tagSignature)
            : this(0, prefix, suffix, colors, tagSignature)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccNamedColor2TagDataEntry"/> class.
        /// </summary>
        /// <param name="vendorFlags">Vendor specific flags</param>
        /// <param name="prefix">Prefix</param>
        /// <param name="suffix">Suffix</param>
        /// <param name="colors">The named colors</param>
        /// <param name="tagSignature">Tag Signature</param>
        public IccNamedColor2TagDataEntry(int vendorFlags, string prefix, string suffix, IccNamedColor[] colors, IccProfileTag tagSignature)
            : base(IccTypeSignature.NamedColor2, tagSignature)
        {
            Guard.NotNull(colors, nameof(colors));

            int coordinateCount = 0;
            if (colors.Length > 0)
            {
                coordinateCount = colors[0].DeviceCoordinates?.Length ?? 0;
                Guard.IsTrue(colors.Any(t => (t.DeviceCoordinates?.Length ?? 0) != coordinateCount), nameof(colors), "Device coordinate count must be the same for all colors");
            }

            this.VendorFlags = vendorFlags;
            this.CoordinateCount = coordinateCount;
            this.Prefix = prefix;
            this.Suffix = suffix;
            this.Colors = colors;
        }

        /// <summary>
        /// Gets the number of coordinates
        /// </summary>
        public int CoordinateCount { get; }

        /// <summary>
        /// Gets the prefix
        /// </summary>
        public string Prefix { get; }

        /// <summary>
        /// Gets the suffix
        /// </summary>
        public string Suffix { get; }

        /// <summary>
        /// Gets the vendor specific flags
        /// </summary>
        public int VendorFlags { get; }

        /// <summary>
        /// Gets the named colors
        /// </summary>
        public IccNamedColor[] Colors { get; }

        /// <inheritdoc />
        public override bool Equals(IccTagDataEntry other)
        {
            if (base.Equals(other) && other is IccNamedColor2TagDataEntry entry)
            {
                return this.CoordinateCount == entry.CoordinateCount
                && this.Prefix == entry.Prefix
                && this.Suffix == entry.Suffix
                && this.VendorFlags == entry.VendorFlags
                && this.Colors.SequenceEqual(entry.Colors);
            }

            return false;
        }
    }
}
