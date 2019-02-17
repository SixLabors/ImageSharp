// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.MetaData
{
    /// <summary>
    /// Enumerated frame process modes to apply to multi-frame images.
    /// </summary>
    public enum FrameDecodingMode
    {
        /// <summary>
        /// Decodes all the frames of a multi-frame image.
        /// </summary>
        All,

        /// <summary>
        /// Decodes only the first frame of a multi-frame image.
        /// </summary>
        First
    }
}