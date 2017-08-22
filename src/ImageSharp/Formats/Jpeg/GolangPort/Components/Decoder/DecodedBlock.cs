// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using Block8x8F = SixLabors.ImageSharp.Formats.Jpeg.Common.Block8x8F;

namespace SixLabors.ImageSharp.Formats.Jpeg.GolangPort.Components.Decoder
{
    /// <summary>
    /// A structure to store unprocessed <see cref="Block8x8F"/> instances and their coordinates while scanning the image.
    /// The <see cref="Block"/> is present in a "raw" decoded frequency-domain form.
    /// We need to apply IDCT and unzigging to transform them into color-space blocks.
    /// </summary>
    internal struct DecodedBlock
    {
        /// <summary>
        /// The <see cref="Block8x8F"/>
        /// </summary>
        public Block8x8F Block;
    }
}