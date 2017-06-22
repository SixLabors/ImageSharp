// <copyright file="Configuration.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Threading.Tasks;

    using Formats;
    using ImageSharp.IO;

    /// <summary>
    /// Provides initialization code which allows extending the library.
    /// </summary>
    public class Configuration : IImageFormatHost
    {
        /// <summary>
        /// A lazily initialized configuration default instance.
        /// </summary>
        private static readonly Lazy<Configuration> Lazy = new Lazy<Configuration>(() => CreateDefaultInstance());

        /// <summary>
        /// An object that can be used to synchronize access to the <see cref="Configuration"/>.
        /// </summary>
        private readonly object syncRoot = new object();

        /// <summary>
        /// The list of supported <see cref="IImageEncoder"/> keyed to mimestypes.
        /// </summary>
        private readonly ConcurrentDictionary<string, IImageEncoder> mimeTypeEncoders = new ConcurrentDictionary<string, IImageEncoder>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The list of supported <see cref="IImageEncoder"/> keyed to fiel extensions.
        /// </summary>
        private readonly ConcurrentDictionary<string, IImageEncoder> extensionsEncoders = new ConcurrentDictionary<string, IImageEncoder>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The list of supported <see cref="IImageEncoder"/> keyed to mimestypes.
        /// </summary>
        private readonly ConcurrentDictionary<string, IImageDecoder> mimeTypeDecoders = new ConcurrentDictionary<string, IImageDecoder>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The list of supported <see cref="IMimeTypeDetector"/>s.
        /// </summary>
        private readonly List<IMimeTypeDetector> mimeTypeDetectors = new List<IMimeTypeDetector>();

        /// <summary>
        /// Initializes a new instance of the <see cref="Configuration" /> class.
        /// </summary>
        public Configuration()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Configuration" /> class.
        /// </summary>
        /// <param name="providers">A collection of providers to configure</param>
        public Configuration(params IImageFormatProvider[] providers)
        {
            if (providers != null)
            {
                foreach (IImageFormatProvider p in providers)
                {
                    p.Configure(this);
                }
            }
        }

        /// <summary>
        /// Gets the default <see cref="Configuration"/> instance.
        /// </summary>
        public static Configuration Default { get; } = Lazy.Value;

        /// <summary>
        /// Gets the global parallel options for processing tasks in parallel.
        /// </summary>
        public ParallelOptions ParallelOptions { get; } = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };

        /// <summary>
        /// Gets the maximum header size of all formats.
        /// </summary>
        internal int MaxHeaderSize { get; private set; }

        /// <summary>
        /// Gets the currently registerd <see cref="IMimeTypeDetector"/>s.
        /// </summary>
        internal IEnumerable<IMimeTypeDetector> MimeTypeDetectors => this.mimeTypeDetectors;

        /// <summary>
        /// Gets the typeof of all the current image decoders
        /// </summary>
        internal IEnumerable<Type> AllMimeImageDecoders => this.mimeTypeDecoders.Select(x => x.Value.GetType()).Distinct().ToList();

        /// <summary>
        /// Gets the typeof of all the current image decoders
        /// </summary>
        internal IEnumerable<Type> AllMimeImageEncoders => this.mimeTypeEncoders.Select(x => x.Value.GetType()).Distinct().ToList();

        /// <summary>
        /// Gets the typeof of all the current image decoders
        /// </summary>
        internal IEnumerable<Type> AllExtImageEncoders => this.mimeTypeEncoders.Select(x => x.Value.GetType()).Distinct().ToList();

#if !NETSTANDARD1_1
        /// <summary>
        /// Gets or sets the fielsystem helper for accessing the local file system.
        /// </summary>
        internal IFileSystem FileSystem { get; set; } = new LocalFileSystem();
