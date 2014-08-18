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
    #region Using

    using System;
    using System.Drawing;
    #endregion

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
        /// The font size to render the text.
        /// </summary>
        private int fontSize = 48;

        /// <summary>
        /// The position to start creating the text from.
        /// </summary>
        private Point position = Point.Empty;
        #endregion

        #region Properties
        /// <summary>
        /// Gets or sets Text.
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// Gets or sets the Color to render the font.
        /// <remarks>
        /// <para>Defaults to black.</para>
        /// </remarks>
        /// </summary>
        public Color TextColor
        {
            get { return this.textColor; }
            set { this.textColor = value; }
        }

        /// <summary>
        /// Gets or sets the name of the font.
        /// </summary>
        public string Font { get; set; }

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
        public Point Position
        {
            get { return this.position; }
            set { this.position = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether a DropShadow should be drawn.
        /// </summary>
        public bool DropShadow { get; set; }
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
                && this.TextColor == textLayer.TextColor
                && this.Font == textLayer.Font
                && this.FontSize == textLayer.FontSize
                && this.Style == textLayer.Style
                && this.DropShadow == textLayer.DropShadow
                && this.Opacity == textLayer.Opacity
                && this.Position == textLayer.Position;
        }

        /// <summary>
        /// Returns a hash code value that represents this object.
        /// </summary>
        /// <returns>
        /// A hash code that represents this object.
        /// </returns>
        public override int GetHashCode()
        {
            return this.Text.GetHashCode() +
                this.TextColor.GetHashCode() +
                this.Font.GetHashCode() +
                this.FontSize.GetHashCode() +
                this.Style.GetHashCode() +
                this.DropShadow.GetHashCode() +
                this.Opacity.GetHashCode() +
                this.Position.GetHashCode();
        }
    }
}
