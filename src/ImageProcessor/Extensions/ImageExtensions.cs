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
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.IO;

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
        /// The image.
        /// </param>
        /// <returns>
        /// The <see cref="ImageInfo"/>.
        /// </returns>
        public static ImageInfo GetImageInfo(this Image imagex)
        {
            string path = new Uri(System.Reflection.Assembly.GetExecutingAssembly().CodeBase).LocalPath;
            // ReSharper disable once AssignNullToNotNullAttribute
            string resolvedPath = Path.Combine(Path.GetDirectoryName(path), "frames");
            DirectoryInfo di = new DirectoryInfo(resolvedPath);
            if (!di.Exists)
            {
                di.Create();
            }


            ImageInfo info = new ImageInfo();

            using (Image image = (Image)imagex.Clone())
            {
                info.Height = image.Height;
                info.Width = image.Width;

                // Test value of flags using bitwise AND.
                // ReSharper disable once BitwiseOperatorOnEnumWithoutFlags
                info.IsIndexed = (image.PixelFormat & PixelFormat.Indexed) != 0;

                if (image.RawFormat.Equals(ImageFormat.Gif))
                {
                    if (ImageAnimator.CanAnimate(image))
                    {
                        FrameDimension frameDimension = new FrameDimension(image.FrameDimensionsList[0]);

                        int frameCount = image.GetFrameCount(frameDimension);
                        int delay = 0;
                        int index = 0;
                        List<GifFrame> gifFrames = new List<GifFrame>();

                        for (int f = 0; f < frameCount; f++)
                        {
                            int thisDelay = BitConverter.ToInt32(image.GetPropertyItem(20736).Value, index);
                            int toAddDelay = thisDelay * 10 < 20 ? 20 : thisDelay * 10; // Minimum delay is 20 ms

                            // Find the frame
                            image.SelectActiveFrame(frameDimension, f);

                            image.Save(Path.Combine(resolvedPath, f + ".gif"), ImageFormat.Gif);

                            // TODO: Get positions.
                            gifFrames.Add(new GifFrame
                                              {
                                                  Delay = toAddDelay,
                                                  Image = (Image)image.Clone()
                                              });

                            delay += toAddDelay;
                            index += 4;
                        }

                        info.GifFrames = gifFrames;
                        info.AnimationLength = delay;
                        info.IsAnimated = true;

                        info.LoopCount = BitConverter.ToInt16(image.GetPropertyItem(20737).Value, 0);

                        // Loop info is stored at byte 20737.
                        info.IsLooped = info.LoopCount != 1;
                    }
                }

                return info;
            }
        }
    }
}
