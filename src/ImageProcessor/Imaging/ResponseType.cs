// -----------------------------------------------------------------------
// <copyright file="ResponseType.cs" company="James South">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

namespace ImageProcessor.Imaging
{
    #region Using
    using System.ComponentModel;
    #endregion

    /// <summary>
    /// Globally available enumeration which specifies the correct HTTP MIME type of
    /// the output stream for different response types.
    /// <para>
    /// http://en.wikipedia.org/wiki/Internet_media_type"/
    /// </para>
    /// </summary>
    public enum ResponseType
    {
        #region Image
        /// <summary>
        /// The correct HTTP MIME type of the output stream for bmp images.
        /// </summary>
        [DescriptionAttribute("image/bmp")]
        Bmp,

        /// <summary>
        /// The correct HTTP MIME type of the output stream for gif images.
        /// </summary>
        [DescriptionAttribute("image/gif")]
        Gif,

        /// <summary>
        /// The correct HTTP MIME type of the output stream for jpeg images.
        /// </summary>
        [DescriptionAttribute("image/jpeg")]
        Jpeg,

        /// <summary>
        /// The correct HTTP MIME type of the output stream for png images.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Png", Justification = "File extension name")]
        [DescriptionAttribute("image/png")]
        Png,

        /// <summary>
        /// The correct HTTP MIME type of the output stream for svg images.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Svg", Justification = "File extension name")]
        [DescriptionAttribute("image/svg+xml")]
        Svg,

        /// <summary>
        /// The correct HTTP MIME type of the output stream for tiff images.
        /// </summary>
        [DescriptionAttribute("image/tiff")]
        Tiff,
        #endregion
    }
}
