// <copyright file="ImageMetaData.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System.Collections.Generic;
    using System.Diagnostics;

    /// <summary>
    /// Encapsulates the metadata of an image.
    /// </summary>
    public sealed class ImageMetaData : IMetaData
    {
        /// <summary>
        /// The default horizontal resolution value (dots per inch) in x direction.
        /// <remarks>The default value is 96 dots per inch.</remarks>
        /// </summary>
        public const double DefaultHorizontalResolution = 96;

        /// <summary>
        /// The default vertical resolution value (dots per inch) in y direction.
        /// <remarks>The default value is 96 dots per inch.</remarks>
        /// </summary>
        public const double DefaultVerticalResolution = 96;

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
        internal ImageMetaData(ImageMetaData other)
        {
            Debug.Assert(other != null);

            this.HorizontalResolution = other.HorizontalResolution;
            this.VerticalResolution = other.VerticalResolution;
            this.Quality = other.Quality;
            this.FrameDelay = other.FrameDelay;
            this.RepeatCount = other.RepeatCount;

            foreach (ImageProperty property in other.Properties)
            {
              this.Properties.Add(new ImageProperty(property));
            }

            if (other.ExifProfile != null)
            {
              this.ExifProfile = new ExifProfile(other.ExifProfile);
            }
        }

        /// <summary>
        /// Gets or sets the resolution of the image in x- direction. It is defined as
        ///  number of dots per inch and should be an positive value.
        /// </summary>
        /// <value>The density of the image in x- direction.</value>
        public double HorizontalResolution
        {
            get
            {
                return this.horizontalResolution;
            }

            set
            {
              if (value > 0)
              {
                  this.horizontalResolution = value;
              }
            }
        }

        /// <summary>
        /// Gets or sets the resolution of the image in y- direction. It is defined as
        /// number of dots per inch and should be an positive value.
        /// </summary>
        /// <value>The density of the image in y- direction.</value>
        public double VerticalResolution
        {
            get
            {
                return this.verticalResolution;
            }

            set
            {
                if (value > 0)
                {
                    this.verticalResolution = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the Exif profile.
        /// </summary>
        public ExifProfile ExifProfile { get; set; }

        /// <summary>
        /// Gets or sets the frame delay for animated images.
        /// If not 0, this field specifies the number of hundredths (1/100) of a second to
        /// wait before continuing with the processing of the Data Stream.
        /// The clock starts ticking immediately after the graphic is rendered.
        /// </summary>
        public int FrameDelay { get; set; }

        /// <summary>
        /// Gets the list of properties for storing meta information about this image.
        /// </summary>
        /// <value>A list of image properties.</value>
        public IList<ImageProperty> Properties { get; } = new List<ImageProperty>();

        /// <summary>
        /// Gets or sets the quality of the image. This affects the output quality of lossy image formats.
        /// </summary>
        public int Quality { get; set; }

        /// <summary>
        /// Gets or sets the number of times any animation is repeated.
        /// <remarks>0 means to repeat indefinitely.</remarks>
        /// </summary>
        public ushort RepeatCount { get; set; }

        /// <summary>
        /// Synchronizes the profiles with the current meta data.
        /// </summary>
        internal void SyncProfiles()
        {
            this.SyncExifProfile();
        }

        private void SyncExifProfile()
        {
            if (this.ExifProfile == null)
            {
                return;
            }

            this.SyncExifResolution(ExifTag.XResolution, this.HorizontalResolution);
            this.SyncExifResolution(ExifTag.YResolution, this.VerticalResolution);
        }

        private void SyncExifResolution(ExifTag tag, double resolution)
        {
            ExifValue value = this.ExifProfile.GetValue(tag);
            if (value != null)
            {
                Rational newResolution = new Rational(resolution, false);
                this.ExifProfile.SetValue(tag, newResolution);
            }
        }
    }
}
