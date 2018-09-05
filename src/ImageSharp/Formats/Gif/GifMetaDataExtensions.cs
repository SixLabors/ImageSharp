// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.MetaData;

namespace SixLabors.ImageSharp.Formats.Gif
{
    /// <summary>
    /// Extension methods for storing meta data specific to Gif images.
    /// </summary>
    public static class GifMetaDataExtensions
    {
        /// <summary>
        /// Adds or updates the Gif specific meta data to the image.
        /// </summary>
        /// <param name="meta">The image meta data.</param>
        /// <param name="value">The gif meta data.</param>
        public static void AddOrUpdateGifMetaData(this ImageMetaData meta, GifMetaData value) => meta.AddOrUpdateMetaData(GifConstants.MetaDataKey, value);

        /// <summary>
        /// Gets the Gif format specific meta data from the image.
        /// </summary>
        /// <param name="meta">The image meta data.</param>
        /// <returns>The <see cref="GifMetaData"/> or null.</returns>
        public static GifMetaData GetGifMetaData(this ImageMetaData meta)
        {
            meta.TryGetMetaData(GifConstants.MetaDataKey, out GifMetaData value);
            return value;
        }

        /// <summary>
        /// Adds or updates the Gif specific meta data to the image frame.
        /// </summary>
        /// <param name="meta">The image meta data.</param>
        /// <param name="value">The gif meta data.</param>
        public static void AddOrUpdateGifFrameMetaData(this ImageFrameMetaData meta, GifFrameMetaData value) => meta.AddOrUpdateMetaData(GifConstants.MetaDataKey, value);

        /// <summary>
        /// Gets the Gif format specific meta data from the image frame.
        /// </summary>
        /// <param name="meta">The image meta data.</param>
        /// <returns>The <see cref="GifMetaData"/> or null.</returns>
        public static GifFrameMetaData GetGifFrameMetaData(this ImageFrameMetaData meta)
        {
            meta.TryGetMetaData(GifConstants.MetaDataKey, out GifFrameMetaData value);
            return value;
        }
    }
}