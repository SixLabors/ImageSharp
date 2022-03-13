// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// Wrapping mode for the border pixels in convolution processing.
    /// </summary>
    public enum BorderWrappingMode : byte
    {
        /// <summary>Repeat the border pixel value: aaaaaa|abcdefgh|hhhhhhh</summary>
        Repeat = 0,

        /// <summary>Take values from the opposite edge: cdefgh|abcdefgh|abcdefg</summary>
        Wrap = 1,

        /// <summary>Mirror the last few border values: fedcb|abcdefgh|gfedcb</summary>
        /// <remarks>Please note this mode doe not repeat the very border pixel, as this gives better image quality.</remarks>
        Mirror = 2
    }
}
