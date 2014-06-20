// --------------------------------------------------------------------------------------------------------------------
// <copyright file="FormatBase.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   The supported format base implement this class when building a supported format.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.Formats
{
    using System;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.IO;

    /// <summary>
    /// The supported format base. Implement this class when building a supported format.
    /// </summary>
    public abstract class FormatBase : ISupportedImageFormat
    {
        /// <summary>
        /// Gets the file headers.
        /// </summary>
        public abstract byte[][] FileHeaders { get; }

        /// <summary>
        /// Gets the list of file extensions.
        /// </summary>
        public abstract string[] FileExtensions { get; }

        /// <summary>
        /// Gets the standard identifier used on the Internet to indicate the type of data that a file contains.
        /// </summary>
        public abstract string MimeType { get; }

        /// <summary>
        /// Gets the default file extension.
        /// </summary>
        public string DefaultExtension
        {
            get
            {
                return this.MimeType.Replace("image/", string.Empty);
            }
        }

        /// <summary>
        /// Gets the file format of the image. 
        /// </summary>
        public abstract ImageFormat ImageFormat { get; }

        /// <summary>
        /// Gets or sets a value indicating whether the image format is indexed.
        /// </summary>
        public bool IsIndexed { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the image format is animated.
        /// </summary>
        public bool IsAnimated { get; set; }

        /// <summary>
        /// Gets or sets the quality of output for images.
        /// </summary>
        public int Quality { get; set; }

        /// <summary>
        /// Applies the given processor the current image.
        /// </summary>
        /// <param name="processor">The processor delegate.</param>
        /// <param name="factory">The <see cref="ImageFactory" />.</param>
        public virtual void ApplyProcessor(Func<ImageFactory, Image> processor, ImageFactory factory)
        {
            factory.Image = processor.Invoke(factory);
        }

        /// <summary>
        /// Decodes the image to process.
        /// </summary>
        /// <param name="stream">
        /// The <see cref="T:System.IO.stream" /> containing the image information.
        /// </param>
        /// <returns>
        /// The the <see cref="T:System.Drawing.Image" />.
        /// </returns>
        public virtual Image Load(Stream stream)
        {
            return Image.FromStream(stream, true);
        }

        /// <summary>
        /// Saves the current image to the specified output stream.
        /// </summary>
        /// <param name="memoryStream">The <see cref="T:System.IO.MemoryStream" /> to save the image information to.</param>
        /// <param name="image">The <see cref="T:System.Drawing.Image" /> to save.</param>
        /// <returns>
        /// The <see cref="T:System.Drawing.Image" />.
        /// </returns>
        public virtual Image Save(MemoryStream memoryStream, Image image)
        {
            image.Save(memoryStream, this.ImageFormat);
            memoryStream.Position = 0;
            return image;
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
        public virtual Image Save(string path, Image image)
        {
            image.Save(path, this.ImageFormat);
            return image;
        }
    }
}
