// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IGraphicsProcessor.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Defines properties and methods for ImageProcessor Plugins.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Processors
{
    #region Using
    using System.Collections.Generic;
    using System.Drawing;
    using System.Text.RegularExpressions;
    #endregion

    /// <summary>
    /// Defines properties and methods for ImageProcessor Plugins.
    /// </summary>
    public interface IGraphicsProcessor
    {
        #region Properties
        /// <summary>
        /// Gets or sets the DynamicParameter.
        /// </summary>
        dynamic DynamicParameter { get; set; }

        /// <summary>
        /// Gets or sets any additional settings required by the processor.
        /// </summary>
        Dictionary<string, string> Settings { get; set; }
        #endregion

        #region Methods
        /// <summary>
        /// Processes the image.
        /// </summary>
        /// <param name="factory">
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class containing
        /// the image to process.
        /// </param>
        /// <returns>
        /// The processed image from the current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        Image ProcessImage(ImageFactory factory);
        #endregion
    }
}
