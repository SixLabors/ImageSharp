// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ImageExtensions.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encapsulates a series of time saving extension methods to the <see cref="T:System.Drawing.Imaging.Image" /> class.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Core.Common.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Imaging;
    using ImageProcessor.Imaging;

    /// <summary>
    /// Encapsulates a series of time saving extension methods to the <see cref="T:System.Drawing.Imaging.Image" /> class.
    /// </summary>
    public static class ImageExtensions
    {
        /// <summary>
        /// Returns information about the given <see cref="System.Drawing.Image"/>.
        /// </summary>
        /// <param name="image">
        /// The image to extend.
        /// </param>
        /// <param name="format">
        /// The image format.
        /// </param>
        /// <param name="fetchFrames">
        /// Whether to fetch the images frames.
        /// </param>
        /// <returns>
        /// The <see cref="ImageInfo"/>.
        /// </returns>
        public static ImageInfo GetImageInfo(this Image image, ImageFormat format, bool fetchFrames = true)
        {
            ImageInfo info = new ImageInfo
                                 {
                                     Height = image.Height,
                                     Width = image.Width,
                                     // ReSharper disable once BitwiseOperatorOnEnumWithoutFlags
                                     IsIndexed = (image.PixelFormat & PixelFormat.Indexed) != 0
                                 };

            if (image.RawFormat.Guid == ImageFormat.Gif.Guid && format.Guid == ImageFormat.Gif.Guid)
            {
                if (ImageAnimator.CanAnimate(image))
                {
                    info.IsAnimated = true;

                    if (fetchFrames)
                    {
                        FrameDimension frameDimension = new FrameDimension(image.FrameDimensionsList[0]);
                        int frameCount = image.GetFrameCount(frameDimension);
                        int last = frameCount - 1;
                        int delay = 0;
                        int index = 0;
                        List<GifFrame> gifFrames = new List<GifFrame>();

                        for (int f = 0; f < frameCount; f++)
                        {
                            int thisDelay = BitConverter.ToInt32(image.GetPropertyItem(20736).Value, index);
                            int toAddDelay = thisDelay * 10 < 20 ? 20 : thisDelay * 10; // Minimum delay is 20 ms

                            // Find the frame
                            image.SelectActiveFrame(frameDimension, f);

                            // TODO: Get positions.
                            gifFrames.Add(new GifFrame { Delay = toAddDelay, Image = (Image)image.Clone() });

                            // Reset the position.
                            if (f == last)
                            {
                                image.SelectActiveFrame(frameDimension, 0);
                            }

                            delay += toAddDelay;
                            index += 4;
                        }

                        info.GifFrames = gifFrames;
                        info.AnimationLength = delay;

                        // Loop info is stored at byte 20737.
                        info.LoopCount = BitConverter.ToInt16(image.GetPropertyItem(20737).Value, 0);
                        info.IsLooped = info.LoopCount != 1;
                    }
                }
            }

            return info;
        }
    }
}