#endif

        /// <summary>
        /// Registers a new format provider.
        /// </summary>
        /// <param name="formatProvider">The format providers to call configure on.</param>
        public void AddImageFormat(IImageFormatProvider formatProvider)
        {
            Guard.NotNull(formatProvider, nameof(formatProvider));
            formatProvider.Configure(this);
        }

        /// <inheritdoc />
        public void SetMimeTypeEncoder(string mimeType, IImageEncoder encoder)
        {
            Guard.NotNullOrEmpty(mimeType, nameof(mimeType));
            Guard.NotNull(encoder, nameof(encoder));
            this.mimeTypeEncoders.AddOrUpdate(mimeType?.Trim(), encoder, (s, e) => encoder);
        }

        /// <inheritdoc />
        public void SetFileExtensionEncoder(string extension, IImageEncoder encoder)
        {
            Guard.NotNullOrEmpty(extension, nameof(extension));
            Guard.NotNull(encoder, nameof(encoder));
            this.extensionsEncoders.AddOrUpdate(extension?.Trim(), encoder, (s, e) => encoder);
        }

        /// <inheritdoc />
        public void SetMimeTypeDecoder(string mimeType, IImageDecoder decoder)
        {
            Guard.NotNullOrEmpty(mimeType, nameof(mimeType));
            Guard.NotNull(decoder, nameof(decoder));
            this.mimeTypeDecoders.AddOrUpdate(mimeType, decoder, (s, e) => decoder);
        }

        /// <summary>
        /// Removes all the registerd detectors
        /// </summary>
        public void ClearMimeTypeDetector()
        {
            this.mimeTypeDetectors.Clear();
        }

        /// <inheritdoc />
        public void AddMimeTypeDetector(IMimeTypeDetector detector)
        {
            Guard.NotNull(detector, nameof(detector));
            this.mimeTypeDetectors.Add(detector);
            this.SetMaxHeaderSize();
        }

        /// <summary>
        /// For the specified mimetype find the decoder.
        /// </summary>
        /// <param name="mimeType">the mimetype to discover</param>
        /// <returns>the IImageDecoder if found othersize null </returns>
        public IImageDecoder FindMimeTypeDecoder(string mimeType)
        {
            Guard.NotNullOrEmpty(mimeType, nameof(mimeType));
            if (this.mimeTypeDecoders.TryGetValue(mimeType, out IImageDecoder dec))
            {
                return dec;
            }

            return null;
        }

        /// <summary>
        /// For the specified mimetype find the encoder.
        /// </summary>
        /// <param name="mimeType">the mimetype to discover</param>
        /// <returns>the IImageEncoder if found othersize null </returns>
        public IImageEncoder FindMimeTypeEncoder(string mimeType)
        {
            Guard.NotNullOrEmpty(mimeType, nameof(mimeType));
            if (this.mimeTypeEncoders.TryGetValue(mimeType, out IImageEncoder dec))
            {
                return dec;
            }

            return null;
        }

        /// <summary>
        /// For the specified mimetype find the encoder.
        /// </summary>
        /// <param name="extensions">the extensions to discover</param>
        /// <returns>the IImageEncoder if found othersize null </returns>
        public IImageEncoder FindFileExtensionsEncoder(string extensions)
        {
            extensions = extensions?.TrimStart('.');
            Guard.NotNullOrEmpty(extensions, nameof(extensions));
            if (this.extensionsEncoders.TryGetValue(extensions, out IImageEncoder dec))
            {
                return dec;
            }

            return null;
        }

        /// <summary>
        /// Creates the default instance, with Png, Jpeg, Gif and Bmp preregisterd (if they have been referenced)
        /// </summary>
        /// <returns>The default configuration of <see cref="Configuration"/> </returns>
        internal static Configuration CreateDefaultInstance()
        {
            return new Configuration(
                new PngImageFormatProvider(),
                new JpegImageFormatProvider(),
                new GifImageFormatProvider(),
                new BmpImageFormatProvider());
        }

        /// <summary>
        /// Sets max header size.
        /// </summary>
        private void SetMaxHeaderSize()
        {
            this.MaxHeaderSize = this.mimeTypeDetectors.Max(x => x.HeaderSize);
        }
    }
}
