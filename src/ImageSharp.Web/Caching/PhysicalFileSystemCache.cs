// <copyright file="PhysicalFileSystemCache.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Web.Caching
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    using ImageSharp.Memory;

    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.FileProviders;

    /// <summary>
    /// Implements a physical file system based cache.
    /// </summary>
    public class PhysicalFileSystemCache : IImageCache
    {
        /// <summary>
        /// The configuration key for determining the cache folder.
        /// </summary>
        public const string Folder = "CacheFolder";

        /// <summary>
        /// The hosting environment the application is running in.
        /// </summary>
        private readonly IHostingEnvironment environment;

        /// <summary>
        /// The file provider abstraction.
        /// </summary>
        private readonly IFileProvider fileProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="PhysicalFileSystemCache"/> class.
        /// </summary>
        /// <param name="environment">The hosting environment the application is running in</param>
        public PhysicalFileSystemCache(IHostingEnvironment environment)
        {
            this.environment = environment;
            this.fileProvider = this.environment.WebRootFileProvider;
        }

        /// <inheritdoc/>
        public IDictionary<string, string> Settings { get; set; }
            = new Dictionary<string, string>
            {
                { Folder, "is-cache" }
            };

        /// <inheritdoc/>
        public async Task<CachedBuffer> GetAsync(string key)
        {
            IFileInfo fileInfo = this.fileProvider.GetFileInfo(this.ToFilePath(key));

            byte[] buffer;

            // Check to see if the file exists.
            if (!fileInfo.Exists)
            {
                return default(CachedBuffer);
            }

            long length;
            using (Stream stream = fileInfo.CreateReadStream())
            {
                length = stream.Length;

                // Buffer is returned to the pool in the middleware
                buffer = BufferDataPool.Rent((int)length);
                await stream.ReadAsync(buffer, 0, (int)length);
            }

            return new CachedBuffer(buffer, length);
        }

        /// <inheritdoc/>
        public async Task<CachedInfo> IsExpiredAsync(string key, DateTime minDateUtc)
        {
            // TODO do we use an in memory cache to reduce IO?
            IFileInfo fileInfo = this.fileProvider.GetFileInfo(this.ToFilePath(key));

            // Check if the file exists and whether the last modified date is less than the min date.
            bool exists = fileInfo.Exists;
            DateTimeOffset lastModified = exists ? fileInfo.LastModified : DateTimeOffset.MinValue;
            long length = exists ? fileInfo.Length : 0;
            bool expired = !exists || fileInfo.LastModified.UtcDateTime < minDateUtc;

            // TODO: Task.FromResult ok?
            return await Task.FromResult(new CachedInfo(expired, lastModified, length));
        }

        /// <inheritdoc/>
        public async Task<DateTimeOffset> SetAsync(string key, byte[] value, int length)
        {
            string path = Path.Combine(this.environment.WebRootPath, this.ToFilePath(key));
            string directory = Path.GetDirectoryName(path);

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (FileStream fileStream = File.Create(path))
            {
                await fileStream.WriteAsync(value, 0, length);
            }

            return File.GetLastWriteTimeUtc(path);
        }

        /// <summary>
        /// Converts the key into a nested file path.
        /// </summary>
        /// <param name="key">The cache key.</param>
        /// <returns>The <see cref="string"/></returns>
        private string ToFilePath(string key)
        {
            return $"{this.Settings[Folder]}/{string.Join("/", key.Substring(0, 8).ToCharArray())}/{key}";
        }
    }
}