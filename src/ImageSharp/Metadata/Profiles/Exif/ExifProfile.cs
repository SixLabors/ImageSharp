// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif
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
        private List<IExifValue> values;

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
            this.InvalidTags = Array.Empty<ExifTag>();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExifProfile"/> class
        /// by making a copy from another EXIF profile.
        /// </summary>
        /// <param name="other">The other EXIF profile, where the clone should be made from.</param>
        /// <exception cref="ArgumentNullException"><paramref name="other"/> is null.</exception>>
        private ExifProfile(ExifProfile other)
        {
            Guard.NotNull(other, nameof(other));

            this.Parts = other.Parts;
            this.thumbnailLength = other.thumbnailLength;
            this.thumbnailOffset = other.thumbnailOffset;

            this.InvalidTags = other.InvalidTags.Count > 0
                ? new List<ExifTag>(other.InvalidTags)
                : (IReadOnlyList<ExifTag>)Array.Empty<ExifTag>();

            if (other.values != null)
            {
                this.values = new List<IExifValue>(other.Values.Count);

                foreach (IExifValue value in other.Values)
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
        public IReadOnlyList<IExifValue> Values
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
            where TPixel : unmanaged, IPixel<TPixel>
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
        /// <param name="tag">The tag of the exif value.</param>
        /// <returns>The value with the specified tag.</returns>
        /// <typeparam name="TValueType">The data type of the tag.</typeparam>
        public IExifValue<TValueType> GetValue<TValueType>(ExifTag<TValueType> tag)
        {
            IExifValue value = this.GetValueInternal(tag);
            return value is null ? null : (IExifValue<TValueType>)value;
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
        /// <param name="tag">The tag of the exif value.</param>
        /// <param name="value">The value.</param>
        /// <typeparam name="TValueType">The data type of the tag.</typeparam>
        public void SetValue<TValueType>(ExifTag<TValueType> tag, TValueType value)
            => this.SetValueInternal(tag, value);

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
                return Array.Empty<byte>();
            }

            var writer = new ExifWriter(this.values, this.Parts);
            return writer.GetData();
        }

        /// <inheritdoc/>
        public ExifProfile DeepClone() => new ExifProfile(this);

        /// <summary>
        /// Returns the value with the specified tag.
        /// </summary>
        /// <param name="tag">The tag of the exif value.</param>
        /// <returns>The value with the specified tag.</returns>
        internal IExifValue GetValueInternal(ExifTag tag)
        {
            foreach (IExifValue exifValue in this.Values)
            {
                if (exifValue.Tag == tag)
                {
                    return exifValue;
                }
            }

            return null;
        }

        /// <summary>
        /// Sets the value of the specified tag.
        /// </summary>
        /// <param name="tag">The tag of the exif value.</param>
        /// <param name="value">The value.</param>
        internal void SetValueInternal(ExifTag tag, object value)
        {
            foreach (IExifValue exifValue in this.Values)
            {
                if (exifValue.Tag == tag)
                {
                    exifValue.TrySetValue(value);
                    return;
                }
            }

            ExifValue newExifValue = ExifValues.Create(tag);
            if (newExifValue is null)
            {
                throw new NotSupportedException();
            }

            newExifValue.TrySetValue(value);
            this.values.Add(newExifValue);
        }

        /// <summary>
        /// Synchronizes the profiles with the specified metadata.
        /// </summary>
        /// <param name="metadata">The metadata.</param>
        internal void Sync(ImageMetadata metadata)
        {
            this.SyncResolution(ExifTag.XResolution, metadata.HorizontalResolution);
            this.SyncResolution(ExifTag.YResolution, metadata.VerticalResolution);
        }

        private void SyncResolution(ExifTag<Rational> tag, double resolution)
        {
            IExifValue<Rational> value = this.GetValue(tag);

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
                this.values = new List<IExifValue>();
                return;
            }

            var reader = new ExifReader(this.data);

            this.values = reader.ReadValues();

            this.InvalidTags = reader.InvalidTags.Count > 0
                ? new List<ExifTag>(reader.InvalidTags)
                : (IReadOnlyList<ExifTag>)Array.Empty<ExifTag>();

            this.thumbnailOffset = (int)reader.ThumbnailOffset;
            this.thumbnailLength = (int)reader.ThumbnailLength;
        }
    }
}
