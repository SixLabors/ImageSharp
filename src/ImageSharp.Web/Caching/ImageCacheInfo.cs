// <copyright file="ImageCacheInfo.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Web.Caching
{
    using System;

    /// <summary>
    /// Contains information about a cached image instance
    /// </summary>
    public struct ImageCacheInfo : IEquatable<ImageCacheInfo>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ImageCacheInfo"/> struct.
        /// </summary>
        /// <param name="expired">Whether the cached image is expired</param>
        /// <param name="lastModified">The last modified date of the cached image</param>
        /// <param name="length">The length, in bytes, of the cached image</param>
        public ImageCacheInfo(bool expired, DateTimeOffset lastModified, long length)
        {
            this.Expired = expired;
            this.LastModified = lastModified;
            this.Length = length;
        }

        /// <summary>
        /// Gets a value indicating whether the cached image is expired
        /// </summary>
        public bool Expired { get; }

        /// <summary>
        /// Gets the last modified date of the cached image
        /// </summary>
        public DateTimeOffset LastModified { get; }

        /// <summary>
        /// Gets the length, in bytes, of the cached image
        /// </summary>
        public long Length { get; }

        /// <inheritdoc/>
        public bool Equals(ImageCacheInfo other)
        {
            return this.Expired == other.Expired && this.LastModified.Equals(other.LastModified) && this.Length == other.Length;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is ImageCacheInfo && this.Equals((ImageCacheInfo)obj);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = this.Expired.GetHashCode();
                hashCode = (hashCode * 397) ^ this.LastModified.GetHashCode();
                hashCode = (hashCode * 397) ^ this.Length.GetHashCode();
                return hashCode;
            }
        }
    }
}