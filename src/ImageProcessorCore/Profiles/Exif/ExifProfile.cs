// <copyright file="ExifProfile.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;

    /// <summary>
    /// Class that can be used to access an Exif profile.
    /// </summary>
    public sealed class ExifProfile
    {
        private byte[] data;
        private Collection<ExifValue> values;
        private List<ExifTag> invalidTags;
        private int thumbnailOffset;
        private int thumbnailLength;

        ///<summary>
        /// Initializes a new instance of the <see cref="ExifProfile"/> class.
        ///</summary>
        ///<param name="data">The byte array to read the exif profile from.</param>
        public ExifProfile()
            : this((byte[])null)
        {
        }

        ///<summary>
        /// Initializes a new instance of the <see cref="ExifProfile"/> class.
        ///</summary>
        ///<param name="data">The byte array to read the exif profile from.</param>
        public ExifProfile(byte[] data)
        {
            Parts = ExifParts.All;
            BestPrecision = false;
            this.data = data;
            this.invalidTags = new List<ExifTag>();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExifProfile"/> class
        /// by making a copy from another exif profile.
        /// </summary>
        /// <param name="other">The other exif profile, where the clone should be made from.</param>
        /// <exception cref="ArgumentNullException"><paramref name="other"/> is null.</exception>
        public ExifProfile(ExifProfile other)
        {
            Guard.NotNull(other, nameof(other));

            Parts = other.Parts;
            BestPrecision = other.BestPrecision;

            this.thumbnailLength = other.thumbnailLength;
            this.thumbnailOffset = other.thumbnailOffset;
            this.invalidTags = new List<ExifTag>(other.invalidTags);
            if (other.values != null)
            {
                this.values = new Collection<ExifValue>();
                foreach(ExifValue value in other.values)
                {
                    this.values.Add(new ExifValue(value));
                }
            }
            else
            {
                this.data = other.data;
            }
        }

        ///<summary>
        /// Specifies if rationals should be stored with the best precision possible. This is disabled
        /// by default, setting this to true will have an impact on the performance.
        ///</summary>
        public bool BestPrecision
        {
            get;
            set;
        }

        ///<summary>
        /// Specifies which parts will be written when the profile is added to an image.
        ///</summary>
        public ExifParts Parts
        {
            get;
            set;
        }

        ///<summary>
        /// Returns the tags that where found but contained an invalid value.
        ///</summary>
        public IEnumerable<ExifTag> InvalidTags
        {
            get
            {
                return this.invalidTags;
            }
        }

        ///<summary>
        /// Returns the values of this exif profile.
        ///</summary>
        public IEnumerable<ExifValue> Values
        {
            get
            {
                InitializeValues();
                return this.values;
            }
        }

        ///<summary>
        /// Returns the thumbnail in the exif profile when available.
        ///</summary>
        /// <typeparam name="T">The pixel format.</typeparam>
        /// <typeparam name="TP">The packed format. <example>long, float.</example></typeparam>
        public Image<T, TP> CreateThumbnail<T, TP>()
            where T : IPackedVector<TP>
            where TP : struct
        {
            InitializeValues();

            if (this.thumbnailOffset == 0 || this.thumbnailLength == 0)
                return null;

            if (this.data.Length < (this.thumbnailOffset + this.thumbnailLength))
                return null;

            using (MemoryStream memStream = new MemoryStream(this.data, this.thumbnailOffset, this.thumbnailLength))
            {
                return new Image<T, TP>(memStream);
            }
        }

        ///<summary>
        /// Returns the value with the specified tag.
        ///</summary>
        ///<param name="tag">The tag of the exif value.</param>
        public ExifValue GetValue(ExifTag tag)
        {
            foreach (ExifValue exifValue in Values)
            {
                if (exifValue.Tag == tag)
                    return exifValue;
            }

            return null;
        }

        ///<summary>
        /// Removes the value with the specified tag.
        ///</summary>
        ///<param name="tag">The tag of the exif value.</param>
        public bool RemoveValue(ExifTag tag)
        {
            InitializeValues();

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

        ///<summary>
        /// Sets the value of the specified tag.
        ///</summary>
        ///<param name="tag">The tag of the exif value.</param>
        ///<param name="value">The value.</param>
        public void SetValue(ExifTag tag, object value)
        {
            foreach (ExifValue exifValue in Values)
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

        ///<summary>
        /// Converts this instance to a byte array.
        ///</summary>
        public byte[] ToByteArray()
        {
            if (this.values == null)
                return data;

            if (this.values.Count == 0)
                return null;

            ExifWriter writer = new ExifWriter(this.values, Parts, BestPrecision);
            return writer.GetData();
        }

        private void InitializeValues()
        {
            if (this.values != null)
                return;

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