// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Encoder
{
    /// <summary>
    /// Poor man's stackalloc: Contains a value-type <see cref="float"/> buffer sized for 4 <see cref="Block8x8F"/> instances.
    /// Useful for decoder/encoder operations allocating a block for each Jpeg component.
    /// </summary>
    internal unsafe struct BlockQuad
    {
        /// <summary>
        /// The value-type <see cref="float"/> buffer sized for 4 <see cref="Block8x8F"/> instances.
        /// </summary>
        public fixed float Data[4 * Block8x8F.Size];
    }
}