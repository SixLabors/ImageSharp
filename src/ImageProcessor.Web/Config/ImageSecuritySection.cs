// -----------------------------------------------------------------------
// <copyright file="ImageSecuritySection.cs" company="James South">
//     Copyright (c) James South.
//     Dual licensed under the MIT or GPL Version 2 licenses.
// </copyright>
// -----------------------------------------------------------------------

namespace ImageProcessor.Web.Config
{
    #region Using
    using System;
    using System.Configuration;
    #endregion

    /// <summary>
    /// Represents an imagecache section within a configuration file.
    /// </summary>
    public class ImageSecuritySection : ConfigurationSection
    {
        #region Properties
        /// <summary>
        /// Gets or sets a value indicating whether the current application is allowed download remote files.
        /// </summary>
        /// <value><see langword="true"/> if the current application is allowed download remote files; otherwise, <see langword="false"/>.</value>
        [ConfigurationProperty("allowRemoteDownloads", DefaultValue = false, IsRequired = true)]
        public bool AllowRemoteDownloads
        {
            get { return (bool)this["allowRemoteDownloads"]; }
            set { this["allowRemoteDownloads"] = value; }
        }

        /// <summary>
        /// Gets or sets the maximum allowed remote file timeout in milliseconds for the application.
        /// </summary>
        /// <value>The maximum number of days to store an image in the cache.</value>
        /// <remarks>Defaults to 30000 (30 seconds) if not set.</remarks>
        [ConfigurationProperty("timeout", DefaultValue = "300000", IsRequired = true)]
        public int Timeout
        {
            get
            {
                return (int)this["timeout"];
            }

            set
            {
                this["timeout"] = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum allowed remote file size in bytes for the application.
        /// </summary>
        /// <value>The maximum number of days to store an image in the cache.</value>
        /// <remarks>Defaults to 524288 (512kb) if not set.</remarks>
        [ConfigurationProperty("maxBytes", DefaultValue = "524288", IsRequired = true)]
        public int MaxBytes
        {
            get
            {
                return (int)this["maxBytes"];
            }

            set
            {
                this["maxBytes"] = value;
            }
        }

        /// <summary>
        /// Gets or sets the prefix for remote files for the application.
        /// </summary>
        /// <value>The prefix for remote files for the application.</value>
        [ConfigurationProperty("remotePrefix", DefaultValue = "", IsRequired = true)]
        public string RemotePrefix
        {
            get { return (string)this["remotePrefix"]; }

            set { this["remotePrefix"] = value; }
        }

        /// <summary>
        /// Gets the <see cref="T:ImageProcessor.Web.Config.ImageSecuritySection.WhiteListElementCollection"/>
        /// </summary>
        /// <value>The <see cref="T:ImageProcessor.Web.Config.ImageSecuritySection.WhiteListElementCollection"/></value>
        [ConfigurationProperty("whiteList", IsRequired = true)]
        public WhiteListElementCollection WhiteList
        {
            get
            {
                object o = this["whiteList"];
                return o as WhiteListElementCollection;
            }
        }
        #endregion

        #region Methods
        /// <summary>
        /// Retrieves the security configuration section from the current application configuration. 
        /// </summary>
        /// <returns>The cache configuration section from the current application configuration.</returns>
        public static ImageSecuritySection GetConfiguration()
        {
            ImageSecuritySection imageSecuritySection = ConfigurationManager.GetSection("imageProcessor/security") as ImageSecuritySection;

            if (imageSecuritySection != null)
            {
                return imageSecuritySection;
            }

            return new ImageSecuritySection();
        }
        #endregion

        /// <summary>
        /// Represents a whitelist collection configuration element within the configuration.
        /// </summary>
        public class WhiteListElementCollection : ConfigurationElementCollection
        {
            /// <summary>
            /// Gets or sets the whitelist item at the given index.
            /// </summary>
            /// <param name="index">The index of the whitelist item to get.</param>
            /// <returns>The whitelist item at the given index.</returns>
            public SafeURL this[int index]
            {
                get
                {
                    return this.BaseGet(index) as SafeURL;
                }

                set
                {
                    if (this.BaseGet(index) != null)
                    {
                        this.BaseRemoveAt(index);
                    }

                    this.BaseAdd(index, value);
                }
            }

            /// <summary>
            /// Creates a new SafeURL configuration element.
            /// </summary>
            /// <returns>
            /// A new SafeURL configuration element.
            /// </returns>
            protected override ConfigurationElement CreateNewElement()
            {
                return new SafeURL();
            }

            /// <summary>
            /// Gets the element key for a specified whitelist configuration element.
            /// </summary>
            /// <param name="element">The <see cref="T:System.Configuration.ConfigurationElement">ConfigurationElement</see> to return the key for.</param>
            /// <returns>The element key for a specified whitelist configuration element.</returns>
            protected override object GetElementKey(ConfigurationElement element)
            {
                return ((SafeURL)element).Url;
            }
        }

        /// <summary>
        /// Represents a whitelist configuration element within the configuration.
        /// </summary>
        public class SafeURL : ConfigurationElement
        {
            /// <summary>
            /// Gets or sets the url of the whitelisted file.
            /// </summary>
            /// <value>The url of the whitelisted file.</value>
            [ConfigurationProperty("url", DefaultValue = "", IsRequired = true)]
            public Uri Url
            {
                get { return (Uri)this["url"]; }

                set { this["url"] = value; }
            }
        }
    }
}
