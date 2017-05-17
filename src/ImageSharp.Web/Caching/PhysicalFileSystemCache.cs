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
                { Folder, "iscache" }
            };

        /// <inheritdoc/>
        public async Task<byte[]> GetAsync(IHostingEnvironment environment, string key)
        {
            IFileProvider fileProvider = environment.WebRootFileProvider;
            IFileInfo fileInfo = fileProvider.GetFileInfo(this.ToFilePath(key));

            byte[] buffer;

            // Check to see if the file exists.
            if (!fileInfo.Exists)
            {
                return null;
            }

            using (Stream stream = fileInfo.CreateReadStream())
            {
                // TODO: There's no way for us to pool this is there :(
                buffer = new byte[stream.Length];
                await stream.ReadAsync(buffer, 0, (int)stream.Length);
            }

            return buffer;
        }

        /// <inheritdoc/>
        public async Task<bool> IsExpiredAsync(IHostingEnvironment environment, string key, DateTime maxDateUtc)
        {
            // TODO do we use an in memory cache to reduce IO?
            IFileProvider fileProvider = environment.WebRootFileProvider;
            IFileInfo fileInfo = fileProvider.GetFileInfo(this.ToFilePath(key));

            // Check if the file exists and whether the last modified date is greater than the max date.
            // TODO: Task.FromResult ok?
            return await Task.FromResult(!fileInfo.Exists || fileInfo.LastModified.UtcDateTime < maxDateUtc);
        }

        /// <inheritdoc/>
        public async Task SetAsync(IHostingEnvironment environment, string key, byte[] value, DateTime creationDateUtc)
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