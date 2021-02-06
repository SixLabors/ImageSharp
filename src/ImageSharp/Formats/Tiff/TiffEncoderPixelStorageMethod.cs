// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Experimental.Tiff
{
    /// <summary>
    /// The tiff encoder pixel storage method.
    /// </summary>
    public enum TiffEncoderPixelStorageMethod
    {
        /// <summary>
        /// The auto mode.
        /// </summary>
        Auto,

        /// <summary>
        /// The single strip mode.
        /// </summary>
        SingleStrip,

        /// <summary>
        /// The multi strip mode.
        /// </summary>
        MultiStrip,
    }
}
