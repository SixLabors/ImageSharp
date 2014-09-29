// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MatrixFilterBase.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   The matrix filter base contains equality methods.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.Filters.Photo
{
    using System.Drawing;
    using System.Drawing.Imaging;

    /// <summary>
    /// The matrix filter base contains equality methods.
    /// </summary>
    public abstract class MatrixFilterBase : IMatrixFilter
    {
        /// <summary>
        /// Gets the <see cref="T:System.Drawing.Imaging.ColorMatrix" /> for this filter instance.
        /// </summary>
        public abstract ColorMatrix Matrix { get; }

        /// <summary>
        /// Processes the image.
        /// </summary>
        /// <param name="image">The current image to process</param>
        /// <param name="newImage">The new Image to return</param>
        /// <returns>
        /// The processed image.
        /// </returns>
        public abstract Image TransformImage(Image image, Image newImage);

        /// <summary>
        /// Determines whether the specified <see cref="IMatrixFilter" />, is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="IMatrixFilter" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="IMatrixFilter" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        public override bool Equals(object obj)
        {
            IMatrixFilter filter = obj as IMatrixFilter;

            if (filter == null)
            {
                return false;
            }

            return this.GetType().Name == filter.GetType().Name
                   && this.Matrix.Equals(filter.Matrix);
        }

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>
        /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table. 
        /// </returns>
        public override int GetHashCode()
        {
            return this.GetType().Name.GetHashCode() ^ this.Matrix.GetHashCode();
        }
    }
}