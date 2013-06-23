// -----------------------------------------------------------------------
// <copyright file="RotateLayer.cs" company="James South">
//     Copyright (c) James South.
//     Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace ImageProcessor.Imaging
{
    #region Using
    using System.Drawing;
    #endregion

    /// <summary>
    /// Encapsulates the properties required to rotate an image.
    /// </summary>
    public class RotateLayer
    {
        #region Constructors
        /// <summary>
        /// Initializes a new instance of the <see cref="RotateLayer"/> class.
        /// </summary>
        public RotateLayer()
        {
            this.BackgroundColor = Color.Transparent;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RotateLayer"/> class.
        /// </summary>
        /// <param name="angle">
        /// The angle at which to rotate the image.
        /// </param>
        public RotateLayer(int angle)
        {
            this.Angle = angle;
            this.BackgroundColor = Color.Transparent;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RotateLayer"/> class.
        /// </summary>
        /// <param name="angle">
        /// The angle at which to rotate the image.
        /// </param>
        /// <param name="backgroundColor">
        /// The <see cref="T:System.Drawing.Color"/> to set as the background color.
        /// <remarks>Used for image formats that do not support transparency</remarks>
        /// </param>
        public RotateLayer(int angle, Color backgroundColor)
        {
            this.Angle = angle;
            this.BackgroundColor = backgroundColor;
        }
        #endregion

        #region Properties
        /// <summary>
        /// Gets or sets the angle at which to rotate the image.
        /// </summary>
        public int Angle { get; set; }

        /// <summary>
        /// Gets or sets the background color.
        /// </summary>
        public Color BackgroundColor { get; set; }
        #endregion

        /// <summary>
        /// Returns a value that indicates whether the specified object is an 
        /// <see cref="RotateLayer"/> object that is equivalent to 
        /// this <see cref="RotateLayer"/> object.
        /// </summary>
        /// <param name="obj">
        /// The object to test.
        /// </param>
        /// <returns>
        /// True if the given object  is an <see cref="RotateLayer"/> object that is equivalent to 
        /// this <see cref="RotateLayer"/> object; otherwise, false.
        /// </returns>
        public override bool Equals(object obj)
        {
            RotateLayer rotate = obj as RotateLayer;

            if (rotate == null)
            {
                return false;
            }

            return this.Angle == rotate.Angle && this.BackgroundColor == rotate.BackgroundColor;
        }

        /// <summary>
        /// Returns a hash code value that represents this object.
        /// </summary>
        /// <returns>
        /// A hash code that represents this object.
        /// </returns>
        public override int GetHashCode()
        {
            return this.Angle.GetHashCode() + this.BackgroundColor.GetHashCode();
        }
    }
}
