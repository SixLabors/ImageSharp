// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Primitives;

namespace SixLabors.ImageSharp.MetaData.Profiles.Exif
{
    /// <summary>
    /// Represents an EXIF profile providing access to the collection of values.
    /// </summary>
    public sealed class ExifProfile : IDeepCloneable<ExifProfile>
    {
        /// <summary>
        /// The byte array to read the EXIF profile from.
        /// </summary>
        private readonly byte[] data;

        /// <summary>
        /// The collection of EXIF values
        /// </summary>
        private List<ExifValue> values;

        /// <summary>
        /// The thumbnail offset position in the byte stream
        /// </summary>
        private int thumbnailOffset;

        /// <summary>
        /// The thumbnail length in the byte stream
        /// </summary>
        private int thumbnailLength;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExifProfile"/> class.
        /// </summary>
        public ExifProfile()
            : this((byte[])null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExifProfile"/> class.
        /// </summary>
        /// <param name="data">The byte array to read the EXIF profile from.</param>
        public ExifProfile(byte[] data)
        {
            this.Parts = ExifParts.All;
            this.data = data;
            this.InvalidTags = new List<ExifTag>();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExifProfile"/> class
        /// by making a copy from another EXIF profile.
        /// </summary>
        /// <param name="other">The other EXIF profile, where the clone should be made from.</param>
        private ExifProfile(ExifProfile other)
        {
            this.Parts = other.Parts;
            this.thumbnailLength = other.thumbnailLength;
            this.thumbnailOffset = other.thumbnailOffset;
            this.InvalidTags = new List<ExifTag>(other.InvalidTags);
            if (other.values != null)
            {
                this.values = new List<ExifValue>(other.Values.Count);

                foreach (ExifValue value in other.Values)
                {
                    this.values.Add(value.DeepClone());
                }
            }

            if (other.data != null)
            {
                this.data = new byte[other.data.Length];
                other.data.AsSpan().CopyTo(this.data);
            }
        }

        /// <summary>
        /// Gets or sets which parts will be written when the profile is added to an image.
        /// </summary>
        public ExifParts Parts { get; set; }

        /// <summary>
        /// Gets the tags that where found but contained an invalid value.
        /// </summary>
        public IReadOnlyList<ExifTag> InvalidTags { get; private set; }

        /// <summary>
        /// Gets the values of this EXIF profile.
        /// </summary>
        public IReadOnlyList<ExifValue> Values
        {
            get
            {
                this.InitializeValues();
                return this.values;
            }
        }

        /// <summary>
        /// Returns the thumbnail in the EXIF profile when available.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>
        /// The <see cref="Image{TPixel}"/>.
        /// </returns>
        public Image<TPixel> CreateThumbnail<TPixel>()
            where TPixel : struct, IPixel<TPixel>
        {
            this.InitializeValues();

            if (this.thumbnailOffset == 0 || this.thumbnailLength == 0)
            {
                return null;
            }

            if (this.data is null || this.data.Length < (this.thumbnailOffset + this.thumbnailLength))
            {
                return null;
            }

            using (var memStream = new MemoryStream(this.data, this.thumbnailOffset, this.thumbnailLength))
            {
                return Image.Load<TPixel>(memStream);
            }
        }

        /// <summary>
        /// Returns the value with the specified tag.
        /// </summary>
        /// <param name="tag">The tag of the EXIF value.</param>
        /// <returns>
        /// The <see cref="ExifValue"/>.
        /// </returns>
        public ExifValue GetValue(ExifTag tag)
        {
            foreach (ExifValue exifValue in this.Values)
            {
                if (exifValue.Tag == tag)
                {
                    return exifValue;
                }
            }

            return null;
        }

        /// <summary>
        /// Conditionally returns the value of the tag if it exists.
        /// </summary>
        /// <param name="tag">The tag of the EXIF value.</param>
        /// <param name="value">The value of the tag, if found.</param>
        /// <returns>
        /// The <see cref="bool"/>.
        /// </returns>
        public bool TryGetValue(ExifTag tag, out ExifValue value)
        {
            foreach (ExifValue exifValue in this.Values)
            {
                if (exifValue.Tag == tag)
                {
                    value = exifValue;

                    return true;
                }
            }

            value = default;

            return false;
        }

        /// <summary>
        /// Removes the value with the specified tag.
        /// </summary>
        /// <param name="tag">The tag of the EXIF value.</param>
        /// <returns>
        /// The <see cref="bool"/>.
        /// </returns>
        public bool RemoveValue(ExifTag tag)
        {
            this.InitializeValues();

            for (int i = 0; i < this.values.Count; i++)
            {
                if (this.values[i].Tag == tag)
                {
                    this.values.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Sets the value of the specified tag.
        /// </summary>
        /// <param name="tag">The tag of the EXIF value.</param>
        /// <param name="value">The value.</param>
        public void SetValue(ExifTag tag, object value)
        {
            for (int i = 0; i < this.Values.Count; i++)
            {
                if (this.values[i].Tag == tag)
                {
                    this.values[i] = this.values[i].WithValue(value);

                    return;
                }
            }

            var newExifValue = ExifValue.Create(tag, value);

            this.values.Add(newExifValue);
        }

        /// <summary>
        /// Converts this instance to a byte array.
        /// </summary>
        /// <returns>The <see cref="T:byte[]"/></returns>
        public byte[] ToByteArray()
        {
            if (this.values is null)
            {
                return this.data;
            }

            if (this.values.Count == 0)
            {
                return null;
            }

            var writer = new ExifWriter(this.values, this.Parts);
            return writer.GetData();
        }

        /// <inheritdoc/>
        public ExifProfile DeepClone() => new ExifProfile(this);

        /// <summary>
        /// Synchronizes the profiles with the specified meta data.
        /// </summary>
        /// <param name="metaData">The meta data.</param>
        internal void Sync(ImageMetaData metaData)
        {
            this.SyncResolution(ExifTag.XResolution, metaData.HorizontalResolution);
            this.SyncResolution(ExifTag.YResolution, metaData.VerticalResolution);
        }

        private void SyncResolution(ExifTag tag, double resolution)
        {
            ExifValue value = this.GetValue(tag);

            if (value is null)
            {
                return;
            }

            if (value.IsArray || value.DataType != ExifDataType.Rational)
            {
                this.RemoveValue(value.Tag);
            }

            var newResolution = new Rational(resolution, false);
            this.SetValue(tag, newResolution);
        }

        private void InitializeValues()
        {
            if (this.values != null)
            {
                return;
            }

            if (this.data is null)
            {
                this.values = new List<ExifValue>();
                return;
            }

            var reader = new ExifReader(this.data);

            this.values = reader.ReadValues();

            this.InvalidTags = new List<ExifTag>(reader.InvalidTags);
            this.thumbnailOffset = (int)reader.ThumbnailOffset;
            this.thumbnailLength = (int)reader.ThumbnailLength;
        }
    }
}