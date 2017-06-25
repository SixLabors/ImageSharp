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
        /// The list of supported <see cref="IImageEncoder"/> keyed to mime types.
        /// </summary>
        private readonly ConcurrentDictionary<string, IImageEncoder> mimeTypeEncoders = new ConcurrentDictionary<string, IImageEncoder>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The list of supported mime types keyed to file extensions.
        /// </summary>
        private readonly ConcurrentDictionary<string, string> extensionsMap = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The list of supported <see cref="IImageEncoder"/> keyed to mime types.
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
        /// Gets the maximum header size of all the formats.
        /// </summary>
        internal int MaxHeaderSize { get; private set; }

        /// <summary>
        /// Gets the currently registered <see cref="IMimeTypeDetector"/>s.
        /// </summary>
        internal IEnumerable<IMimeTypeDetector> MimeTypeDetectors => this.mimeTypeDetectors;

        /// <summary>
        /// Gets the currently registered <see cref="IImageDecoder"/>s.
        /// </summary>
        internal IEnumerable<KeyValuePair<string, IImageDecoder>> ImageDecoders => this.mimeTypeDecoders;

        /// <summary>
        /// Gets the currently registered <see cref="IImageEncoder"/>s.
        /// </summary>
        internal IEnumerable<KeyValuePair<string, IImageEncoder>> ImageEncoders => this.mimeTypeEncoders;

        /// <summary>
        /// Gets the currently registered file extensions.
        /// </summary>
        internal IEnumerable<KeyValuePair<string, string>> ImageExtensionToMimeTypeMapping => this.extensionsMap;

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
        public void SetFileExtensionToMimeTypeMapping(string extension, string mimeType)
        {
            Guard.NotNullOrEmpty(extension, nameof(extension));
            Guard.NotNullOrEmpty(mimeType, nameof(mimeType));
            this.extensionsMap.AddOrUpdate(extension?.Trim(), mimeType, (s, e) => mimeType);
        }

        /// <inheritdoc />
        public void SetMimeTypeDecoder(string mimeType, IImageDecoder decoder)
        {
            Guard.NotNullOrEmpty(mimeType, nameof(mimeType));
            Guard.NotNull(decoder, nameof(decoder));
            this.mimeTypeDecoders.AddOrUpdate(mimeType, decoder, (s, e) => decoder);
        }

        /// <summary>
        /// Removes all the registered mime type detectors.
        /// </summary>
        public void ClearMimeTypeDetectors()
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
        /// Creates the default instance with the following <see cref="IImageFormatProvider"/>s preregistered:
        /// <para><see cref="PngImageFormatProvider"/></para>
        /// <para><see cref="JpegImageFormatProvider"/></para>
        /// <para><see cref="GifImageFormatProvider"/></para>
        /// <para><see cref="BmpImageFormatProvider"/></para>
        /// </summary>
        /// <returns>The default configuration of <see cref="Configuration"/></returns>
        internal static Configuration CreateDefaultInstance()
        {
            return new Configuration(
                new PngImageFormatProvider(),
                new JpegImageFormatProvider(),
                new GifImageFormatProvider(),
                new BmpImageFormatProvider());
        }

        /// <summary>
        /// For the specified mime type find the decoder.
        /// </summary>
        /// <param name="mimeType">The mime type to discover</param>
        /// <returns>The <see cref="IImageDecoder"/> if found otherwise null</returns>
        internal IImageDecoder FindMimeTypeDecoder(string mimeType)
        {
            Guard.NotNullOrEmpty(mimeType, nameof(mimeType));
            if (this.mimeTypeDecoders.TryGetValue(mimeType, out IImageDecoder decoder))
            {
                return decoder;
            }

            return null;
        }

        /// <summary>
        /// For the specified mime type find the encoder.
        /// </summary>
        /// <param name="mimeType">The mime type to discover</param>
        /// <returns>The <see cref="IImageEncoder"/> if found otherwise null</returns>
        internal IImageEncoder FindMimeTypeEncoder(string mimeType)
        {
            Guard.NotNullOrEmpty(mimeType, nameof(mimeType));
            if (this.mimeTypeEncoders.TryGetValue(mimeType, out IImageEncoder encoder))
            {
                return encoder;
            }

            return null;
        }

        /// <summary>
        /// For the specified mime type find the encoder.
        /// </summary>
        /// <param name="extensions">The extensions to discover</param>
        /// <returns>The <see cref="IImageEncoder"/> if found otherwise null</returns>
        internal IImageEncoder FindFileExtensionsEncoder(string extensions)
        {
            extensions = extensions?.TrimStart('.');
            Guard.NotNullOrEmpty(extensions, nameof(extensions));
            if (this.extensionsMap.TryGetValue(extensions, out string mimeType))
            {
                return this.FindMimeTypeEncoder(mimeType);
            }

            return null;
        }

        /// <summary>
        /// For the specified extension find the mime type.
        /// </summary>
        /// <param name="extensions">the extensions to discover</param>
        /// <returns>The mime type if found otherwise null</returns>
        internal string FindFileExtensionsMimeType(string extensions)
        {
            extensions = extensions?.TrimStart('.');
            Guard.NotNullOrEmpty(extensions, nameof(extensions));
            if (this.extensionsMap.TryGetValue(extensions, out string mimeType))
            {
                return mimeType;
            }

            return null;
        }

        /// <summary>
        /// Sets the max header size.
        /// </summary>
        private void SetMaxHeaderSize()
        {
            this.MaxHeaderSize = this.mimeTypeDetectors.Max(x => x.HeaderSize);
        }
    }
}
