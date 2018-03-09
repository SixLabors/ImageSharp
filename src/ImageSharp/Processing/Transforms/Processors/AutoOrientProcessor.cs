// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.MetaData.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Transforms.Processors
{
    /// <summary>
    /// Adjusts an image so that its orientation is suitable for viewing. Adjustments are based on EXIF metadata embedded in the image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class AutoOrientProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <inheritdoc/>
        protected override void BeforeImageApply(Image<TPixel> source, Rectangle sourceRectangle)
        {
            OrientationType orientation = GetExifOrientation(source);
            Size size = sourceRectangle.Size;
            switch (orientation)
            {
                case OrientationType.TopRight:
                    new FlipProcessor<TPixel>(FlipType.Horizontal).Apply(source, sourceRectangle);
                    break;

                case OrientationType.BottomRight:
                    new RotateProcessor<TPixel>((int)RotateType.Rotate180, size).Apply(source, sourceRectangle);
                    break;

                case OrientationType.BottomLeft:
                    new FlipProcessor<TPixel>(FlipType.Vertical).Apply(source, sourceRectangle);
                    break;

                case OrientationType.LeftTop:
                    new RotateProcessor<TPixel>((int)RotateType.Rotate90, size).Apply(source, sourceRectangle);
                    new FlipProcessor<TPixel>(FlipType.Horizontal).Apply(source, sourceRectangle);
                    break;

                case OrientationType.RightTop:
                    new RotateProcessor<TPixel>((int)RotateType.Rotate90, size).Apply(source, sourceRectangle);
                    break;

                case OrientationType.RightBottom:
                    new FlipProcessor<TPixel>(FlipType.Vertical).Apply(source, sourceRectangle);
                    new RotateProcessor<TPixel>((int)RotateType.Rotate270, size).Apply(source, sourceRectangle);
                    break;

                case OrientationType.LeftBottom:
                    new RotateProcessor<TPixel>((int)RotateType.Rotate270, size).Apply(source, sourceRectangle);
                    break;

                case OrientationType.Unknown:
                case OrientationType.TopLeft:
                default:
                    break;
            }
        }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> sourceBase, Rectangle sourceRectangle, Configuration config)
        {
            // All processing happens at the image level within BeforeImageApply();
        }

        /// <summary>
        /// Returns the current EXIF orientation
        /// </summary>
        /// <param name="source">The image to auto rotate.</param>
        /// <returns>The <see cref="OrientationType"/></returns>
        private static OrientationType GetExifOrientation(Image<TPixel> source)
        {
            if (source.MetaData.ExifProfile == null)
            {
                return OrientationType.Unknown;
            }

            ExifValue value = source.MetaData.ExifProfile.GetValue(ExifTag.Orientation);
            if (value == null)
            {
                return OrientationType.Unknown;
            }

            OrientationType orientation;
            if (value.DataType == ExifDataType.Short)
            {
                orientation = (OrientationType)value.Value;
            }
            else
            {
                orientation = (OrientationType)Convert.ToUInt16(value.Value);
                source.MetaData.ExifProfile.RemoveValue(ExifTag.Orientation);
            }

            source.MetaData.ExifProfile.SetValue(ExifTag.Orientation, (ushort)OrientationType.TopLeft);

            return orientation;
        }
    }
}