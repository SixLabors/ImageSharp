// <copyright file="PhysicalFileSystemCache.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Web.Caching
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Caching.Distributed;

    /// <summary>
    /// Implements a physical file system based cache.
    /// </summary>
    public class PhysicalFileSystemCache : IDistributedCache
    {
        /// <inheritdoc/>
        public byte[] Get(string key)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public Task<byte[]> GetAsync(string key)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public void Refresh(string key)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public Task RefreshAsync(string key)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public void Remove(string key)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public Task RemoveAsync(string key)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            throw new NotImplementedException();
        }
    }
}