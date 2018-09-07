// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using SixLabors.ImageSharp.Formats;

namespace SixLabors.ImageSharp.MetaData
{
    /// <summary>
    /// Encapsulates the metadata of an image frame.
    /// </summary>
    public sealed class ImageFrameMetaData
    {
        private readonly Dictionary<IImageFormat, IImageFormatFrameMetaData> metaData = new Dictionary<IImageFormat, IImageFormatFrameMetaData>();

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageFrameMetaData"/> class.
        /// </summary>
        internal ImageFrameMetaData()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageFrameMetaData"/> class
        /// by making a copy from other metadata.
        /// </summary>
        /// <param name="other">
        /// The other <see cref="ImageFrameMetaData"/> to create this instance from.
        /// </param>
        internal ImageFrameMetaData(ImageFrameMetaData other)
        {
            DebugGuard.NotNull(other, nameof(other));

            foreach (KeyValuePair<IImageFormat, IImageFormatFrameMetaData> meta in other.metaData)
            {
                this.metaData.Add(meta.Key, meta.Value);
            }
        }

        /// <summary>
        /// Clones this ImageFrameMetaData.
        /// </summary>
        /// <returns>The cloned instance.</returns>
        public ImageFrameMetaData Clone() => new ImageFrameMetaData(this);

        /// <summary>
        /// Adds or updates the specified key and value to the <see cref="ImageMetaData"/>.
        /// </summary>
        /// <param name="key">The key of the metadata to add.</param>
        /// <param name="value">The value of the element to add.</param>
        /// <exception cref="ArgumentNullException">key is null.</exception>
        /// <exception cref="ArgumentNullException">value is null.</exception>
        /// <exception cref="ArgumentException">An element with the same key already exists in the <see cref="ImageMetaData"/>.</exception>
        public void AddOrUpdateFormatMetaData(IImageFormat key, IImageFormatFrameMetaData value)
        {
            // Don't think this needs to be threadsafe.
            Guard.NotNull(value, nameof(value));
            this.metaData[key] = value;
        }

        /// <summary>
        /// Gets the metadata value associated with the specified key.
        /// </summary>
        /// <typeparam name="T">The type of metadata.</typeparam>
        /// <param name="key">The key of the value to get.</param>
        /// <returns>
        /// The <typeparamref name="T"/>.
        /// </returns>
        public T GetOrAddFormatMetaData<T>(IImageFormat key)
            where T : IImageFormatFrameMetaData, new()
        {
            if (this.metaData.TryGetValue(key, out IImageFormatFrameMetaData meta))
            {
                return (T)meta;
            }

            var newMeta = new T();
            this.AddOrUpdateFormatMetaData(key, newMeta);
            return newMeta;
        }
    }
}