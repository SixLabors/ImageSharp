// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Formats.Bmp
{
    /// <summary>
    /// Enum for the different color spaces.
    /// </summary>
    internal enum BmpColorSpace
    {
        /// <summary>
        /// This value implies that endpoints and gamma values are given in the appropriate fields.
        /// </summary>
        LCS_CALIBRATED_RGB = 0,

        /// <summary>
        /// The Windows default color space ('Win ').
        /// </summary>
        LCS_WINDOWS_COLOR_SPACE = 1466527264,

        /// <summary>
        /// Specifies that the bitmap is in sRGB color space ('sRGB').
        /// </summary>
        LCS_sRGB = 1934772034,

        /// <summary>
        /// This value indicates that bV5ProfileData points to the file name of the profile to use (gamma and endpoints values are ignored).
        /// </summary>
        PROFILE_LINKED = 1279872587,

        /// <summary>
        /// This value indicates that bV5ProfileData points to a memory buffer that contains the profile to be used (gamma and endpoints values are ignored).
        /// </summary>
        PROFILE_EMBEDDED = 1296188740
    }
}
