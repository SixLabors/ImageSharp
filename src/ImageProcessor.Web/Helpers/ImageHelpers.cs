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
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using ImageProcessor.Configuration;
    using ImageProcessor.Imaging.Formats;
    using ImageProcessor.Web.Configuration;
    using ImageProcessor.Web.Processors;

    /// <summary>
    /// The image helpers.
    /// </summary>
    public static class ImageHelpers
    {
        /// <summary>
        /// The regex pattern.
        /// </summary>
        public static readonly string ExtensionRegexPattern = BuildExtensionRegexPattern();

        /// <summary>
        /// The image format regex.
        /// </summary>
        private static readonly Regex FormatRegex = new Regex(@"(\.?)(png8|" + ExtensionRegexPattern + ")", RegexOptions.IgnoreCase | RegexOptions.RightToLeft);

        /// <summary>
        /// The image format regex for matching the file format at the end of a string.
        /// </summary>
        private static readonly Regex EndFormatRegex = new Regex(@"(\.)" + ExtensionRegexPattern + "$", RegexOptions.IgnoreCase | RegexOptions.RightToLeft);

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
        /// <param name="fullPath">
        /// The string to parse.
        /// </param>
        /// <param name="queryString">
        /// The querystring containing instructions.
        /// </param>
        /// <returns>
        /// The correct file extension for the given string input if it can find one; otherwise an empty string.
        /// </returns>
        public static string GetExtension(string fullPath, string queryString)
        {
            Match match = null;

            // First check to see if the format processor is being used and test against that.
            IWebGraphicsProcessor format = ImageProcessorConfiguration.Instance.GraphicsProcessors
                                           .FirstOrDefault(p => typeof(Format) == p.GetType());

            if (format != null)
            {
                match = format.RegexPattern.Match(queryString);
            }

            if (match == null || !match.Success)
            {
                // Test against the path minus the querystring so any other
                // processors don't interere.
                string trimmed = fullPath;
                if (queryString != null)
                {
                    trimmed = trimmed.Replace(queryString, string.Empty);
                }

                match = FormatRegex.Match(trimmed);
            }

            if (match.Success)
            {
                string value = match.Value;

                // Clip if format processor match.
                if (match.Value.Contains("="))
                {
                    value = value.Split('=')[1];
                }

                // Ah the enigma that is the png file.
                if (value.ToLowerInvariant().EndsWith("png8"))
                {
                    return "png";
                }

                return value;
            }

            return string.Empty;
        }

        /// <summary>
        /// Builds a regular expression from the <see cref="T:ImageProcessor.Imaging.Formats.ISupportedImageFormat"/> type, this allows extensibility.
        /// </summary>
        /// <returns>
        /// The <see cref="Regex"/> to match matrix filters.
        /// </returns>
        private static string BuildExtensionRegexPattern()
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