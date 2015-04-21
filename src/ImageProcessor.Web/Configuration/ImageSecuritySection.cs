// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ImageSecuritySection.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Represents an image security section within a configuration file.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Web.Configuration
{
    using System;
    using System.Configuration;
    using System.IO;
    using System.Xml;

    using ImageProcessor.Web.Helpers;

    /// <summary>
    /// Represents an image security section within a configuration file.
    /// </summary>
    public sealed class ImageSecuritySection : ConfigurationSection
    {
        /// <summary>
        /// Gets the <see cref="CORSOriginElement"/>
        /// </summary>
        /// <value>The <see cref="CORSOriginElement"/></value>
        [ConfigurationProperty("cors", IsRequired = false)]
        public CORSOriginElement CORSOrigin
        {
            get
            {
                object o = this["cors"];
                return o as CORSOriginElement;
            }
        }

        /// <summary>
        /// Gets the <see cref="ServiceElementCollection"/>
        /// </summary>
        /// <value>The <see cref="ServiceElementCollection"/></value>
        [ConfigurationProperty("services", IsRequired = true)]
        public ServiceElementCollection ImageServices
        {
            get
            {
                object o = this["services"];
                return o as ServiceElementCollection;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether to auto load services.
        /// </summary>
        public bool AutoLoadServices { get; set; }

        /// <summary>
        /// Retrieves the security configuration section from the current application configuration. 
        /// </summary>
        /// <returns>The cache configuration section from the current application configuration.</returns>
        public static ImageSecuritySection GetConfiguration()
        {
            ImageSecuritySection imageSecuritySection = ConfigurationManager.GetSection("imageProcessor/security") as ImageSecuritySection;

            if (imageSecuritySection != null)
            {
                imageSecuritySection.AutoLoadServices = false;
                return imageSecuritySection;
            }

            string section = ResourceHelpers.ResourceAsString("ImageProcessor.Web.Configuration.Resources.security.config.transform");
            XmlReader reader = new XmlTextReader(new StringReader(section));
            imageSecuritySection = new ImageSecuritySection();
            imageSecuritySection.DeserializeSection(reader);
            imageSecuritySection.AutoLoadServices = true;
            return imageSecuritySection;
        }

        /// <summary>
        /// Represents a ServiceElement configuration element within the configuration.
        /// </summary>
        public class ServiceElement : ConfigurationElement
        {
            /// <summary>
            /// Gets or sets the name of the service.
            /// </summary>
            /// <value>The name of the service.</value>
            [ConfigurationProperty("name", DefaultValue = "", IsRequired = true)]
            public string Name
            {
                get { return (string)this["name"]; }

                set { this["name"] = value; }
            }

            /// <summary>
            /// Gets or sets the prefix of the service.
            /// </summary>
            /// <value>The prefix of the service.</value>
            [ConfigurationProperty("prefix", DefaultValue = "", IsRequired = false)]
            public string Prefix
            {
                get { return (string)this["prefix"]; }

                set { this["prefix"] = value; }
            }

            /// <summary>
            /// Gets or sets the type of the service.
            /// </summary>
            /// <value>The full Type definition of the service</value>
            [ConfigurationProperty("type", DefaultValue = "", IsRequired = true)]
            public string Type
            {
                get { return (string)this["type"]; }

                set { this["type"] = value; }
            }

            /// <summary>
            /// Gets the <see cref="SettingElementCollection"/>.
            /// </summary>
            /// <value>
            /// The <see cref="SettingElementCollection"/>.
            /// </value>
            [ConfigurationProperty("settings", IsRequired = false)]
            public SettingElementCollection Settings
            {
                get
                {
                    return this["settings"] as SettingElementCollection;
                }
            }

            /// <summary>
            /// Gets the <see cref="T:ImageProcessor.Web.Config.ImageSecuritySection.WhiteListElementCollection"/>.
            /// </summary>
            /// <value>
            /// The <see cref="T:ImageProcessor.Web.Config.ImageSecuritySection.WhiteListElementCollection"/>.
            /// </value>
            [ConfigurationProperty("whitelist", IsRequired = false)]
            public WhiteListElementCollection WhiteList
            {
                get
                {
                    return this["whitelist"] as WhiteListElementCollection;
                }
            }
        }

        /// <summary>
        /// Represents a collection of <see cref="ServiceElement"/> elements within the configuration.
        /// </summary>
        public class ServiceElementCollection : ConfigurationElementCollection
        {
            /// <summary>
            /// Gets the type of the <see cref="ConfigurationElementCollection"/>.
            /// </summary>
            /// <value>
            /// The <see cref="ConfigurationElementCollectionType"/> of this collection.
            /// </value>
            public override ConfigurationElementCollectionType CollectionType
            {
                get { return ConfigurationElementCollectionType.BasicMap; }
            }

            /// <summary>
            /// Gets the name used to identify this collection of elements in the configuration file when overridden in a derived class.
            /// </summary>
            /// <value>
            /// The name of the collection; otherwise, an empty string. The default is an empty string.
            /// </value>
            protected override string ElementName
            {
                get { return "service"; }
            }

            /// <summary>
            /// Gets or sets the <see cref="T:ImageProcessor.Web.Config.ImageSecuritySection.ServiceElement"/>
            /// at the specified index within the collection.
            /// </summary>
            /// <param name="index">The index at which to get the specified object.</param>
            /// <returns>
            /// The <see cref="T:ImageProcessor.Web.Config.ImageSecuritySection.ServiceElement"/>
            /// at the specified index within the collection.
            /// </returns>
            public ServiceElement this[int index]
            {
                get
                {
                    return (ServiceElement)BaseGet(index);
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
            /// When overridden in a derived class, creates a new <see cref="ConfigurationElement"/>.
            /// </summary>
            /// <returns>
            /// A new <see cref="ConfigurationElement"/>.
            /// </returns>
            protected override ConfigurationElement CreateNewElement()
            {
                return new ServiceElement();
            }

            /// <summary>
            /// Gets the element key for a specified configuration element when overridden in a derived class.
            /// </summary>
            /// <returns>
            /// An <see cref="T:System.Object"/> that acts as the key for the specified <see cref="ConfigurationElement"/>.
            /// </returns>
            /// <param name="element">The <see cref="ConfigurationElement"/> to return the key for. </param>
            protected override object GetElementKey(ConfigurationElement element)
            {
                return ((ServiceElement)element).Name;
            }
        }

        /// <summary>
        /// Represents a CORSOriginsElement configuration element within the configuration.
        /// </summary>
        public class CORSOriginElement : ConfigurationElement
        {
            /// <summary>
            /// Gets the <see cref="T:ImageProcessor.Web.Config.ImageSecuritySection.WhiteListElementCollection"/>.
            /// </summary>
            /// <value>
            /// The <see cref="T:ImageProcessor.Web.Config.ImageSecuritySection.WhiteListElementCollection"/>.
            /// </value>
            [ConfigurationProperty("whitelist", IsRequired = false)]
            public WhiteListElementCollection WhiteList
            {
                get
                {
                    return this["whitelist"] as WhiteListElementCollection;
                }
            }
        }

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
            public SafeUrl this[int index]
            {
                get
                {
                    return this.BaseGet(index) as SafeUrl;
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
                return new SafeUrl();
            }

            /// <summary>
            /// Gets the element key for a specified whitelist configuration element.
            /// </summary>
            /// <param name="element">The <see cref="ConfigurationElement">ConfigurationElement</see> to return the key for.</param>
            /// <returns>The element key for a specified whitelist configuration element.</returns>
            protected override object GetElementKey(ConfigurationElement element)
            {
                return ((SafeUrl)element).Url;
            }
        }

        /// <summary>
        /// Represents a whitelist configuration element within the configuration.
        /// </summary>
        public class SafeUrl : ConfigurationElement
        {
            /// <summary>
            /// Gets or sets the url of the white listed file.
            /// </summary>
            /// <value>The url of the white listed file.</value>
            [ConfigurationProperty("url", DefaultValue = "", IsRequired = true)]
            public Uri Url
            {
                get { return new Uri(this["url"].ToString(), UriKind.RelativeOrAbsolute); }

                set { this["url"] = value; }
            }
        }
    }
}
