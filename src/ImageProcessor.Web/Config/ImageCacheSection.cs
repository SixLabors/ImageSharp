// -----------------------------------------------------------------------
// <copyright file="ImageCacheSection.cs" company="James South">
//     Copyright (c) James South.
//     Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace ImageProcessor.Web.Config
{
    #region Using
    using System.Configuration;
    using ImageProcessor.Helpers.Extensions;
    #endregion

    /// <summary>
    /// Represents an image cache section within a configuration file.
    /// </summary>
    public sealed class ImageCacheSection : ConfigurationSection
    {
        /// <summary>
        /// Gets or sets the virtual path of the cache folder.
        /// </summary>
        /// <value>The name of the cache folder.</value>
        [ConfigurationProperty("virtualPath", DefaultValue = "~/cache", IsRequired = true)]
        [StringValidator(MinLength = 3, MaxLength = 200)]
        public string VirtualPath
        {
            get
            {
                string virtualPath = (string)this["virtualPath"];

                return virtualPath.IsValidVirtualPathName() ? virtualPath : "~/cache";
            }

            set
            {
                this["virtualPath"] = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum number of days to store an image in the cache.
        /// </summary>
        /// <value>The maximum number of days to store an image in the cache.</value>
        /// <remarks>Defaults to 28 if not set.</remarks>
        [ConfigurationProperty("maxDays", DefaultValue = "28", IsRequired = false)]
        [IntegerValidator(ExcludeRange = false, MinValue = 0)]
        public int MaxDays
        {
            get
            {
                return (int)this["maxDays"];
            }

            set
            {
                this["maxDays"] = value;
            }
        }

        /// <summary>
        /// Retrieves the cache configuration section from the current application configuration. 
        /// </summary>
        /// <returns>The cache configuration section from the current application configuration.</returns>
        public static ImageCacheSection GetConfiguration()
        {
            ImageCacheSection imageCacheSection = ConfigurationManager.GetSection("imageProcessor/cache") as ImageCacheSection;

            if (imageCacheSection != null)
            {
                return imageCacheSection;
            }

            return new ImageCacheSection();
        }
    }
}
