// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ContentAwareResizeLayer.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encapsulates the properties required to resize an image using content aware resizing.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Plugins.Cair.Imaging
{
    using System.Drawing;

    /// <summary>
    /// Encapsulates the properties required to resize an image using content aware resizing.
    /// </summary>
    public class ContentAwareResizeLayer
    {
        /// <summary>
        /// The convolution type to apply to the layer.
        /// </summary>
        private ConvolutionType convolutionType = ConvolutionType.Prewitt;

        /// <summary>
        /// The energy function to apply to the layer.
        /// </summary>
        private EnergyFunction energyFunction = EnergyFunction.Forward;

        /// <summary>
        /// The expected output type.
        /// </summary>
        private OutputType outputType = OutputType.Cair;

        /// <summary>
        /// Whether to assign multiple threads to the resizing method.
        /// </summary>
        private bool parallelize = true;

        /// <summary>
        /// The timeout in milliseconds to attempt to resize for.
        /// </summary>
        private int timeout = 60000;

        /// <summary>
        /// Initializes a new instance of the <see cref="ContentAwareResizeLayer"/> class.
        /// </summary>
        /// <param name="size">
        /// The <see cref="T:System.Drawing.Size"/> containing the width and height to set the image to.
        /// </param>
        public ContentAwareResizeLayer(Size size)
        {
            this.Size = size;
        }

        /// <summary>
        /// Gets or sets the content aware resize convolution type (Default ContentAwareResizeConvolutionType.Prewitt).
        /// </summary>
        public ConvolutionType ConvolutionType
        {
            get
            {
                return this.convolutionType;
            }

            set
            {
                this.convolutionType = value;
            }
        }

        /// <summary>
        /// Gets or sets the energy function (Default EnergyFunction.Forward).
        /// </summary>
        public EnergyFunction EnergyFunction
        {
            get
            {
                return this.energyFunction;
            }

            set
            {
                this.energyFunction = value;
            }
        }

        /// <summary>
        /// Gets or sets the expected output type.
        /// </summary>
        public OutputType OutputType
        {
            get
            {
                return this.outputType;
            }

            set
            {
                this.outputType = value;
            }
        }

        /// <summary>
        /// Gets or sets the size.
        /// </summary>
        public Size Size { get; set; }

        /// <summary>
        /// Gets or sets the the file path to a bitmap file that provides weight indicators specified using
        /// color to guide preservation of image portions during carving. 
        /// <remarks>
        /// The following colors define weight guidance.
        /// &#10; <see cref="Color.Green"/> - Protect the weight.
        ///  &#10; <see cref="Color.Red"/> - Remove the weight.
        ///  &#10; <see cref="Color.Black"/> - No weight.
        /// </remarks>
        /// </summary>
        public string WeightPath { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to assign multiple threads to the resizing method.
        /// (Default true)
        /// </summary>
        public bool Parallelize
        {
            get
            {
                return this.parallelize;
            }

            set
            {
                this.parallelize = value;
            }
        }

        /// <summary>
        /// Gets or sets the timeout in milliseconds to attempt to resize for (Default 60000).
        /// </summary>
        public int Timeout
        {
            get
            {
                return this.timeout;
            }

            set
            {
                this.timeout = value;
            }
        }

        /// <summary>
        /// Returns a value that indicates whether the specified object is an 
        /// <see cref="ContentAwareResizeLayer"/> object that is equivalent to 
        /// this <see cref="ContentAwareResizeLayer"/> object.
        /// </summary>
        /// <param name="obj">
        /// The object to test.
        /// </param>
        /// <returns>
        /// True if the given object  is an <see cref="ContentAwareResizeLayer"/> object that is equivalent to 
        /// this <see cref="ContentAwareResizeLayer"/> object; otherwise, false.
        /// </returns>
        public override bool Equals(object obj)
        {
            ContentAwareResizeLayer resizeLayer = obj as ContentAwareResizeLayer;

            if (resizeLayer == null)
            {
                return false;
            }

            return this.Size == resizeLayer.Size
                && this.ConvolutionType == resizeLayer.ConvolutionType
                && this.EnergyFunction == resizeLayer.EnergyFunction
                && this.OutputType == resizeLayer.OutputType
                && this.Parallelize == resizeLayer.Parallelize
                && this.Timeout == resizeLayer.Timeout
                && this.WeightPath == resizeLayer.WeightPath;
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
                int hashCode = (int)this.ConvolutionType;
                hashCode = (hashCode * 397) ^ (int)this.EnergyFunction;
                hashCode = (hashCode * 397) ^ this.Parallelize.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)this.OutputType;
                hashCode = (hashCode * 397) ^ this.Timeout;
                hashCode = (hashCode * 397) ^ this.Size.GetHashCode();
                hashCode = (hashCode * 397) ^ (this.WeightPath != null ? this.WeightPath.GetHashCode() : 0);
                return hashCode;
            }
        }
    }
}
