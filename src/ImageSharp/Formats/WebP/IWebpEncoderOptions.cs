// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Webp
{
    /// <summary>
    /// Configuration options for use during webp encoding.
    /// </summary>
    internal interface IWebPEncoderOptions
    {
        /// <summary>
        /// Gets a value indicating whether lossy compression should be used.
        /// If false, lossless compression will be used.
        /// </summary>
        bool Lossy { get; }

        /// <summary>
        /// Gets the compression quality. Between 0 and 100.
        /// For lossy, 0 gives the smallest size and 100 the largest. For lossless,
        /// this parameter is the amount of effort put into the compression: 0 is the fastest but gives larger
        /// files compared to the slowest, but best, 100.
        /// Defaults to 75.
        /// </summary>
        int Quality { get; }

        /// <summary>
        /// Gets the encoding method to use. Its a quality/speed trade-off (0=fast, 6=slower-better).
        /// Defaults to 4.
        /// </summary>
        int Method { get; }

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
