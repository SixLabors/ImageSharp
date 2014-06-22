// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ISupportedImageFormat.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   The SupportedImageFormat interface providing information about image formats to ImageProcessor.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.Formats
{
    using System;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.IO;

    /// <summary>
    /// The SupportedImageFormat interface providing information about image formats to ImageProcessor.
    /// </summary>
    public interface ISupportedImageFormat
    {
        /// <summary>
        /// Gets the file headers.
        /// </summary>
        byte[][] FileHeaders { get; }

        /// <summary>
        /// Gets the list of file extensions.
        /// </summary>
        string[] FileExtensions { get; }

        /// <summary>
        /// Gets the standard identifier used on the Internet to indicate the type of data that a file contains. 
        /// </summary>
        string MimeType { get; }

        /// <summary>
        /// Gets the default file extension.
        /// </summary>
        string DefaultExtension { get; }

        /// <summary>
        /// Gets the file format of the image. 
        /// </summary>
        ImageFormat ImageFormat { get; }

        /// <summary>
        /// Gets or sets a value indicating whether the image format is indexed.
        /// </summary>
        bool IsIndexed { get; set; }

        /// <summary>
        /// Gets or sets the quality of output for images.
        /// </summary>
        int Quality { get; set; }

        #region Methods
        /// <summary>
        /// Applies the given processor the current image.
        /// </summary>
        /// <param name="processor">
        /// The processor delegate.
        /// </param>
        /// <param name="factory">
        /// The <see cref="ImageFactory"/>.
        /// </param>
        void ApplyProcessor(Func<ImageFactory, Image> processor, ImageFactory factory);

        /// <summary>
        /// Loads the image to process. 
        /// </summary>
        /// <param name="stream">
        /// The <see cref="T:System.IO.Stream"/> containing the image information.
        /// </param>
        /// <returns>
        /// The <see cref="T:System.Drawing.Image"/>.
        /// </returns>
        Image Load(Stream stream);

        /// <summary>
        /// Saves the current image to the specified output stream.
        /// </summary>
        /// <param name="stream">
        /// The <see cref="T:System.IO.Stream"/> to save the image information to.
        /// </param>
        /// <param name="image">
        /// The <see cref="T:System.Drawing.Image"/> to save.
        /// </param>
        /// <returns>
        /// The <see cref="T:System.Drawing.Image"/>.
        /// </returns>
        Image Save(Stream stream, Image image);

        /// <summary>
        /// Saves the current image to the specified file path.
        /// </summary>
        /// <param name="path">The path to save the image to.</param>
        /// <param name="image"> 
        /// The <see cref="T:System.Drawing.Image"/> to save.
        /// </param>
        /// <returns>
        /// The <see cref="T:System.Drawing.Image"/>.
        /// </returns>
        Image Save(string path, Image image);
        #endregion
    }
}
