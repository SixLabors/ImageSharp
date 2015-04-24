// --------------------------------------------------------------------------------------------------------------------
// <copyright file="GaussianLayer.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   A Gaussian layer for applying sharpening and blurring methods to an image.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging
{
    using System;

    /// <summary>
    /// A Gaussian layer for applying sharpening and blurring methods to an image.
    /// </summary>
    public class GaussianLayer
    {
        /// <summary>
        /// The size.
        /// </summary>
        private int size;

        /// <summary>
        /// The sigma.
        /// </summary>
        private double sigma;

        /// <summary>
        /// The threshold.
        /// </summary>
        private int threshold;

        /// <summary>
        /// Initializes a new instance of the <see cref="GaussianLayer"/> class.
        /// </summary>
        public GaussianLayer()
        {
            this.Size = 3;
            this.Sigma = 1.4;
            this.Threshold = 0;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GaussianLayer"/> class.
        /// </summary>
        /// <param name="size">
        /// The size to set the Gaussian kernel to.
        /// </param>
        /// <param name="sigma">
        /// The Sigma value (standard deviation) for Gaussian function used to calculate the kernel.
        /// </param>
        /// <param name="threshold">
        /// The threshold value, which is added to each weighted sum of pixels.
        /// </param>
        public GaussianLayer(int size, double sigma = 1.4, int threshold = 0)
        {
            this.Size = size;
            this.Sigma = sigma;
            this.Threshold = threshold;
        }

        /// <summary>
        /// Gets or sets the size of the Gaussian kernel.
        /// <remarks>
        /// <para>
        /// If set to a value below 0, the property will be set to 0.
        /// </para>
        /// </remarks>
        /// </summary>
        public int Size
        {
            get
            {
                return this.size;
            }

            set
            {
                if (value < 0)
                {
                    value = 0;
                }

                this.size = value;
            }
        }

        /// <summary>
        /// Gets or sets the sigma value (standard deviation) for Gaussian function used to calculate the kernel.
        /// <remarks>
        /// <para>
        /// If set to a value below 0, the property will be set to 0.
        /// </para>
        /// </remarks>
        /// </summary>
        public double Sigma
        {
            get
            {
                return this.sigma;
            }

            set
            {
                if (value < 0)
                {
                    value = 0;
                }

                this.sigma = value;
            }
        }

        /// <summary>
        /// Gets or sets the threshold value, which is added to each weighted sum of pixels.
        /// <remarks>
        /// <para>
        /// If set to a value below 0, the property will be set to 0.
        /// </para>
        /// </remarks>
        /// </summary>
        public int Threshold
        {
            get
            {
                return this.threshold;
            }

            set
            {
                if (value < 0)
                {
                    value = 0;
                }

                this.threshold = value;
            }
        }

        /// <summary>
        /// Returns a value that indicates whether the specified object is an 
        /// <see cref="GaussianLayer"/> object that is equivalent to 
        /// this <see cref="GaussianLayer"/> object.
        /// </summary>
        /// <param name="obj">
        /// The object to test.
        /// </param>
        /// <returns>
        /// True if the given object  is an <see cref="GaussianLayer"/> object that is equivalent to 
        /// this <see cref="GaussianLayer"/> object; otherwise, false.
        /// </returns>
        public override bool Equals(object obj)
        {
            GaussianLayer gaussianLayer = obj as GaussianLayer;

            if (gaussianLayer == null)
            {
                return false;
            }

            return this.Size == gaussianLayer.Size
                && Math.Abs(this.Sigma - gaussianLayer.Sigma) < 0.0001
                && this.Threshold == gaussianLayer.Threshold;
        }

        /// <summary>
        /// Serves as a hash function for a particular type. 
        /// </summary>
        /// <returns>
        /// A hash code for the current <see cref="T:System.Object"/>.
        /// </returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = this.Size;
                hashCode = (hashCode * 397) ^ this.Size.GetHashCode();
                hashCode = (hashCode * 397) ^ this.Threshold;
                return hashCode;
            }
        }
    }
}
