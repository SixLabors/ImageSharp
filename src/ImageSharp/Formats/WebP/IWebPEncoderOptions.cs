// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

namespace SixLabors.ImageSharp.Formats.WebP
{
    /// <summary>
    /// Configuration options for use during webp encoding.
    /// </summary>
    internal interface IWebPEncoderOptions
    {
        /// <summary>
        /// Gets a value indicating whether lossless compression should be used.
        /// If false, lossy compression will be used.
        /// </summary>
        bool Lossless { get; }

        /// <summary>
        /// Gets the compression quality. Between 0 and 100.
        /// For lossy, 0 gives the smallest size and 100 the largest. For lossless,
        /// this parameter is the amount of effort put into the compression: 0 is the fastest but gives larger
        /// files compared to the slowest, but best, 100.
        /// </summary>
        float Quality { get; }

        /// <summary>
        /// Gets a value indicating whether the alpha plane should be compressed with WebP lossless format.
        /// </summary>
        bool AlphaCompression { get; }

        /// <summary>
        /// Gets the number of entropy-analysis passes (in [1..10]).
        /// </summary>
        int EntropyPasses { get; }
    }
}
