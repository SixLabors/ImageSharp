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
        /// <param name="size">
        /// The <see cref="T:System.Drawing.Size"/> containing the width and height to set the image to.
        /// </param>
        public ResizeLayer(Size size)
        {
            this.Size = size;
            this.ResizeMode = ResizeMode.Pad;
            this.AnchorPosition = AnchorPosition.Center;
            this.BackgroundColor = Color.Transparent;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ResizeLayer"/> class.
        /// </summary>
        /// <param name="size">
        /// The <see cref="T:System.Drawing.Size"/> containing the width and height to set the image to.
        /// </param>
        /// <param name="resizeMode">
        /// The <see cref="ResizeMode"/> to apply to resized image.
        /// </param>
        public ResizeLayer(Size size, ResizeMode resizeMode)
        {
            this.Size = size;
            this.ResizeMode = resizeMode;
            this.AnchorPosition = AnchorPosition.Center;
            this.BackgroundColor = Color.Transparent;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ResizeLayer"/> class.
        /// </summary>
        /// <param name="size">
        /// The <see cref="T:System.Drawing.Size"/> containing the width and height to set the image to.
        /// </param>
        /// <param name="anchorPosition">
        /// The <see cref="AnchorPosition"/> to apply to resized image.
        /// </param>
        public ResizeLayer(Size size, AnchorPosition anchorPosition)
        {
            this.Size = size;
            this.AnchorPosition = anchorPosition;
            this.ResizeMode = ResizeMode.Pad;
            this.BackgroundColor = Color.Transparent;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ResizeLayer"/> class.
        /// </summary>
        /// <param name="backgroundColor">
        /// The <see cref="T:System.Drawing.Color"/> to set as the background color.
        /// <remarks>Used for image formats that do not support transparency</remarks>
        /// </param>
        /// <param name="resizeMode">
        /// The resize mode to apply to resized image.
        /// </param>
        /// <param name="anchorPosition">
        /// The <see cref="AnchorPosition"/> to apply to resized image.
        /// </param>
        public ResizeLayer(Color backgroundColor, ResizeMode resizeMode = ResizeMode.Pad, AnchorPosition anchorPosition = AnchorPosition.Center)
        {
            this.BackgroundColor = backgroundColor;
            this.ResizeMode = resizeMode;
            this.AnchorPosition = anchorPosition;
        }
        #endregion

        #region Properties
        /// <summary>
        /// Gets or sets the size.
        /// </summary>
        public Size Size { get; set; }

        /// <summary>
        /// Gets or sets the background color.
        /// </summary>
        public Color BackgroundColor { get; set; }

        /// <summary>
        /// Gets or sets the resize mode.
        /// </summary>
        public ResizeMode ResizeMode { get; set; }

        /// <summary>
        /// Gets or sets the anchor position.
        /// </summary>
        public AnchorPosition AnchorPosition { get; set; }
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
                && this.AnchorPosition == resizeLayer.AnchorPosition
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
            return this.Size.GetHashCode() + this.ResizeMode.GetHashCode() + this.AnchorPosition.GetHashCode() + this.BackgroundColor.GetHashCode();
        }
    }
}
