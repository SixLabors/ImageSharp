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

    using System.Collections.Generic;
    using System.Drawing;
    using System.Linq;

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
        /// <param name="resizeMode">
        /// The resize mode to apply to resized image. (Default ResizeMode.Pad)
        /// </param>
        /// <param name="anchorPosition">
        /// The <see cref="AnchorPosition"/> to apply to resized image. (Default AnchorPosition.Center)
        /// </param>
        /// <param name="upscale">
        /// Whether to allow up-scaling of images. (Default true)
        /// </param>
        /// <param name="centerCoordinates">
        /// The center coordinates (Default null)
        /// </param>
        /// <param name="maxSize">
        /// The maximum size to resize an image to. 
        /// Used to restrict resizing based on calculated resizing
        /// </param>
        /// <param name="restrictedSizes">
        /// The range of sizes to restrict resizing an image to. 
        /// Used to restrict resizing based on calculated resizing
        /// </param>
        public ResizeLayer(
            Size size,
            ResizeMode resizeMode = ResizeMode.Pad,
            AnchorPosition anchorPosition = AnchorPosition.Center,
            bool upscale = true,
            float[] centerCoordinates = null,
            Size? maxSize = null,
            List<Size> restrictedSizes = null)
        {
            this.Size = size;
            this.Upscale = upscale;
            this.ResizeMode = resizeMode;
            this.AnchorPosition = anchorPosition;
            this.CenterCoordinates = centerCoordinates ?? new float[] { };
            this.MaxSize = maxSize;
            this.RestrictedSizes = restrictedSizes ?? new List<Size>();
        }
        #endregion

        #region Properties
        /// <summary>
        /// Gets or sets the size.
        /// </summary>
        public Size Size { get; set; }

        /// <summary>
        /// Gets or sets the max size.
        /// </summary>
        public Size? MaxSize { get; set; }

        /// <summary>
        /// Gets or sets the restricted range of sizes. to restrict resizing methods to.
        /// </summary>
        public List<Size> RestrictedSizes { get; set; }

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
                && this.Upscale == resizeLayer.Upscale
                && ((this.CenterCoordinates != null
                    && resizeLayer.CenterCoordinates != null
                    && this.CenterCoordinates.SequenceEqual(resizeLayer.CenterCoordinates))
                    || (this.CenterCoordinates == resizeLayer.CenterCoordinates))
                && this.MaxSize == resizeLayer.MaxSize
                && ((this.RestrictedSizes != null
                    && resizeLayer.RestrictedSizes != null
                    && this.RestrictedSizes.SequenceEqual(resizeLayer.RestrictedSizes))
                    || (this.RestrictedSizes == resizeLayer.RestrictedSizes));
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
                int hashCode = this.Size.GetHashCode();
                hashCode = (hashCode * 397) ^ this.MaxSize.GetHashCode();
                hashCode = (hashCode * 397) ^ (this.RestrictedSizes != null ? this.RestrictedSizes.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (int)this.ResizeMode;
                hashCode = (hashCode * 397) ^ (int)this.AnchorPosition;
                hashCode = (hashCode * 397) ^ this.Upscale.GetHashCode();
                hashCode = (hashCode * 397) ^ (this.CenterCoordinates != null ? this.CenterCoordinates.GetHashCode() : 0);
                return hashCode;
            }
        }
    }
}