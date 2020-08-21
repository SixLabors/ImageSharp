// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    /// <summary>
    /// Contains helper methods for working with transforms.
    /// </summary>
    internal static class TransformProcessorHelpers
    {
        /// <summary>
        /// Updates the dimensional metadata of a transformed image
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="image">The image to update</param>
        public static void UpdateDimensionalMetadata<TPixel>(Image<TPixel> image)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            ExifProfile profile = image.Metadata.ExifProfile;
            if (profile is null)
            {
                return;
            }

            // Only set the value if it already exists.
            if (profile.GetValue(ExifTag.PixelXDimension) != null)
            {
                profile.SetValue(ExifTag.PixelXDimension, image.Width);
            }

            if (profile.GetValue(ExifTag.PixelYDimension) != null)
            {
                profile.SetValue(ExifTag.PixelYDimension, image.Height);
            }
        }
    }
}
