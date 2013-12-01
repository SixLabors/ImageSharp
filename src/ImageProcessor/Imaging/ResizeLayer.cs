// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ResizeLayer.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encapsulates the properties required to resize an image.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging
{
    #region Using
    using System.Drawing;
    #endregion

    /// <summary>
    /// Encapsulates the properties required to resize an image.
    /// </summary>
    public class ResizeLayer
    {
        #region Constructors
        /// <summary>
        /// Initializes a new instance of the <see cref="ResizeLayer"/> class.
        /// </summary>
        public ResizeLayer()
        {
            this.ResizeMode = ResizeMode.Pad;
            this.BackgroundColor = Color.Transparent;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ResizeLayer"/> class.
        /// </summary>
        /// <param name="resizeMode">
        /// The resize mode to apply to resized image.
        /// </param>
        public ResizeLayer(ResizeMode resizeMode)
        {
            this.ResizeMode = resizeMode;
            this.BackgroundColor = Color.Transparent;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ResizeLayer"/> class.
        /// </summary>
        /// <param name="resizeMode">
        /// The resize mode to apply to resized image.
        /// </param>
        /// <param name="backgroundColor">
        /// The <see cref="T:System.Drawing.Color"/> to set as the background color.
        /// <remarks>Used for image formats that do not support transparency</remarks>
        /// </param>
        public ResizeLayer(ResizeMode resizeMode, Color backgroundColor)
        {
            this.ResizeMode = resizeMode;
            this.BackgroundColor = backgroundColor;
        }
        #endregion

        #region Properties
        /// <summary>
        /// Gets or sets the size.
        /// </summary>
        public Size Size { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to ResizeMode the layer.
        /// </summary>
        public ResizeMode ResizeMode { get; set; }

        /// <summary>
        /// Gets or sets the background color.
        /// </summary>
        public Color BackgroundColor { get; set; }
        #endregion

        /// <summary>
        /// Returns a value that indicates whether the specified object is an 
        /// <see cref="ResizeLayer"/> object that is equivalent to 
        /// this <see cref="ResizeLayer"/> object.
        /// </summary>
        /// <param name="obj">
        /// The object to test.
        /// </param>
        /// <returns>
        /// True if the given object  is an <see cref="ResizeLayer"/> object that is equivalent to 
        /// this <see cref="ResizeLayer"/> object; otherwise, false.
        /// </returns>
        public override bool Equals(object obj)
        {
            ResizeLayer resizeLayer = obj as ResizeLayer;

            if (resizeLayer == null)
            {
                return false;
            }

            return this.Size == resizeLayer.Size 
                && this.ResizeMode == resizeLayer.ResizeMode
                && this.BackgroundColor == resizeLayer.BackgroundColor;
        }

        /// <summary>
        /// Returns a hash code value that represents this object.
        /// </summary>
        /// <returns>
        /// A hash code that represents this object.
        /// </returns>
        public override int GetHashCode()
        {
            return this.Size.GetHashCode() + this.ResizeMode.GetHashCode() + this.BackgroundColor.GetHashCode();
        }
    }
}
