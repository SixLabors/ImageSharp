// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ImageHelpers.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   The image helpers.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Web.Helpers
{
    #region Using
    using System.Drawing.Imaging;
    using System.IO;
    using System.Text.RegularExpressions;
    #endregion

    /// <summary>
    /// The image helpers.
    /// </summary>
    public static class ImageHelpers
    {
        /// <summary>
        /// The image format regex.
        /// </summary>
        private static readonly Regex FormatRegex = new Regex(@"(\.?)(j(pg|peg)|bmp|png|gif|ti(ff|f)|ico)", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.RightToLeft);

        /// <summary>
        /// The image format regex for matching the file format at the end of a string.
        /// </summary>
        private static readonly Regex EndFormatRegex = new Regex(@"(\.)(j(pg|peg)|bmp|png|gif|ti(ff|f)|ico)$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.RightToLeft);

        /// <summary>
        /// Checks a given string to check whether the value contains a valid image extension.
        /// </summary>
        /// <param name="fileName">The string containing the filename to check.</param>
        /// <returns>True the value contains a valid image extension, otherwise false.</returns>
        public static bool IsValidImageExtension(string fileName)
        {
            return EndFormatRegex.IsMatch(fileName);
        }

        /// <summary>
        /// Returns the correct file extension for the given string input
        /// </summary>
        /// <param name="input">
        /// The string to parse.
        /// </param>
        /// <returns>
        /// The correct file extension for the given string input if it can find one; otherwise an empty string.
        /// </returns>
        public static string GetExtension(string input)
        {
            Match match = FormatRegex.Matches(input)[0];

            return match.Success ? match.Value : string.Empty;
        }

        /// <summary>
        /// Returns the correct image format based on the given file extension.
        /// </summary>
        /// <param name="fileName">The string containing the filename to check against.</param>
        /// <returns>The correct image format based on the given filename.</returns>
        //public static ImageFormat GetImageFormat(string fileName)
        //{
        //    string extension = Path.GetExtension(fileName);

        //    if (extension != null)
        //    {
        //        string ext = extension.ToUpperInvariant();

        //        switch (ext)
        //        {
        //            case ".ICO":
        //                return ImageFormat.Icon;
        //            case ".PNG":
        //                return ImageFormat.Png;
        //            case ".BMP":
        //                return ImageFormat.Bmp;
        //            case ".GIF":
        //                return ImageFormat.Gif;
        //            case ".TIF":
        //            case ".TIFF":
        //                return ImageFormat.Tiff;
        //            default:
        //                // Should be a jpeg.
        //                return ImageFormat.Jpeg;
        //        }
        //    }

        //    // TODO: Show custom exception?
        //    return null;
        //}
    }
}
