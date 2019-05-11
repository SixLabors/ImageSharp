// Copyright (c) Six Labors and contributors.
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
            where TPixel : struct, IPixel<TPixel>
        {
            ExifProfile profile = image.Metadata.ExifProfile;
            if (profile is null)
            {
                return;
            }

            // Removing the previously stored value allows us to set a value with our own data tag if required.
            if (profile.GetValue(ExifTag.PixelXDimension) != null)
            {
                profile.RemoveValue(ExifTag.PixelXDimension);

                if (image.Width <= ushort.MaxValue)
                {
                    profile.SetValue(ExifTag.PixelXDimension, (ushort)image.Width);
                }
                else
                {
                    profile.SetValue(ExifTag.PixelXDimension, (uint)image.Width);
                }
            }

            if (profile.GetValue(ExifTag.PixelYDimension) != null)
            {
                profile.RemoveValue(ExifTag.PixelYDimension);

                if (image.Height <= ushort.MaxValue)
                {
                    profile.SetValue(ExifTag.PixelYDimension, (ushort)image.Height);
                }
                else
                {
                    profile.SetValue(ExifTag.PixelYDimension, (uint)image.Height);
                }
            }
        }
    }
}