// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

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

        /// <summary>Mirror the last few border values: fedcba|abcdefgh|hgfedcb</summary>
        /// <remarks>This Mode is similar to <see cref="Bounce"/>, but here the very border pixel is repeated.</remarks>
        Mirror = 2,

        /// <summary>Bounce off the border: fedcb|abcdefgh|gfedcb</summary>
        /// <remarks>This Mode is similar to <see cref="Mirror"/>, but here the very border pixel is not repeated.</remarks>
        Bounce = 3
    }
}
