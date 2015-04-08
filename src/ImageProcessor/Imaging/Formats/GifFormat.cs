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

    using ImageProcessor.Imaging.Helpers;
    using ImageProcessor.Imaging.Quantizers;

    /// <summary>
    /// Provides the necessary information to support gif images.
    /// </summary>
    public class GifFormat : FormatBase, IQuantizableImageFormat
    {
        /// <summary>
        /// The quantizer for reducing the image palette.
        /// </summary>
        private IQuantizer quantizer = new OctreeQuantizer(255, 8);

        /// <summary>
        /// The color count.
        /// </summary>
        private int colorCount;

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
        /// Gets or sets the quantizer for reducing the image palette.
        /// </summary>
        public IQuantizer Quantizer
        {
            get
            {
                return this.quantizer;
            }

            set
            {
                this.quantizer = value;
            }
        }

        /// <summary>
        /// Gets or sets the color count.
        /// </summary>
        public int ColorCount
        {
            get
            {
                return this.colorCount;
            }

            set
            {
                int count = ImageMaths.Clamp(value, 0, 255);
                this.colorCount = count;
                this.quantizer = new OctreeQuantizer(count, 8);
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
                GifEncoder encoder = new GifEncoder(null, null, decoder.LoopCount);
                foreach (GifFrame frame in decoder.GifFrames)
                {
                    factory.Image = frame.Image;
                    frame.Image = this.Quantizer.Quantize(processor.Invoke(factory));
                    encoder.AddFrame(frame);
                }

                factory.Image = encoder.Save();
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
                image = this.Quantizer.Quantize(image);
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
                image = this.Quantizer.Quantize(image);
            }

            return base.Save(path, image);
        }
    }
}