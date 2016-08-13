// <copyright file="EntropyCrop.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    using Processors;

    /// <summary>
    /// Extension methods for the <see cref="Image{T,TP}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Adjusts an image so that its orientation is suitable for viewing.
        /// </summary>
        /// <typeparam name="T">The pixel format.</typeparam>
        /// <typeparam name="TP">The packed format. <example>long, float.</example></typeparam>
        /// <param name="source">The image to crop.</param>
        /// <returns>The <see cref="Image"/></returns>
        public static Image<T, TP> AutoOrient<T, TP>(this Image<T, TP> source, ProgressEventHandler progressHandler = null)
            where T : IPackedVector<TP>
            where TP : struct
        {
            Orientation orientation = GetExifOrientation(source);

            switch (orientation)
            {
                case Orientation.Unknown:
                case Orientation.TopLeft:
                default:
                    return source;

                case Orientation.TopRight:
                    return source.Flip(FlipType.Horizontal, progressHandler);

                case Orientation.BottomRight:
                    return source.Rotate(RotateType.Rotate180, progressHandler);

                case Orientation.BottomLeft:
                    return source.Flip(FlipType.Vertical, progressHandler);

                case Orientation.LeftTop:
                    return source
                                .Rotate(RotateType.Rotate90, progressHandler)
                                .Flip(FlipType.Horizontal, progressHandler);

                case Orientation.RightTop:
                    return source.Rotate(RotateType.Rotate90, progressHandler);

                case Orientation.RightBottom:
                    return source
                                .Flip(FlipType.Vertical, progressHandler)
                                .Rotate(RotateType.Rotate270, progressHandler);

                case Orientation.LeftBottom:
                    return source.Rotate(RotateType.Rotate270, progressHandler);
            }
        }

        private static Orientation GetExifOrientation<T, TP>(Image<T, TP> source)
            where T : IPackedVector<TP>
            where TP : struct
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
