// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;

namespace SixLabors.ImageSharp.Formats
{
    /// <summary>
    /// The base class for all image formats.
    /// Inheriting classes should implement the singleton pattern by creating a private constructor.
    /// </summary>
    /// <typeparam name="T">The type of image format.</typeparam>
    public abstract class ImageFormatBase<T> : IImageFormat
        where T : class, IImageFormat
    {
        private static readonly Lazy<T> Lazy = new Lazy<T>(CreateInstance);

        /// <summary>
        /// Gets the current instance.
        /// </summary>
        public static T Instance => Lazy.Value;

        /// <inheritdoc/>
        public abstract string Name { get; }

        /// <inheritdoc/>
        public abstract string DefaultMimeType { get; }

        /// <inheritdoc/>
        public abstract IEnumerable<string> MimeTypes { get; }

        /// <inheritdoc/>
        public abstract IEnumerable<string> FileExtensions { get; }

        private static T CreateInstance() => (T)Activator.CreateInstance(typeof(T), true);
    }
}
