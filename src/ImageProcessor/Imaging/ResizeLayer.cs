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
        /// <param name="backgroundColor">
        /// The <see cref="T:System.Drawing.Color"/> to set as the background color.
        /// <remarks>Used for image formats that do not support transparency (Default transparent)</remarks>
        /// </param>
        /// <param name="resizeMode">
        /// The resize mode to apply to resized image. (Default ResizeMode.Pad)
        /// </param>
        /// <param name="anchorPosition">
        /// The <see cref="AnchorPosition"/> to apply to resized image. (Default AnchorPosition.Center)
        /// </param>
        /// <param name="upscale">
        /// Whether to allow up-scaling of images. (Default true)
        /// </param>
        public ResizeLayer(
            Size size,
            Color? backgroundColor = null,
            ResizeMode resizeMode = ResizeMode.Pad,
            AnchorPosition anchorPosition = AnchorPosition.Center,
            bool upscale = true)
        {
            this.Size = size;
            this.Upscale = upscale;
            this.BackgroundColor = backgroundColor ?? Color.Transparent;
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

        /// <summary>
        /// Gets or sets a value indicating whether to allow up-scaling of images.
        /// </summary>
        public bool Upscale { get; set; }

        /// <summary>
        /// Gets or sets the center coordinates.
        /// </summary>
        public float[] CenterCoordinates { get; set; }

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
                && this.BackgroundColor == resizeLayer.BackgroundColor
                && this.Upscale == resizeLayer.Upscale;
        }

        /// <summary>
        /// Returns a hash code value that represents this object.
        /// </summary>
        /// <returns>
        /// A hash code that represents this object.
        /// </returns>
        public override int GetHashCode()
        {
            return this.Size.GetHashCode() +
                this.ResizeMode.GetHashCode() +
                this.AnchorPosition.GetHashCode() +
                this.BackgroundColor.GetHashCode() +
                this.Upscale.GetHashCode();
        }
    }
}
