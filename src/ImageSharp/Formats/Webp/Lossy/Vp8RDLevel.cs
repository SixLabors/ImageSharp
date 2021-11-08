// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Webp.Lossy
{
    /// <summary>
    /// Rate-distortion optimization levels
    /// </summary>
    internal enum Vp8RdLevel
    {
        /// <summary>
        /// No rd-opt.
        /// </summary>
        RdOptNone = 0,

        /// <summary>
        /// Basic scoring (no trellis).
        /// </summary>
        RdOptBasic = 1,

        /// <summary>
        /// Perform trellis-quant on the final decision only.
        /// </summary>
        RdOptTrellis = 2,

        /// <summary>
        /// Trellis-quant for every scoring (much slower).
        /// </summary>
        RdOptTrellisAll = 3
    }
}
