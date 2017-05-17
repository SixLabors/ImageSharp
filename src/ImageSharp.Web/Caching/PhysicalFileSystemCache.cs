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
        private const string Folder = "CachedFolder";

        /// <inheritdoc/>
        public IDictionary<string, string> Settings { get; set; }
            = new Dictionary<string, string>
            {
                { Folder, "is-cache" }
            };

        /// <inheritdoc/>
        public async Task<CachedBuffer> GetAsync(IHostingEnvironment environment, string key)
        {
            IFileProvider fileProvider = environment.WebRootFileProvider;
            IFileInfo fileInfo = fileProvider.GetFileInfo(this.ToFilePath(key));

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
        public async Task<CachedInfo> IsExpiredAsync(IHostingEnvironment environment, string key, DateTime minDateUtc)
        {
            // TODO do we use an in memory cache to reduce IO?
            IFileProvider fileProvider = environment.WebRootFileProvider;
            IFileInfo fileInfo = fileProvider.GetFileInfo(this.ToFilePath(key));

            // Check if the file exists and whether the last modified date is less than the min date.
            bool exists = fileInfo.Exists;
            DateTimeOffset lastModified = exists ? fileInfo.LastModified : DateTimeOffset.MinValue;
            long length = exists ? fileInfo.Length : 0;
            bool expired = !exists || fileInfo.LastModified.UtcDateTime < minDateUtc;

            // TODO: Task.FromResult ok?
            return await Task.FromResult(new CachedInfo(expired, lastModified, length));
        }

        /// <inheritdoc/>
        public async Task<DateTimeOffset> SetAsync(IHostingEnvironment environment, string key, byte[] value)
        {
            IFileProvider fileProvider = environment.WebRootFileProvider;
            IFileInfo fileInfo = fileProvider.GetFileInfo(this.ToFilePath(key));
            string path = fileInfo.PhysicalPath;
            string directory = Path.GetDirectoryName(path);

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (FileStream fileStream = File.Create(path))
            {
                await fileStream.WriteAsync(value, 0, value.Length);
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
            return $"/{this.Settings[Folder]}/{string.Join("/", key.Substring(0, 8).ToCharArray())}/{key}";
        }
    }
}