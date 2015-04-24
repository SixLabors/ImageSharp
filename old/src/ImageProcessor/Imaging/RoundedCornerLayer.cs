// --------------------------------------------------------------------------------------------------------------------
// <copyright file="RoundedCornerLayer.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encapsulates the properties required to add rounded corners to an image.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging
{
    /// <summary>
    /// Encapsulates the properties required to add rounded corners to an image.
    /// </summary>
    public class RoundedCornerLayer
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RoundedCornerLayer"/> class.
        /// </summary>
        /// <param name="radius">
        /// The radius at which the corner will be rounded.
        /// </param>
        /// <param name="topLeft">
        /// Set if top left is rounded
        /// </param>
        /// <param name="topRight">
        /// Set if top right is rounded
        /// </param>
        /// <param name="bottomLeft">
        /// Set if bottom left is rounded
        /// </param>
        /// <param name="bottomRight">
        /// Set if bottom right is rounded
        /// </param>
        public RoundedCornerLayer(int radius, bool topLeft = true, bool topRight = true, bool bottomLeft = true, bool bottomRight = true)
        {
            this.Radius = radius;
            this.TopLeft = topLeft;
            this.TopRight = topRight;
            this.BottomLeft = bottomLeft;
            this.BottomRight = bottomRight;
        }

        #region Properties
        /// <summary>
        /// Gets or sets the radius of the corners.
        /// </summary>
        public int Radius { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether top left corners are to be added.
        /// </summary>
        public bool TopLeft { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether top right corners are to be added.
        /// </summary>
        public bool TopRight { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether bottom left corners are to be added.
        /// </summary>
        public bool BottomLeft { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether bottom right corners are to be added.
        /// </summary>
        public bool BottomRight { get; set; }
        #endregion

        /// <summary>
        /// Returns a value that indicates whether the specified object is an 
        /// <see cref="RoundedCornerLayer"/> object that is equivalent to 
        /// this <see cref="RoundedCornerLayer"/> object.
        /// </summary>
        /// <param name="obj">
        /// The object to test.
        /// </param>
        /// <returns>
        /// True if the given object is an <see cref="RoundedCornerLayer"/> object that is equivalent to 
        /// this <see cref="RoundedCornerLayer"/> object; otherwise, false.
        /// </returns>
        public override bool Equals(object obj)
        {
            RoundedCornerLayer rounded = obj as RoundedCornerLayer;

            if (rounded == null)
            {
                return false;
            }

            return this.Radius == rounded.Radius
                   && this.TopLeft == rounded.TopLeft && this.TopRight == rounded.TopRight
                   && this.BottomLeft == rounded.BottomLeft && this.BottomRight == rounded.BottomRight;
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
                int hashCode = this.Radius;
                hashCode = (hashCode * 397) ^ this.TopLeft.GetHashCode();
                hashCode = (hashCode * 397) ^ this.TopRight.GetHashCode();
                hashCode = (hashCode * 397) ^ this.BottomLeft.GetHashCode();
                hashCode = (hashCode * 397) ^ this.BottomRight.GetHashCode();
                return hashCode;
            }
        }
    }
}
