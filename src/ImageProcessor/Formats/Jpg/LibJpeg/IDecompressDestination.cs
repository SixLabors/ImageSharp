// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IDecompressDestination.cs" company="James South">
//   Copyright (c) James South and contributors.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace BitMiracle.LibJpeg
{
    using System.IO;

    /// <summary>
    /// Common interface for processing of decompression.
    /// </summary>
    internal interface IDecompressDestination
    {
        /// <summary>
        /// Gets the stream with decompressed data.
        /// </summary>
        Stream Output
        {
            get;
        }

        /// <summary>
        /// Sets the image attributes.
        /// </summary>
        /// <param name="parameters">
        /// The <see cref="LoadedImageAttributes"/> containing attributes.
        /// </param>
        void SetImageAttributes(LoadedImageAttributes parameters);

        /// <summary>
        /// Begins writing. Called before decompression
        /// </summary>
        void BeginWrite();

        /// <summary>
        /// Processes the given row of pixels.
        /// </summary>
        /// <param name="row">
        /// The <see cref="T:byte[]"/> representing the row.
        /// </param>
        void ProcessPixelsRow(byte[] row);

        /// <summary>
        /// Ends writing. Called after decompression
        /// </summary>
        void EndWrite();
    }
}
