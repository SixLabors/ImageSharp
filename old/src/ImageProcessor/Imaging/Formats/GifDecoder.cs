// --------------------------------------------------------------------------------------------------------------------
// <copyright file="GifDecoder.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Decodes gifs to provides information.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.Formats
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Imaging;

    using ImageProcessor.Imaging.MetaData;

    /// <summary>
    /// Decodes gifs to provides information.
    /// </summary>
    public class GifDecoder
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GifDecoder"/> class.
        /// </summary>
        /// <param name="image">
        /// The <see cref="Image"/> to decode.
        /// </param>
        public GifDecoder(Image image)
        {
            this.Height = image.Height;
            this.Width = image.Width;

            if (FormatUtilities.IsAnimated(image))
            {
                this.IsAnimated = true;

                if (this.IsAnimated)
                {
                    int frameCount = image.GetFrameCount(FrameDimension.Time);
                    int last = frameCount - 1;
                    double length = 0;

                    List<GifFrame> gifFrames = new List<GifFrame>();

                    // Get the times stored in the gif.
                    byte[] times = image.GetPropertyItem((int)ExifPropertyTag.FrameDelay).Value;

                    for (int i = 0; i < frameCount; i++)
                    {
                        // Convert each 4-byte chunk into an integer.
                        // GDI returns a single array with all delays, while Mono returns a different array for each frame.
                        TimeSpan delay = TimeSpan.FromMilliseconds(BitConverter.ToInt32(times, (4 * i) % times.Length) * 10);

                        // Find the frame
                        image.SelectActiveFrame(FrameDimension.Time, i);

                        // TODO: Get positions.
                        gifFrames.Add(new GifFrame { Delay = delay, Image = new Bitmap(image) });

                        // Reset the position.
                        if (i == last)
                        {
                            image.SelectActiveFrame(FrameDimension.Time, 0);
                        }

                        length += delay.TotalMilliseconds;
                    }

                    this.GifFrames = gifFrames;
                    this.AnimationLength = length;

                    // Loop info is stored at byte 20737.
                    this.LoopCount = BitConverter.ToInt16(image.GetPropertyItem((int)ExifPropertyTag.LoopCount).Value, 0);
                    this.IsLooped = this.LoopCount != 1;
                }
            }
        }

        /// <summary>
        /// Gets or sets the image width.
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// Gets or sets the image height.
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the image is animated.
        /// </summary>
        public bool IsAnimated { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the image is looped.
        /// </summary>
        public bool IsLooped { get; set; }

        /// <summary>
        /// Gets or sets the loop count.
        /// </summary>
        public int LoopCount { get; set; }

        /// <summary>
        /// Gets or sets the gif frames.
        /// </summary>
        public ICollection<GifFrame> GifFrames { get; set; }

        /// <summary>
        /// Gets or sets the animation length in milliseconds.
        /// </summary>
        public double AnimationLength { get; set; }
    }
}
