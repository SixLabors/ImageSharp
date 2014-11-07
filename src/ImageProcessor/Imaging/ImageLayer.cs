// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ImageLayer.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encapsulates the properties required to add an image layer to an image.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging
{
    using System.Drawing;

    /// <summary>
    /// Encapsulates the properties required to add an image layer to an image.
    /// </summary>
    public class ImageLayer
    {
        /// <summary>
        /// The opacity at which to render the text.
        /// </summary>
        private int opacity = 100;

        /// <summary>
        /// Gets or sets the image.
        /// </summary>
        public Image Image { get; set; }

        /// <summary>
        /// Gets or sets the size.
        /// </summary>
        public Size Size { get; set; }

        /// <summary>
        /// Gets or sets the Opacity of the text layer.
        /// </summary>
        public int Opacity
        {
            get { return this.opacity; }
            set { this.opacity = value; }
        }

        /// <summary>
        /// Gets or sets the Position of the text layer.
        /// </summary>
        public Point? Position { get; set; }

        /// <summary>
        /// Returns a value that indicates whether the specified object is an 
        /// <see cref="TextLayer"/> object that is equivalent to 
        /// this <see cref="TextLayer"/> object.
        /// </summary>
        /// <param name="obj">
        /// The object to test.
        /// </param>
        /// <returns>
        /// True if the given object  is an <see cref="TextLayer"/> object that is equivalent to 
        /// this <see cref="TextLayer"/> object; otherwise, false.
        /// </returns>
        public override bool Equals(object obj)
        {
            ImageLayer imageLayer = obj as ImageLayer;

            if (imageLayer == null)
            {
                return false;
            }

            return this.Image == imageLayer.Image
                && this.Size == imageLayer.Size
                && this.Opacity == imageLayer.Opacity
                && this.Position == imageLayer.Position;
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
                int hashCode = this.Image.GetHashCode();
                hashCode = (hashCode * 397) ^ this.Size.GetHashCode();
                hashCode = (hashCode * 397) ^ this.Opacity;
                hashCode = (hashCode * 397) ^ this.Position.GetHashCode();
                return hashCode;
            }
        }
    }
}
