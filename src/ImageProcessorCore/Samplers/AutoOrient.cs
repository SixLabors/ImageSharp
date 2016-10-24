// <copyright file="EntropyCrop.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    /// <summary>
    /// Extension methods for the <see cref="Image{TColor, TPacked}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Adjusts an image so that its orientation is suitable for viewing.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="source">The image to auto rotate.</param>
        /// <returns>The <see cref="Image"/></returns>
        public static Image<TColor, TPacked> AutoOrient<TColor, TPacked>(this Image<TColor, TPacked> source)
            where TColor : struct, IPackedVector<TPacked>
            where TPacked : struct
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
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="source">The image to auto rotate.</param>
        /// <returns>The <see cref="Orientation"/></returns>
        private static Orientation GetExifOrientation<TColor, TPacked>(Image<TColor, TPacked> source)
            where TColor : struct, IPackedVector<TPacked>
            where TPacked : struct
        {
            if (source.ExifProfile == null)
            {
                return Orientation.Unknown;
            }

            ExifValue value = source.ExifProfile.GetValue(ExifTag.Orientation);
            if (value == null)
            {
                return Orientation.Unknown;
            }

            Orientation orientation = (Orientation)value.Value;

            source.ExifProfile.SetValue(ExifTag.Orientation, (ushort)Orientation.TopLeft);

            return orientation;
        }
    }
}