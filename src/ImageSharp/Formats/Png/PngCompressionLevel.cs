// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Png
{
    /// <summary>
    /// Provides enumeration of available PNG compression levels.
    /// </summary>
    public enum PngCompressionLevel
    {
        /// <summary>
        /// Level 0. Equivalent to <see cref="NoCompression"/>.
        /// </summary>
        Level0 = 0,

        /// <summary>
        /// No compression. Equivalent to <see cref="Level0"/>.
        /// </summary>
        NoCompression = Level0,

        /// <summary>
        /// Level 1. Equivalent to <see cref="BestSpeed"/>.
        /// </summary>
        Level1 = 1,

        /// <summary>
        /// Best speed compression level.
        /// </summary>
        BestSpeed = Level1,

        /// <summary>
        /// Level 2.
        /// </summary>
        Level2 = 2,

        /// <summary>
        /// Level 3.
        /// </summary>
        Level3 = 3,

        /// <summary>
        /// Level 4.
        /// </summary>
        Level4 = 4,

        /// <summary>
        /// Level 5.
        /// </summary>
        Level5 = 5,

        /// <summary>
        /// Level 6. Equivalent to <see cref="DefaultCompression"/>.
        /// </summary>
        Level6 = 6,

        /// <summary>
        /// The default compression level. Equivalent to <see cref="Level6"/>.
        /// </summary>
        DefaultCompression = Level6,

        /// <summary>
        /// Level 7.
        /// </summary>
        Level7 = 7,

        /// <summary>
        /// Level 8.
        /// </summary>
        Level8 = 8,

        /// <summary>
        /// Level 9. Equivalent to <see cref="BestCompression"/>.
        /// </summary>
        Level9 = 9,

        /// <summary>
        /// Best compression level. Equivalent to <see cref="Level9"/>.
        /// </summary>
        BestCompression = Level9,
    }
}
