// <copyright file="AutoOrient.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using ImageSharp.PixelFormats;

    using ImageSharp.Processing;

    /// <summary>
    /// Extension methods for the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Adjusts an image so that its orientation is suitable for viewing. Adjustments are based on EXIF metadata embedded in the image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to auto rotate.</param>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        public static Image<TPixel> AutoOrient<TPixel>(this Image<TPixel> source)
            where TPixel : struct, IPixel<TPixel>
        {
            Orientation orientation = GetExifOrientation(source);

            switch (orientation)
            {
                case Orientation.TopRight:
                    return source.Flip(FlipType.Horizontal);

                case Orientation.BottomRight:
                    return source.Rotate(RotateType.Rotate180);

                case Orientation.BottomLeft:
                    return source.Flip(FlipType.Vertical);

                case Orientation.LeftTop:
                    return source.Rotate(RotateType.Rotate90)
                                 .Flip(FlipType.Horizontal);

                case Orientation.RightTop:
                    return source.Rotate(RotateType.Rotate90);

                case Orientation.RightBottom:
                    return source.Flip(FlipType.Vertical)
                                 .Rotate(RotateType.Rotate270);

                case Orientation.LeftBottom:
                    return source.Rotate(RotateType.Rotate270);

                case Orientation.Unknown:
                case Orientation.TopLeft:
                default:
                    return source;
            }
        }

        /// <summary>
        /// Returns the current EXIF orientation
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to auto rotate.</param>
        /// <returns>The <see cref="Orientation"/></returns>
        private static Orientation GetExifOrientation<TPixel>(Image<TPixel> source)
            where TPixel : struct, IPixel<TPixel>
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