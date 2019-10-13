// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Tga
{
    /// <summary>
    /// Extension methods for TgaImageType enum.
    /// </summary>
    public static class TgaImageTypeExtensions
    {
        /// <summary>
        /// Checks if this tga image type is run length encoded.
        /// </summary>
        /// <param name="imageType">The tga image type.</param>
        /// <returns>True, if this image type is run length encoded, otherwise false.</returns>
        public static bool IsRunLengthEncoded(this TgaImageType imageType)
        {
            if (imageType is TgaImageType.RleColorMapped || imageType is TgaImageType.RleBlackAndWhite || imageType is TgaImageType.RleTrueColor)
            {
                return true;
            }

            return false;
        }
    }
}
