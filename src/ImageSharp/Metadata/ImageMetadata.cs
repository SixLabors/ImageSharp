// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.Metadata.Profiles.Iptc;

namespace SixLabors.ImageSharp.Metadata
{
    /// <summary>
    /// Encapsulates the metadata of an image.
    /// </summary>
    public sealed class ImageMetadata : IDeepCloneable<ImageMetadata>
    {
        /// <summary>
        /// The default horizontal resolution value (dots per inch) in x direction.
        /// <remarks>The default value is 96 <see cref="PixelResolutionUnit.PixelsPerInch"/>.</remarks>
        /// </summary>
        public const double DefaultHorizontalResolution = 96;

        /// <summary>
        /// The default vertical resolution value (dots per inch) in y direction.
        /// <remarks>The default value is 96 <see cref="PixelResolutionUnit.PixelsPerInch"/>.</remarks>
        /// </summary>
        public const double DefaultVerticalResolution = 96;

        /// <summary>
        /// The default pixel resolution units.
        /// <remarks>The default value is <see cref="PixelResolutionUnit.PixelsPerInch"/>.</remarks>
        /// </summary>
        public const PixelResolutionUnit DefaultPixelResolutionUnits = PixelResolutionUnit.PixelsPerInch;

        private readonly Dictionary<IImageFormat, IDeepCloneable> formatMetadata = new Dictionary<IImageFormat, IDeepCloneable>();
        private double horizontalResolution;
        private double verticalResolution;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageMetadata"/> class.
        /// </summary>
        internal ImageMetadata()
        {
            this.horizontalResolution = DefaultHorizontalResolution;
            this.verticalResolution = DefaultVerticalResolution;
            this.ResolutionUnits = DefaultPixelResolutionUnits;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageMetadata"/> class
        /// by making a copy from other metadata.
        /// </summary>
        /// <param name="other">
        /// The other <see cref="ImageMetadata"/> to create this instance from.
        /// </param>
        private ImageMetadata(ImageMetadata other)
        {
            this.HorizontalResolution = other.HorizontalResolution;
            this.VerticalResolution = other.VerticalResolution;
            this.ResolutionUnits = other.ResolutionUnits;

            foreach (KeyValuePair<IImageFormat, IDeepCloneable> meta in other.formatMetadata)
            {
                this.formatMetadata.Add(meta.Key, meta.Value.DeepClone());
            }

            this.ExifProfile = other.ExifProfile?.DeepClone();
            this.IccProfile = other.IccProfile?.DeepClone();
            this.IptcProfile = other.IptcProfile?.DeepClone();
        }

        /// <summary>
        /// Gets or sets the resolution of the image in x- direction.
        /// It is defined as the number of dots per inch and should be an positive value.
        /// </summary>
        /// <value>The density of the image in x- direction.</value>
        public double HorizontalResolution
        {
            get => this.horizontalResolution;

            set
            {
                if (value > 0)
                {
                    this.horizontalResolution = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the resolution of the image in y- direction.
        /// It is defined as the number of dots per inch and should be an positive value.
        /// </summary>
        /// <value>The density of the image in y- direction.</value>
        public double VerticalResolution
        {
            get => this.verticalResolution;

            set
            {
                if (value > 0)
                {
                    this.verticalResolution = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets unit of measure used when reporting resolution.
        ///  00 : No units; width:height pixel aspect ratio = Ydensity:Xdensity
        ///  01 : Pixels per inch (2.54 cm)
        ///  02 : Pixels per centimeter
        ///  03 : Pixels per meter
        /// </summary>
        public PixelResolutionUnit ResolutionUnits { get; set; }

        /// <summary>
        /// Gets or sets the Exif profile.
        /// </summary>
        public ExifProfile ExifProfile { get; set; }

        /// <summary>
        /// Gets or sets the list of ICC profiles.
        /// </summary>
        public IccProfile IccProfile { get; set; }

        /// <summary>
        /// Gets or sets the iptc profile.
        /// </summary>
        public IptcProfile IptcProfile { get; set; }

        /// <summary>
        /// Gets the metadata value associated with the specified key.
        /// </summary>
        /// <typeparam name="TFormatMetadata">The type of metadata.</typeparam>
        /// <param name="key">The key of the value to get.</param>
        /// <returns>
        /// The <typeparamref name="TFormatMetadata"/>.
        /// </returns>
        public TFormatMetadata GetFormatMetadata<TFormatMetadata>(IImageFormat<TFormatMetadata> key)
             where TFormatMetadata : class, IDeepCloneable
        {
            if (this.formatMetadata.TryGetValue(key, out IDeepCloneable meta))
            {
                return (TFormatMetadata)meta;
            }

            TFormatMetadata newMeta = key.CreateDefaultFormatMetadata();
            this.formatMetadata[key] = newMeta;
            return newMeta;
        }

        /// <inheritdoc/>
        public ImageMetadata DeepClone() => new ImageMetadata(this);

        /// <summary>
        /// Synchronizes the profiles with the current metadata.
        /// </summary>
        internal void SyncProfiles() => this.ExifProfile?.Sync(this);
    }
}
