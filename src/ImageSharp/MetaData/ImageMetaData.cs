// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.MetaData.Profiles.Exif;
using SixLabors.ImageSharp.MetaData.Profiles.Icc;

namespace SixLabors.ImageSharp.MetaData
{
    /// <summary>
    /// Encapsulates the metadata of an image.
    /// </summary>
    public sealed class ImageMetaData
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

        private readonly Dictionary<IImageFormat, object> formatMetaData = new Dictionary<IImageFormat, object>();
        private double horizontalResolution;
        private double verticalResolution;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageMetaData"/> class.
        /// </summary>
        internal ImageMetaData()
        {
            this.horizontalResolution = DefaultHorizontalResolution;
            this.verticalResolution = DefaultVerticalResolution;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageMetaData"/> class
        /// by making a copy from other metadata.
        /// </summary>
        /// <param name="other">
        /// The other <see cref="ImageMetaData"/> to create this instance from.
        /// </param>
        private ImageMetaData(ImageMetaData other)
        {
            this.HorizontalResolution = other.HorizontalResolution;
            this.VerticalResolution = other.VerticalResolution;
            this.ResolutionUnits = other.ResolutionUnits;

            foreach (KeyValuePair<IImageFormat, object> meta in other.formatMetaData)
            {
                this.formatMetaData.Add(meta.Key, meta.Value);
            }

            foreach (ImageProperty property in other.Properties)
            {
                this.Properties.Add(property);
            }

            this.ExifProfile = other.ExifProfile != null
                ? new ExifProfile(other.ExifProfile)
                : null;

            this.IccProfile = other.IccProfile != null
                ? new IccProfile(other.IccProfile)
                : null;
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
        public PixelResolutionUnit ResolutionUnits { get; set; } = PixelResolutionUnit.PixelsPerInch;

        /// <summary>
        /// Gets or sets the Exif profile.
        /// </summary>
        public ExifProfile ExifProfile { get; set; }

        /// <summary>
        /// Gets or sets the list of ICC profiles.
        /// </summary>
        public IccProfile IccProfile { get; set; }

        /// <summary>
        /// Gets the list of properties for storing meta information about this image.
        /// </summary>
        public IList<ImageProperty> Properties { get; } = new List<ImageProperty>();

        /// <summary>
        /// Adds or updates the specified key and value to the <see cref="ImageMetaData"/>.
        /// </summary>
        /// <typeparam name="TFormatMetaData">The type of format metadata.</typeparam>
        /// <param name="key">The key of the metadata to add.</param>
        /// <param name="value">The value of the element to add.</param>
        /// <exception cref="ArgumentNullException">key is null.</exception>
        /// <exception cref="ArgumentNullException">value is null.</exception>
        /// <exception cref="ArgumentException">An element with the same key already exists in the <see cref="ImageMetaData"/>.</exception>
        public void AddOrUpdateFormatMetaData<TFormatMetaData>(IImageFormat<TFormatMetaData> key, TFormatMetaData value)
            where TFormatMetaData : class
        {
            // Don't think this needs to be threadsafe.
            Guard.NotNull(value, nameof(value));
            this.formatMetaData[key] = value;
        }

        /// <summary>
        /// Gets the metadata value associated with the specified key.
        /// </summary>
        /// <typeparam name="TFormatMetaData">The type of metadata.</typeparam>
        /// <param name="key">The key of the value to get.</param>
        /// <returns>
        /// The <typeparamref name="TFormatMetaData"/>.
        /// </returns>
        public TFormatMetaData GetOrAddFormatMetaData<TFormatMetaData>(IImageFormat<TFormatMetaData> key)
             where TFormatMetaData : class
        {
            if (this.formatMetaData.TryGetValue(key, out object meta))
            {
                return (TFormatMetaData)meta;
            }

            TFormatMetaData newMeta = key.CreateDefaultFormatMetaData();
            this.AddOrUpdateFormatMetaData(key, newMeta);
            return newMeta;
        }

        /// <summary>
        /// Clones this into a new instance
        /// </summary>
        /// <returns>The cloned metadata instance</returns>
        public ImageMetaData Clone() => new ImageMetaData(this);

        /// <summary>
        /// Looks up a property with the provided name.
        /// </summary>
        /// <param name="name">The name of the property to lookup.</param>
        /// <param name="result">The property, if found, with the provided name.</param>
        /// <returns>Whether the property was found.</returns>
        internal bool TryGetProperty(string name, out ImageProperty result)
        {
            foreach (ImageProperty property in this.Properties)
            {
                if (property.Name == name)
                {
                    result = property;

                    return true;
                }
            }

            result = default;

            return false;
        }

        /// <summary>
        /// Synchronizes the profiles with the current meta data.
        /// </summary>
        internal void SyncProfiles() => this.ExifProfile?.Sync(this);
    }
}
