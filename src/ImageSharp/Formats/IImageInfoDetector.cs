// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SixLabors.ImageSharp.Formats
{
    /// <summary>
    /// Encapsulates methods used for detecting the raw image information without fully decoding it.
    /// </summary>
    public interface IImageInfoDetector
    {
        /// <summary>
        /// Reads the raw image information from the specified stream.
        /// </summary>
        /// <param name="configuration">The configuration for the image.</param>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <returns>The <see cref="PixelTypeInfo"/> object</returns>
        IImageInfo Identify(Configuration configuration, Stream stream);

        /// <summary>
        /// Reads the raw image information from the specified stream.
        /// </summary>
        /// <param name="configuration">The configuration for the image.</param>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>The <see cref="PixelTypeInfo"/> object</returns>
        Task<IImageInfo> IdentifyAsync(Configuration configuration, Stream stream, CancellationToken cancellationToken);
    }
}
