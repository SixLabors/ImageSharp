// --------------------------------------------------------------------------------------------------------------------
// <copyright file="GifFormat.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Provides the necessary information to support gif images.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.Formats
{
    using System;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Text;

    using ImageProcessor.Imaging.Quantizers;

    /// <summary>
    /// Provides the necessary information to support gif images.
    /// </summary>
    public class GifFormat : FormatBase
    {
        /// <summary>
        /// Gets the file headers.
        /// </summary>
        public override byte[][] FileHeaders
        {
            get
            {
                return new[] { Encoding.ASCII.GetBytes("GIF") };
            }
        }

        /// <summary>
        /// Gets the list of file extensions.
        /// </summary>
        public override string[] FileExtensions
        {
            get
            {
                return new[] { "gif" };
            }
        }

        /// <summary>
        /// Gets the standard identifier used on the Internet to indicate the type of data that a file contains. 
        /// </summary>
        public override string MimeType
        {
            get
            {
                return "image/gif";
            }
        }

        /// <summary>
        /// Gets the <see cref="ImageFormat" />.
        /// </summary>
        public override ImageFormat ImageFormat
        {
            get
            {
                return ImageFormat.Gif;
            }
        }

        /// <summary>
        /// Applies the given processor the current image.
        /// </summary>
        /// <param name="processor">The processor delegate.</param>
        /// <param name="factory">The <see cref="ImageFactory" />.</param>
        public override void ApplyProcessor(Func<ImageFactory, Image> processor, ImageFactory factory)
        {
            GifDecoder decoder = new GifDecoder(factory.Image);

            if (decoder.IsAnimated)
            {
                OctreeQuantizer quantizer = new OctreeQuantizer(255, 8);

                // We don't dispose of the memory stream as that is disposed when a new image is created and doing so 
                // beforehand will cause an exception.
                MemoryStream stream = new MemoryStream();
                using (GifEncoder encoder = new GifEncoder(stream, null, null, decoder.LoopCount))
                {
                    foreach (GifFrame frame in decoder.GifFrames)
                    {
                        factory.Image = frame.Image;
                        frame.Image = quantizer.Quantize(processor.Invoke(factory));
                        encoder.AddFrame(frame);
                    }
                }

                stream.Position = 0;
                factory.Image = Image.FromStream(stream);
            }
            else
            {
                base.ApplyProcessor(processor, factory);
            }
        }

        /// <summary>
        /// Saves the current image to the specified output stream.
        /// </summary>
        /// <param name="stream">
        /// The <see cref="T:System.IO.Stream" /> to save the image information to.
        /// </param>
        /// <param name="image">The <see cref="T:System.Drawing.Image" /> to save.</param>
        /// <returns>
        /// The <see cref="T:System.Drawing.Image" />.
        /// </returns>
        public override Image Save(Stream stream, Image image)
        {
            if (!FormatUtilities.IsAnimated(image))
            {
                image = new OctreeQuantizer(255, 8).Quantize(image);
            }

            return base.Save(stream, image);
        }

        /// <summary>
        /// Saves the current image to the specified file path.
        /// </summary>
        /// <param name="path">The path to save the image to.</param>
        /// <param name="image">The 
        /// <see cref="T:System.Drawing.Image" /> to save.</param>
        /// <returns>
        /// The <see cref="T:System.Drawing.Image" />.
        /// </returns>
        public override Image Save(string path, Image image)
        {
            if (!FormatUtilities.IsAnimated(image))
            {
                image = new OctreeQuantizer(255, 8).Quantize(image);
            }

            return base.Save(path, image);
        }
    }
}