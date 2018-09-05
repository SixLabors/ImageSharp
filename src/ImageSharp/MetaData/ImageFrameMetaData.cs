// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;

namespace SixLabors.ImageSharp.MetaData
{
    /// <summary>
    /// Encapsulates the metadata of an image frame.
    /// </summary>
    public sealed class ImageFrameMetaData
    {
        private readonly Dictionary<string, object> metaData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

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

            foreach (KeyValuePair<string, object> meta in other.metaData)
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
        public void AddOrUpdateMetaData(string key, object value)
        {
            // Don't think this needs to be threadsafe.
            Guard.NotNull(value, nameof(value));
            this.metaData[key] = value;
        }

        /// <summary>
        /// Gets the metadata value associated with the specified key.
        /// </summary>
        /// <typeparam name="T">The type of value.</typeparam>
        /// <param name="key">The key of the value to get.</param>
        /// <param name="value">
        /// When this method returns, contains the metadata value associated with the specified key,
        /// if the key is found; otherwise, the default value for the type of the value parameter.
        /// This parameter is passed uninitialized.
        /// </param>
        /// <returns>
        /// true if the <see cref="ImageMetaData"/> contains an element with
        /// the specified key; otherwise, false.
        /// </returns>
        public bool TryGetMetaData<T>(string key, out T value)
        {
            if (this.metaData.TryGetValue(key, out object meta))
            {
                value = (T)meta;
                return true;
            }

            value = default;
            return false;
        }
    }
}