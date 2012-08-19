// -----------------------------------------------------------------------
// <copyright file="RotateLayer.cs" company="James South">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

namespace ImageProcessor.Imaging
{
    #region Using
    using System.Drawing;
    #endregion

    /// <summary>
    /// Enacapsulates the properties required to rotate an image.
    /// </summary>
    internal class RotateLayer
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
    }
}
