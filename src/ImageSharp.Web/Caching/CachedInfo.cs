// <copyright file="CachedInfo.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Web.Caching
{
    using System;

    /// <summary>
    /// Contains information about a cached image instance
    /// </summary>
    public struct CachedInfo : IEquatable<CachedInfo>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CachedInfo"/> struct.
        /// </summary>
        /// <param name="expired">Whether the cached image is expired</param>
        /// <param name="lastModified">The date and time, in coordinated universal time (UTC), the cached image was last written to</param>
        /// <param name="length">The length, in bytes, of the cached image</param>
        public CachedInfo(bool expired, DateTimeOffset lastModified, long length)
        {
            this.Expired = expired;
            this.LastModifiedUtc = lastModified;
            this.Length = length;
        }

        /// <summary>
        /// Gets a value indicating whether the cached image is expired
        /// </summary>
        public bool Expired { get; }

        /// <summary>
        /// Gets the date and time, in coordinated universal time (UTC), the cached image was last written to
        /// </summary>
        public DateTimeOffset LastModifiedUtc { get; }

        /// <summary>
        /// Gets the length, in bytes, of the cached image
        /// </summary>
        public long Length { get; }

        /// <inheritdoc/>
        public bool Equals(CachedInfo other)
        {
            return this.Expired == other.Expired && this.LastModifiedUtc.Equals(other.LastModifiedUtc) && this.Length == other.Length;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is CachedInfo && this.Equals((CachedInfo)obj);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = this.Expired.GetHashCode();
                hashCode = (hashCode * 397) ^ this.LastModifiedUtc.GetHashCode();
                hashCode = (hashCode * 397) ^ this.Length.GetHashCode();
                return hashCode;
            }
        }
    }
}