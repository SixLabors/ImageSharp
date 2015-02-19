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
        /// Initializes a new instance of the <see cref="FormatBase"/> class.
        /// </summary>
        protected FormatBase()
        {
            this.Quality = 90;
        }

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
        /// The <see cref="T:System.Drawing.Image" />.
        /// </returns>
        public virtual Image Load(Stream stream)
        {
#if !__MonoCS__
            return Image.FromStream(stream, true);
#else
            return Image.FromStream(stream);
#endif
        }

        /// <summary>
        /// Saves the current image to the specified output stream.
        /// </summary>
        /// <param name="stream">The <see cref="T:System.IO.Stream" /> to save the image information to.</param>
        /// <param name="image">The <see cref="T:System.Drawing.Image" /> to save.</param>
        /// <returns>
        /// The <see cref="T:System.Drawing.Image" />.
        /// </returns>
        public virtual Image Save(Stream stream, Image image)
        {
            image.Save(stream, this.ImageFormat);
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

        /// <summary>
        /// Determines whether the specified <see cref="System.Object" />, is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="System.Object" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="System.Object" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        public override bool Equals(object obj)
        {
            ISupportedImageFormat format = obj as ISupportedImageFormat;

            if (format == null)
            {
                return false;
            }

            return this.MimeType.Equals(format.MimeType) && this.IsIndexed.Equals(format.IsIndexed);
        }

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <returns>
        /// A 32-bit signed integer that is the hash code for this instance.
        /// </returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = this.MimeType.GetHashCode();
                hashCode = (hashCode * 397) ^ this.IsIndexed.GetHashCode();
                hashCode = (hashCode * 397) ^ this.Quality;
                return hashCode;
            }
        }
    }
}
