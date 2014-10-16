// --------------------------------------------------------------------------------------------------------------------
// <copyright file="FormatUtilities.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Utility methods for working with supported image formats.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.Formats
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    using ImageProcessor.Configuration;
    using ImageProcessor.Imaging.MetaData;

    /// <summary>
    /// Utility methods for working with supported image formats.
    /// </summary>
    public static class FormatUtilities
    {
        /// <summary>
        /// Gets the correct <see cref="ISupportedImageFormat"/> from the given stream.
        /// <see href="http://stackoverflow.com/questions/55869/determine-file-type-of-an-image"/>
        /// </summary>
        /// <param name="stream">
        /// The <see cref="System.IO.Stream"/> to read from.
        /// </param>
        /// <returns>
        /// The <see cref="ISupportedImageFormat"/>.
        /// </returns>
        public static ISupportedImageFormat GetFormat(Stream stream)
        {
            // Reset the position of the stream to ensure we're reading the correct part.
            stream.Position = 0;

            IEnumerable<ISupportedImageFormat> supportedImageFormats =
                ImageProcessorBootstrapper.Instance.SupportedImageFormats;

            byte[] buffer = new byte[4];
            stream.Read(buffer, 0, buffer.Length);

            foreach (ISupportedImageFormat supportedImageFormat in supportedImageFormats)
            {
                byte[][] headers = supportedImageFormat.FileHeaders;

                // ReSharper disable once LoopCanBeConvertedToQuery
                foreach (byte[] header in headers)
                {
                    if (header.SequenceEqual(buffer.Take(header.Length)))
                    {
                        stream.Position = 0;

                        // Return a new instance as we want to use instance properties.
                        return Activator.CreateInstance(supportedImageFormat.GetType()) as ISupportedImageFormat;
                    }
                }
            }

            stream.Position = 0;
            return null;
        }

        /// <summary>
        /// Returns a value indicating whether the given image is indexed.
        /// </summary>
        /// <param name="image">
        /// The <see cref="System.Drawing.Image"/> to test.
        /// </param>
        /// <returns>
        /// The true if the image is indexed; otherwise, false.
        /// </returns>
        public static bool IsIndexed(Image image)
        {
            // Test value of flags using bitwise AND.
            // ReSharper disable once BitwiseOperatorOnEnumWithoutFlags
            return (image.PixelFormat & PixelFormat.Indexed) != 0;
        }

        /// <summary>
        /// Returns a value indicating whether the given image is indexed.
        /// </summary>
        /// <param name="image">
        /// The <see cref="System.Drawing.Image"/> to test.
        /// </param>
        /// <returns>
        /// The true if the image is animated; otherwise, false.
        /// </returns>
        public static bool IsAnimated(Image image)
        {
            return ImageAnimator.CanAnimate(image);
        }

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
        /// The <see cref="GifInfo"/>.
        /// </returns>
        public static GifInfo GetGifInfo(Image image, ImageFormat format, bool fetchFrames = true)
        {
            if (image.RawFormat.Guid != ImageFormat.Gif.Guid && format.Guid != ImageFormat.Gif.Guid)
            {
                throw new ArgumentException("Image is not a gif.");
            }

            GifInfo info = new GifInfo
            {
                Height = image.Height,
                Width = image.Width
            };

            if (IsAnimated(image))
            {
                info.IsAnimated = true;

                if (fetchFrames)
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

                    info.GifFrames = gifFrames;
                    info.AnimationLength = length;

                    // Loop info is stored at byte 20737.
                    info.LoopCount = BitConverter.ToInt16(image.GetPropertyItem((int)ExifPropertyTag.LoopCount).Value, 0);
                    info.IsLooped = info.LoopCount != 1;
                }
            }

            return info;
        }

        /// <summary>
        /// Returns an instance of EncodingParameters for jpeg compression. 
        /// </summary>
        /// <param name="quality">The quality to return the image at.</param>
        /// <returns>The encodingParameters for jpeg compression. </returns>
        public static EncoderParameters GetEncodingParameters(int quality)
        {
            EncoderParameters encoderParameters = null;
            try
            {
                // Create a series of encoder parameters.
                encoderParameters = new EncoderParameters(1);

                // Set the quality.
                encoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, quality);
            }
            catch
            {
                if (encoderParameters != null)
                {
                    encoderParameters.Dispose();
                }
            }

            return encoderParameters;
        }

        /// <summary>
        /// Uses reflection to allow the creation of an instance of <see cref="PropertyItem"/>.
        /// </summary>
        /// <returns>
        /// The <see cref="PropertyItem"/>.
        /// </returns>
        public static PropertyItem CreatePropertyItem()
        {
            Type type = typeof(PropertyItem);
            ConstructorInfo constructor = type.GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public, null, new Type[] { }, null);

            return (PropertyItem)constructor.Invoke(null);
        }
    }
}