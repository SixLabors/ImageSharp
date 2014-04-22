// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ImageExtensions.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encapsulates a series of time saving extension methods to the <see cref="T:System.Drawing.Imaging.Image" /> class.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Extensions
{
    using System;
    using System.Drawing;
    using System.Drawing.Imaging;

    /// <summary>
    /// Encapsulates a series of time saving extension methods to the <see cref="T:System.Drawing.Imaging.Image" /> class.
    /// </summary>
    public static class ImageExtensions
    {
        /// <summary>
        /// Returns information about the given <see cref="System.Drawing.Image"/>.
        /// </summary>
        /// <param name="image">
        /// The image.
        /// </param>
        /// <returns>
        /// The <see cref="ImageInfo"/>.
        /// </returns>
        public static ImageInfo GetImageInfo(this Image image)
        {
            ImageInfo info = new ImageInfo
            {
                Height = image.Height,
                Width = image.Width,

                // Test value of flags using bitwise AND.
                // ReSharper disable once BitwiseOperatorOnEnumWithoutFlags
                IsIndexed = (image.PixelFormat & PixelFormat.Indexed) != 0
            };

            if (image.RawFormat.Equals(ImageFormat.Gif))
            {
                if (ImageAnimator.CanAnimate(image))
                {
                    FrameDimension frameDimension = new FrameDimension(image.FrameDimensionsList[0]);

                    int frameCount = image.GetFrameCount(frameDimension);
                    int delay = 0;
                    int index = 0;

                    for (int f = 0; f < frameCount; f++)
                    {
                        int thisDelay = BitConverter.ToInt32(image.GetPropertyItem(20736).Value, index) * 10;
                        delay += thisDelay < 100 ? 100 : thisDelay; // Minimum delay is 100 ms
                        index += 4;
                    }

                    info.AnimationLength = delay;
                    info.IsAnimated = true;

                    // Loop info is stored at byte 20737.
                    info.IsLooped = BitConverter.ToInt16(image.GetPropertyItem(20737).Value, 0) != 1;
                }
            }

            return info;
        }

        /// <summary>
        /// Provides information about an image.
        /// <see cref="http://madskristensen.net/post/examine-animated-gife28099s-in-c"/>
        /// </summary>
        public struct ImageInfo
        {
            /// <summary>
            /// The image width.
            /// </summary>
            public int Width;

            /// <summary>
            /// The image height.
            /// </summary>
            public int Height;

            /// <summary>
            /// Whether the is indexed.
            /// </summary>
            public bool IsIndexed;

            /// <summary>
            /// Whether the is animated.
            /// </summary>
            public bool IsAnimated;

            /// <summary>
            /// The is looped.
            /// </summary>
            public bool IsLooped;

            /// <summary>
            /// The animation length in milliseconds.
            /// </summary>
            public int AnimationLength;
        }
    }
}
