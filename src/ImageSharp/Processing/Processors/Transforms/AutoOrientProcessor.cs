// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.MetaData.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors
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
            Orientation orientation = GetExifOrientation(source);

            switch (orientation)
            {
                case Orientation.TopRight:
                    new FlipProcessor<TPixel>(FlipType.Horizontal).Apply(source, sourceRectangle);
                    break;

                case Orientation.BottomRight:
                    new RotateProcessor<TPixel>((int)RotateType.Rotate180).Apply(source, sourceRectangle);
                    break;

                case Orientation.BottomLeft:
                    new FlipProcessor<TPixel>(FlipType.Vertical).Apply(source, sourceRectangle);
                    break;

                case Orientation.LeftTop:
                    new RotateProcessor<TPixel>((int)RotateType.Rotate90).Apply(source, sourceRectangle);
                    new FlipProcessor<TPixel>(FlipType.Horizontal).Apply(source, sourceRectangle);
                    break;

                case Orientation.RightTop:
                    new RotateProcessor<TPixel>((int)RotateType.Rotate90).Apply(source, sourceRectangle);
                    break;

                case Orientation.RightBottom:
                    new FlipProcessor<TPixel>(FlipType.Vertical).Apply(source, sourceRectangle);
                    new RotateProcessor<TPixel>((int)RotateType.Rotate270).Apply(source, sourceRectangle);
                    break;

                case Orientation.LeftBottom:
                    new RotateProcessor<TPixel>((int)RotateType.Rotate270).Apply(source, sourceRectangle);
                    break;

                case Orientation.Unknown:
                case Orientation.TopLeft:
                default:
                    break;
            }
        }

        /// <inheritdoc/>
        protected override void OnApply(ImageFrame<TPixel> sourceBase, Rectangle sourceRectangle, Configuration config)
        {
            // all processing happens at the image level within BeforeImageApply();
        }

        /// <summary>
        /// Returns the current EXIF orientation
        /// </summary>
        /// <param name="source">The image to auto rotate.</param>
        /// <returns>The <see cref="Orientation"/></returns>
        private static Orientation GetExifOrientation(Image<TPixel> source)
        {
            if (source.MetaData.ExifProfile == null)
            {
                return Orientation.Unknown;
            }

            ExifValue value = source.MetaData.ExifProfile.GetValue(ExifTag.Orientation);
            if (value == null)
            {
                return Orientation.Unknown;
            }

            Orientation orientation;
            if (value.DataType == ExifDataType.Short)
            {
                orientation = (Orientation)value.Value;
            }
            else
            {
                orientation = (Orientation)Convert.ToUInt16(value.Value);
                source.MetaData.ExifProfile.RemoveValue(ExifTag.Orientation);
            }

            source.MetaData.ExifProfile.SetValue(ExifTag.Orientation, (ushort)Orientation.TopLeft);

            return orientation;
        }
    }
}