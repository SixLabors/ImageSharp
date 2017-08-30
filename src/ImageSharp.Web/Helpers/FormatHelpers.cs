// <copyright file="ResponseConstants.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Web.Helpers
{
    using System;
    using System.IO;
    using System.Linq;

    using ImageSharp.Formats;

    /// <summary>
    /// Helper utilities for image formats
    /// </summary>
    public class FormatHelpers
    {
        /// <summary>
        /// Returns the correct content type (mimetype) for the given cached image key.
        /// </summary>
        /// <param name="configuration">The library configuration</param>
        /// <param name="key">The cache key</param>
        /// <returns>The <see cref="string"/></returns>
        public static string GetContentType(Configuration configuration, string key)
        {
            string extension = Path.GetExtension(key).Replace(".", string.Empty);
            return configuration.ImageFormats.First(f => f.FileExtensions.Contains(extension)).DefaultMimeType;
        }

        /// <summary>
        /// Gets the file extension for the given image uri
        /// </summary>
        /// <param name="configuration">The library configuration</param>
        /// <param name="uri">The full request uri</param>
        /// <returns>The <see cref="string"/></returns>
        public static string GetExtension(Configuration configuration, string uri)
        {
            string extension = null;
            int index = 0;
            foreach (IImageFormat format in configuration.ImageFormats)
            {
                foreach (string ext in format.FileExtensions)
                {
                    int i = uri.LastIndexOf(ext, StringComparison.OrdinalIgnoreCase);
                    if (i <= index)
                    {
                        continue;
                    }

                    index = i;
                    extension = ext;
                }
            }

            return extension;
        }

        /// <summary>
        /// Gets the file extension for the given image uri or a default extension from the first available format.
        /// </summary>
        /// <param name="configuration">The library configuration</param>
        /// <param name="uri">The full request uri</param>
        /// <returns>The <see cref="string"/></returns>
        public static string GetExtensionOrDefault(Configuration configuration, string uri)
        {
            return GetExtension(configuration, uri) ?? configuration.ImageFormats.First().FileExtensions.First();
        }
    }
}