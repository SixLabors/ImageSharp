// -----------------------------------------------------------------------
// <copyright file="RotateLayer.cs" company="James South">
//     Copyright (c) James South.
//     Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace ImageProcessor.Imaging
{
    #region Using
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    #endregion

    /// <summary>
    /// Encapsulates the properties required to rotate an image.
    /// </summary>
    public class RotateLayer : IEqualityComparer<RotateLayer>
    {
        #region Constructors
        /// <summary>
        /// Initializes a new instance of the <see cref="RotateLayer"/> class.
        /// </summary>
        public RotateLayer()
        {
            this.BackgroundColor = Color.Transparent;
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
        /// Returns a value indicating whether this instance is equal to another <see cref="ImageProcessor.Imaging.RotateLayer"/>.
        /// </summary>
        /// <param name="other">
        /// The other <see cref="ImageProcessor.Imaging.RotateLayer"/> to compare to.
        /// </param>
        /// <returns>
        /// True if this instance is equal to another <see cref="ImageProcessor.Imaging.RotateLayer"/>; otherwise, false.
        /// </returns>
        public bool Equals(RotateLayer other)
        {
            return this.Angle.Equals(other.Angle) && this.BackgroundColor.Equals(other.BackgroundColor);
        }

        /// <summary>
        /// Determines whether the specified objects are equal.
        /// </summary>
        /// <returns>
        /// true if the specified objects are equal; otherwise, false.
        /// </returns>
        /// <param name="x">
        /// The first object of type <see cref="ImageProcessor.Imaging.RotateLayer"/> to compare.
        /// </param>
        /// <param name="y">
        /// The second object of type <see cref="ImageProcessor.Imaging.RotateLayer"/> to compare.
        /// </param>
        public bool Equals(RotateLayer x, RotateLayer y)
        {
            return x.Angle.Equals(y.Angle) && x.BackgroundColor.Equals(y.BackgroundColor);
        }

        /// <summary>
        /// Returns a hash code for the specified object.
        /// </summary>
        /// <returns>
        /// A hash code for the specified object.
        /// </returns>
        /// <param name="obj">
        /// The <see cref="T:System.Object"/> for which a hash code is to be returned.
        /// </param>
        /// <exception cref="T:System.ArgumentNullException">
        /// The type of <paramref name="obj"/> is a reference type and <paramref name="obj"/> is null.
        /// </exception>
        public int GetHashCode(RotateLayer obj)
        {
            if (obj == null)
            {
                throw new ArgumentNullException();
            }

            return this.Angle.GetHashCode() + this.BackgroundColor.GetHashCode();
        }
    }
}
