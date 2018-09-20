// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using SixLabors.ImageSharp.Formats;

namespace SixLabors.ImageSharp.MetaData
{
    /// <summary>
    /// Encapsulates the metadata of an image frame.
    /// </summary>
    public sealed class ImageFrameMetaData : IDeepCloneable<ImageFrameMetaData>
    {
        private readonly Dictionary<IImageFormat, IDeepCloneable> formatMetaData = new Dictionary<IImageFormat, IDeepCloneable>();

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

            foreach (KeyValuePair<IImageFormat, IDeepCloneable> meta in other.formatMetaData)
            {
                this.formatMetaData.Add(meta.Key, meta.Value.DeepClone());
            }
        }

        /// <inheritdoc/>
        public ImageFrameMetaData DeepClone() => new ImageFrameMetaData(this);

        /// <summary>
        /// Gets the metadata value associated with the specified key.
        /// </summary>
        /// <typeparam name="TFormatMetaData">The type of format metadata.</typeparam>
        /// <typeparam name="TFormatFrameMetaData">The type of format frame metadata.</typeparam>
        /// <param name="key">The key of the value to get.</param>
        /// <returns>
        /// The <typeparamref name="TFormatFrameMetaData"/>.
        /// </returns>
        public TFormatFrameMetaData GetFormatMetaData<TFormatMetaData, TFormatFrameMetaData>(IImageFormat<TFormatMetaData, TFormatFrameMetaData> key)
            where TFormatMetaData : class
            where TFormatFrameMetaData : class, IDeepCloneable
        {
            if (this.formatMetaData.TryGetValue(key, out IDeepCloneable meta))
            {
                return (TFormatFrameMetaData)meta;
            }

            TFormatFrameMetaData newMeta = key.CreateDefaultFormatFrameMetaData();
            this.formatMetaData[key] = newMeta;
            return newMeta;
        }
    }
}