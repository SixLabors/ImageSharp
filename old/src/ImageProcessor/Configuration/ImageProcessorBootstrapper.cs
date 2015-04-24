// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ImageProcessorBootstrapper.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   The ImageProcessor bootstrapper containing initialization code for extending ImageProcessor.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using ImageProcessor.Common.Extensions;
    using ImageProcessor.Common.Helpers;
    using ImageProcessor.Imaging.Formats;

    /// <summary>
    /// The ImageProcessor bootstrapper containing initialization code for extending ImageProcessor.
    /// </summary>
    public class ImageProcessorBootstrapper
    {
        /// <summary>
        /// A new instance Initializes a new instance of the <see cref="ImageProcessorBootstrapper"/> class.
        /// with lazy initialization.
        /// </summary>
        private static readonly Lazy<ImageProcessorBootstrapper> Lazy =
                        new Lazy<ImageProcessorBootstrapper>(() => new ImageProcessorBootstrapper());

        /// <summary>
        /// Prevents a default instance of the <see cref="ImageProcessorBootstrapper"/> class from being created.
        /// </summary>
        private ImageProcessorBootstrapper()
        {
            this.NativeBinaryFactory = new NativeBinaryFactory();
            this.LoadSupportedImageFormats();
        }

        /// <summary>
        /// Gets the current instance of the <see cref="ImageProcessorBootstrapper"/> class.
        /// </summary>
        public static ImageProcessorBootstrapper Instance
        {
            get
            {
                return Lazy.Value;
            }
        }

        /// <summary>
        /// Gets the supported image formats.
        /// </summary>
        public IEnumerable<ISupportedImageFormat> SupportedImageFormats { get; private set; }

        /// <summary>
        /// Gets the native binary factory for registering embedded (unmanaged) binaries.
        /// </summary>
        public NativeBinaryFactory NativeBinaryFactory { get; private set; }

        /// <summary>
        /// Creates a list, using reflection, of supported image formats that ImageProcessor can run.
        /// </summary>
        private void LoadSupportedImageFormats()
        {
            Type type = typeof(ISupportedImageFormat);
            if (this.SupportedImageFormats == null)
            {
                List<Type> availableTypes =
                    TypeFinder.GetAssembliesWithKnownExclusions()
                        .SelectMany(a => a.GetLoadableTypes())
                        .Where(t => type.IsAssignableFrom(t) && t.IsClass && !t.IsAbstract)
                        .ToList();

                this.SupportedImageFormats =
                    availableTypes.Select(f => (Activator.CreateInstance(f) as ISupportedImageFormat))
                    .ToList();
            }
        }
    }
}
