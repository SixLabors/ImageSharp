// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Webp
{
    /// <summary>
    /// Quality/speed trade-off for the encoding process (0=fast, 6=slower-better).
    /// </summary>
    public enum WebpEncodingMethod
    {
        /// <summary>
        /// Fastest, but quality compromise. Equivalent to <see cref="Fastest"/>.
        /// </summary>
        Level0 = 0,

        /// <summary>
        /// Fastest, but quality compromise.
        /// </summary>
        Fastest = Level0,

        /// <summary>
        /// Level1.
        /// </summary>
        Level1 = 1,

        /// <summary>
        /// Level 2.
        /// </summary>
        Level2 = 2,

        /// <summary>
        /// Level 3.
        /// </summary>
        Level3 = 3,

        /// <summary>
        /// Level 4. Equivalent to <see cref="Default"/>.
        /// </summary>
        Level4 = 4,

        /// <summary>
        /// BestQuality trade off between speed and quality.
        /// </summary>
        Default = Level4,

        /// <summary>
        /// Level 5.
        /// </summary>
        Level5 = 5,

        /// <summary>
        /// Slowest option, but best quality. Equivalent to <see cref="BestQuality"/>.
        /// </summary>
        Level6 = 6,

        /// <summary>
        /// Slowest option, but best quality.
        /// </summary>
        BestQuality = Level6
    }
}
