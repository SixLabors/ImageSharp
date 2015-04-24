// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TextLayer.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encapsulates the properties required to add a layer of text to an image.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging
{
    using System.Drawing;
    using System.Drawing.Text;

    /// <summary>
    /// Encapsulates the properties required to add a layer of text to an image.
    /// </summary>
    public class TextLayer
    {
        #region Fields
        /// <summary>
        /// The color to render the text.
        /// </summary>
        private Color textColor = Color.Black;

        /// <summary>
        /// The opacity at which to render the text.
        /// </summary>
        private int opacity = 100;

        /// <summary>
        /// The font style to render the text.
        /// </summary>
        private FontStyle fontStyle = FontStyle.Regular;

        /// <summary>
        /// The font family to render the text.
        /// </summary>
        private FontFamily fontFamily = new FontFamily(GenericFontFamilies.SansSerif);

        /// <summary>
        /// The font size to render the text.
        /// </summary>
        private int fontSize = 48;
        #endregion

        #region Properties
        /// <summary>
        /// Gets or sets Text.
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="System.Drawing.Color"/> to render the font.
        /// <remarks>
        /// <para>Defaults to black.</para>
        /// </remarks>
        /// </summary>
        public Color FontColor
        {
            get { return this.textColor; }
            set { this.textColor = value; }
        }

        /// <summary>
        /// Gets or sets the name of the font family.
        /// <remarks>
        /// <para>Defaults to generic sans-serif font family.</para>
        /// </remarks>
        /// </summary>
        public FontFamily FontFamily
        {
            get { return this.fontFamily; }
            set { this.fontFamily = value; }
        }

        /// <summary>
        /// Gets or sets the size of the font in pixels.
        /// <remarks>
        /// <para>Defaults to 48 pixels.</para>
        /// </remarks>
        /// </summary>  
        public int FontSize
        {
            get { return this.fontSize; }
            set { this.fontSize = value; }
        }

        /// <summary>
        /// Gets or sets the FontStyle of the text layer.
        /// <remarks>
        /// <para>Defaults to regular.</para>
        /// </remarks>
        /// </summary>
        public FontStyle Style
        {
            get { return this.fontStyle; }
            set { this.fontStyle = value; }
        }

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
        /// Gets or sets a value indicating whether a DropShadow should be drawn.
        /// </summary>
        public bool DropShadow { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the text should be rendered vertically.
        /// </summary>
        public bool Vertical { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the text should be rendered right to left.
        /// </summary>
        public bool RightToLeft { get; set; }
        #endregion

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
            TextLayer textLayer = obj as TextLayer;

            if (textLayer == null)
            {
                return false;
            }

            return this.Text == textLayer.Text
                && this.FontColor == textLayer.FontColor
                && this.FontFamily.Equals(textLayer.FontFamily)
                && this.FontSize == textLayer.FontSize
                && this.Style == textLayer.Style
                && this.DropShadow == textLayer.DropShadow
                && this.Opacity == textLayer.Opacity
                && this.Position == textLayer.Position
                && this.Vertical == textLayer.Vertical
                && this.RightToLeft == textLayer.RightToLeft;
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
                int hashCode = this.Text != null ? this.Text.GetHashCode() : 0;
                hashCode = (hashCode * 397) ^ this.DropShadow.GetHashCode();
                hashCode = (hashCode * 397) ^ (this.FontFamily != null ? this.FontFamily.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (int)this.Style;
                hashCode = (hashCode * 397) ^ this.FontColor.GetHashCode();
                hashCode = (hashCode * 397) ^ this.Opacity;
                hashCode = (hashCode * 397) ^ this.FontSize;
                hashCode = (hashCode * 397) ^ this.Position.GetHashCode();
                hashCode = (hashCode * 397) ^ this.Vertical.GetHashCode();
                hashCode = (hashCode * 397) ^ this.RightToLeft.GetHashCode();
                return hashCode;
            }
        }
    }
}
