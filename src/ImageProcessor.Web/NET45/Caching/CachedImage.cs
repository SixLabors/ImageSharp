namespace ImageProcessor.Web.Caching
{
    #region Using
    using System;
    #endregion

    /// <summary>
    /// Describes a cached image 
    /// </summary>
    public sealed class CachedImage
    {
        /// <summary>
        /// Gets or sets the key identifying the cached image.
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// Gets or sets the value of the cached image.
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// Gets or sets the last write time of the cached image.
        /// </summary>
        public DateTime LastWriteTimeUtc { get; set; }
    }
}
