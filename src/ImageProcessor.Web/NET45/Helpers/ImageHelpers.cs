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

    using System.Text;
    using System.Text.RegularExpressions;

    using ImageProcessor.Configuration;
    using ImageProcessor.Imaging.Formats;

    #endregion

    /// <summary>
    /// The image helpers.
    /// </summary>
    public static class ImageHelpers
    {
        /// <summary>
        /// The regex pattern.
        /// </summary>
        private static readonly string RegexPattern = BuildRegexPattern();

        /// <summary>
        /// The image format regex.
        /// </summary>
        private static readonly Regex FormatRegex = new Regex(@"(\.?)" + RegexPattern, RegexOptions.IgnoreCase | RegexOptions.RightToLeft);

        /// <summary>
        /// The image format regex for matching the file format at the end of a string.
        /// </summary>
        private static readonly Regex EndFormatRegex = new Regex(@"(\.)" + RegexPattern + "$", RegexOptions.IgnoreCase | RegexOptions.RightToLeft);

        /// <summary>
        /// Checks a given string to check whether the value contains a valid image extension.
        /// </summary>
        /// <param name="fileName">The string containing the filename to check.</param>
        /// <returns>True the value contains a valid image extension, otherwise false.</returns>
        public static bool IsValidImageExtension(string fileName)
        {
            Match match = EndFormatRegex.Matches(fileName)[0];

            if (match.Success && !match.Value.ToLowerInvariant().EndsWith("png8"))
            {
                return true;
            }

            return false;
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

            if (match.Success)
            {
                // Ah the enigma that is the png file.
                if (match.Value.ToLowerInvariant().EndsWith("png8"))
                {
                    return "png";
                }

                return match.Value;
            }

            return string.Empty;
        }

        /// <summary>
        /// Builds a regular expression from the <see cref="T:ImageProcessor.Imaging.Formats.ISupportedImageFormat"/> type, this allows extensibility.
        /// </summary>
        /// <returns>
        /// The <see cref="Regex"/> to match matrix filters.
        /// </returns>
        private static string BuildRegexPattern()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append("(");
            int counter = 0;
            foreach (ISupportedImageFormat imageFormat in ImageProcessorBootstrapper.Instance.SupportedImageFormats)
            {
                foreach (string fileExtension in imageFormat.FileExtensions)
                {
                    if (counter == 0)
                    {
                        stringBuilder.Append(fileExtension.ToLowerInvariant());
                    }
                    else
                    {
                        stringBuilder.AppendFormat("|{0}", fileExtension.ToLowerInvariant());
                    }
                }

                counter++;
            }

            stringBuilder.Append(")");
            return stringBuilder.ToString();
        }
    }
}