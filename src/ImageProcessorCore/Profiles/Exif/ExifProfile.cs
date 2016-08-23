// <copyright file="ExifProfile.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>
namespace ImageProcessorCore
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;

    /// <summary>
    /// Represents an EXIF profile providing access to the collection of values.
    /// </summary>
    public sealed class ExifProfile
    {
        /// <summary>
        /// The byte array to read the EXIF profile from.
        /// </summary>
        private readonly byte[] data;

        /// <summary>
        /// The collection of EXIF values
        /// </summary>
        private Collection<ExifValue> values;

        /// <summary>
        /// The list of invalid EXIF tags
        /// </summary>
        private List<ExifTag> invalidTags;

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
            this.invalidTags = new List<ExifTag>();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExifProfile"/> class
        /// by making a copy from another EXIF profile.
        /// </summary>
        /// <param name="other">The other EXIF profile, where the clone should be made from.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="other"/> is null.</exception>
        public ExifProfile(ExifProfile other)
        {
            Guard.NotNull(other, nameof(other));

            this.Parts = other.Parts;
            this.thumbnailLength = other.thumbnailLength;
            this.thumbnailOffset = other.thumbnailOffset;
            this.invalidTags = new List<ExifTag>(other.invalidTags);
            if (other.values != null)
            {
                this.values = new Collection<ExifValue>();
                foreach (ExifValue value in other.values)
                {
                    this.values.Add(new ExifValue(value));
                }
            }
            else
            {
                this.data = other.data;
            }
        }

        /// <summary>
        /// Gets or sets which parts will be written when the profile is added to an image.
        /// </summary>
        public ExifParts Parts
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the tags that where found but contained an invalid value.
        /// </summary>
        public IEnumerable<ExifTag> InvalidTags => this.invalidTags;

        /// <summary>
        /// Gets the values of this EXIF profile.
        /// </summary>
        public IEnumerable<ExifValue> Values
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
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        public Image<TColor, TPacked> CreateThumbnail<TColor, TPacked>()
            where TColor : IPackedVector<TPacked>
            where TPacked : struct
        {
            this.InitializeValues();

            if (this.thumbnailOffset == 0 || this.thumbnailLength == 0)
            {
                return null;
            }

            if (this.data.Length < (this.thumbnailOffset + this.thumbnailLength))
            {
                return null;
            }

            using (MemoryStream memStream = new MemoryStream(this.data, this.thumbnailOffset, this.thumbnailLength))
            {
                return new Image<TColor, TPacked>(memStream);
            }
        }

        /// <summary>
        /// Returns the value with the specified tag.
        /// </summary>
        /// <param name="tag">The tag of the EXIF value.</param>
        public ExifValue GetValue(ExifTag tag)
        {
            foreach (ExifValue exifValue in this.Values)
            {
                if (exifValue.Tag == tag)
                    return exifValue;
            }

            return null;
        }

        /// <summary>
        /// Removes the value with the specified tag.
        /// </summary>
        /// <param name="tag">The tag of the EXIF value.</param>
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
            foreach (ExifValue exifValue in this.Values)
            {
                if (exifValue.Tag == tag)
                {
                    exifValue.Value = value;
                    return;
                }
            }

            ExifValue newExifValue = ExifValue.Create(tag, value);
            this.values.Add(newExifValue);
        }

        /// <summary>
        /// Converts this instance to a byte array.
        /// </summary>
        /// <returns>The <see cref="T:byte[]"/></returns>
        public byte[] ToByteArray()
        {
            if (this.values == null)
            {
                return this.data;
            }

            if (this.values.Count == 0)
            {
                return null;
            }

            ExifWriter writer = new ExifWriter(this.values, this.Parts);
            return writer.GetData();
        }

        private void InitializeValues()
        {
            if (this.values != null)
            {
                return;
            }

            if (this.data == null)
            {
                this.values = new Collection<ExifValue>();
                return;
            }

            ExifReader reader = new ExifReader();
            this.values = reader.Read(this.data);
            this.invalidTags = new List<ExifTag>(reader.InvalidTags);
            this.thumbnailOffset = (int)reader.ThumbnailOffset;
            this.thumbnailLength = (int)reader.ThumbnailLength;
        }
    }
}