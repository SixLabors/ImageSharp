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
    using System.Linq;
    using System.Xml;

    using ImageProcessor.Web.Helpers;

    /// <summary>
    /// Represents an image security section within a configuration file.
    /// </summary>
    public sealed class ImageSecuritySection : ConfigurationSection
    {
        #region Properties
        /// <summary>
        /// Gets the <see cref="T:ImageProcessor.Web.Config.ImageSecuritySection.ServiceElementCollection"/>
        /// </summary>
        /// <value>The <see cref="T:ImageProcessor.Web.Config.ImageSecuritySection.ServiceElementCollection"/></value>
        [ConfigurationProperty("services", IsRequired = true)]
        public ServiceElementCollection ImageServices
        {
            get
            {
                object o = this["services"];
                return o as ServiceElementCollection;
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

            string section = ResourceHelpers.ResourceAsString("ImageProcessor.Web.Configuration.Resources.security.config");
            XmlReader reader = new XmlTextReader(new StringReader(section));
            imageSecuritySection = new ImageSecuritySection();
            imageSecuritySection.DeserializeSection(reader);

            return imageSecuritySection;
        }
        #endregion

        /// <summary>
        /// Represents a ServiceElement configuration element within the configuration.
        /// </summary>
        public class ServiceElement : ConfigurationElement
        {
            /// <summary>
            /// Gets or sets the name of the plugin file.
            /// </summary>
            /// <value>The name of the plugin.</value>
            [ConfigurationProperty("name", DefaultValue = "", IsRequired = true)]
            public string Name
            {
                get { return (string)this["name"]; }

                set { this["name"] = value; }
            }

            /// <summary>
            /// Gets or sets the type of the service file.
            /// </summary>
            /// <value>The full Type definition of the plugin</value>
            [ConfigurationProperty("type", DefaultValue = "", IsRequired = true)]
            public string Type
            {
                get { return (string)this["type"]; }

                set { this["type"] = value; }
            }

            /// <summary>
            /// Gets the <see cref="T:ImageProcessor.Web.Config.ImageSecuritySection.SettingElementCollection"/>.
            /// </summary>
            /// <value>
            /// The <see cref="T:ImageProcessor.Web.Config.ImageSecuritySection.SettingElementCollection"/>.
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
            /// Gets or sets a value indicating whether to auto load all plugins.
            /// <remarks>Defaults to <value>True</value>.</remarks>
            /// </summary>
            /// <value>If True plugins are auto discovered and loaded from all assemblies otherwise they must be defined in the configuration file</value>
            [ConfigurationProperty("autoLoadPlugins", DefaultValue = true, IsRequired = false)]
            public bool AutoLoadPlugins
            {
                get { return (bool)this["autoLoadPlugins"]; }

                set { this["autoLoadPlugins"] = value; }
            }

            /// <summary>
            /// Gets the type of the <see cref="T:System.Configuration.ConfigurationElementCollection"/>.
            /// </summary>
            /// <value>
            /// The <see cref="T:System.Configuration.ConfigurationElementCollectionType"/> of this collection.
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
            /// When overridden in a derived class, creates a new <see cref="T:System.Configuration.ConfigurationElement"/>.
            /// </summary>
            /// <returns>
            /// A new <see cref="T:System.Configuration.ConfigurationElement"/>.
            /// </returns>
            protected override ConfigurationElement CreateNewElement()
            {
                return new ServiceElement();
            }

            /// <summary>
            /// Gets the element key for a specified configuration element when overridden in a derived class.
            /// </summary>
            /// <returns>
            /// An <see cref="T:System.Object"/> that acts as the key for the specified <see cref="T:System.Configuration.ConfigurationElement"/>.
            /// </returns>
            /// <param name="element">The <see cref="T:System.Configuration.ConfigurationElement"/> to return the key for. </param>
            protected override object GetElementKey(ConfigurationElement element)
            {
                return ((ServiceElement)element).Name;
            }
        }

        /// <summary>
        /// Represents a SettingElement configuration element within the configuration.
        /// </summary>
        public class SettingElement : ConfigurationElement
        {
            /// <summary>
            /// Gets or sets the key of the plugin setting.
            /// </summary>
            /// <value>The key of the plugin setting.</value>
            [ConfigurationProperty("key", IsRequired = true, IsKey = true)]
            public string Key
            {
                get
                {
                    return this["key"] as string;
                }

                set
                {
                    this["key"] = value;
                }
            }

            /// <summary>
            /// Gets or sets the value of the plugin setting.
            /// </summary>
            /// <value>The value of the plugin setting.</value>
            [ConfigurationProperty("value", IsRequired = true)]
            public string Value
            {
                get
                {
                    return (string)this["value"];
                }

                set
                {
                    this["value"] = value;
                }
            }
        }

        /// <summary>
        /// Represents a SettingElementCollection collection configuration element within the configuration.
        /// </summary>
        public class SettingElementCollection : ConfigurationElementCollection
        {
            /// <summary>
            /// Gets the type of the <see cref="T:System.Configuration.ConfigurationElementCollection"/>.
            /// </summary>
            /// <value>
            /// The <see cref="T:System.Configuration.ConfigurationElementCollectionType"/> of this collection.
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
                get { return "setting"; }
            }

            /// <summary>
            /// Gets or sets the <see cref="T:ImageProcessor.Web.Config.ImageSecuritySection.SettingElement"/>
            /// at the specified index within the collection.
            /// </summary>
            /// <param name="index">The index at which to get the specified object.</param>
            /// <returns>
            /// The the <see cref="T:ImageProcessor.Web.Config.ImageSecuritySection.SettingElement"/>
            /// at the specified index within the collection.
            /// </returns>
            public ImageSecuritySection.SettingElement this[int index]
            {
                get
                {
                    return (ImageSecuritySection.SettingElement)BaseGet(index);
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
            /// Returns the setting element with the specified key.
            /// </summary>
            /// <param name="key">the key representing the element</param>
            /// <returns>the setting element</returns>
            public new ImageSecuritySection.SettingElement this[string key]
            {
                get { return (ImageSecuritySection.SettingElement)BaseGet(key); }
            }

            /// <summary>
            /// Returns a value indicating whether the settings collection contains the
            /// given object.
            /// </summary>
            /// <param name="key">The key to identify the setting.</param>
            /// <returns>True if the collection contains the key; otherwise false.</returns>
            public bool ContainsKey(string key)
            {
                object[] keys = BaseGetAllKeys();

                return keys.Any(obj => (string)obj == key);
            }

            /// <summary>
            /// Gets the element key for a specified PluginElement configuration element.
            /// </summary>
            /// <param name="element">
            /// The <see cref="T:System.Configuration.ConfigurationElement">ConfigurationElement</see> 
            /// to return the key for.
            /// </param>
            /// <returns>The element key for a specified PluginElement configuration element.</returns>
            protected override object GetElementKey(ConfigurationElement element)
            {
                return ((ImageSecuritySection.SettingElement)element).Key;
            }

            /// <summary>
            /// Creates a new SettingElement configuration element.
            /// </summary>
            /// <returns>
            /// A new SettingElement configuration element.
            /// </returns>
            protected override ConfigurationElement CreateNewElement()
            {
                return new ImageSecuritySection.SettingElement();
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
            /// <param name="element">The <see cref="T:System.Configuration.ConfigurationElement">ConfigurationElement</see> to return the key for.</param>
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
                get { return (Uri)this["url"]; }

                set { this["url"] = value; }
            }
        }
    }
}
