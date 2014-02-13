// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ImageProcessingSection.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Represents an image processing section within a configuration file.
//   Nested syntax adapted from <see cref="http://tneustaedter.blogspot.co.uk/2011/09/how-to-create-one-or-more-nested.html" />
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Web.Config
{
    #region Using
    using System.Configuration;
    using System.Linq;
    #endregion

    /// <summary>
    /// Represents an image processing section within a configuration file.
    /// Nested syntax adapted from <see cref="http://tneustaedter.blogspot.co.uk/2011/09/how-to-create-one-or-more-nested.html"/>
    /// </summary>
    public sealed class ImageProcessingSection : ConfigurationSection
    {
        #region Properties
        /// <summary>
        /// Gets the <see cref="T:ImageProcessor.Web.Config.ImageProcessingSection.PresetElementCollection"/>.
        /// </summary>
        /// <value>
        /// The <see cref="T:ImageProcessor.Web.Config.ImageProcessingSection.PresetElementCollection"/>.
        /// </value>
        [ConfigurationProperty("presets", IsRequired = true)]
        public PresetElementCollection Presets
        {
            get
            {
                return this["presets"] as PresetElementCollection;
            }
        }

        /// <summary>
        /// Gets the <see cref="T:ImageProcessor.Web.Config.ImageProcessingSection.PluginElementCollection"/>.
        /// </summary>
        /// <value>
        /// The <see cref="T:ImageProcessor.Web.Config.ImageProcessingSection.PluginElementCollection"/>.
        /// </value>
        [ConfigurationProperty("plugins", IsRequired = true)]
        public PluginElementCollection Plugins
        {
            get
            {
                return this["plugins"] as PluginElementCollection;
            }
        }
        #endregion

        #region Methods
        /// <summary>
        /// Retrieves the processing configuration section from the current application configuration. 
        /// </summary>
        /// <returns>The processing configuration section from the current application configuration. </returns>
        public static ImageProcessingSection GetConfiguration()
        {
            ImageProcessingSection imageProcessingSection =
                ConfigurationManager.GetSection("imageProcessor/processing") as ImageProcessingSection;

            if (imageProcessingSection != null)
            {
                return imageProcessingSection;
            }

            return new ImageProcessingSection();
        }
        #endregion

        /// <summary>
        /// Represents a PresetElement configuration element within the configuration.
        /// </summary>
        public class PresetElement : ConfigurationElement
        {
            /// <summary>
            /// Gets or sets the name of the preset.
            /// </summary>
            /// <value>The name of the plugin.</value>
            [ConfigurationProperty("name", DefaultValue = "", IsRequired = true)]
            public string Name
            {
                get { return (string)this["name"]; }

                set { this["name"] = value; }
            }

            /// <summary>
            /// Gets or sets the value of the preset.
            /// </summary>
            /// <value>The full Type definition of the plugin</value>
            [ConfigurationProperty("value", DefaultValue = "", IsRequired = true)]
            public string Value
            {
                get { return (string)this["value"]; }

                set { this["value"] = value; }
            }
        }

        /// <summary>
        /// Represents a PresetElementCollection collection configuration element within the configuration.
        /// </summary>
        public class PresetElementCollection : ConfigurationElementCollection
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
                get { return "preset"; }
            }

            /// <summary>
            /// Gets or sets the <see cref="T:ImageProcessor.Web.Config.ImageProcessingSection.PresetElement"/>
            /// at the specified index within the collection.
            /// </summary>
            /// <param name="index">The index at which to get the specified object.</param>
            /// <returns>
            /// The the <see cref="T:ImageProcessor.Web.Config.ImageProcessingSection.PresetElement"/>
            /// at the specified index within the collection.
            /// </returns>
            public PresetElement this[int index]
            {
                get
                {
                    return (PresetElement)BaseGet(index);
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
            /// Creates a new Preset configuration element.
            /// </summary>
            /// <returns>
            /// A new PluginConfig configuration element.
            /// </returns>
            protected override ConfigurationElement CreateNewElement()
            {
                return new PresetElement();
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
                return ((PresetElement)element).Name;
            }
        }

        /// <summary>
        /// Represents a PluginElement configuration element within the configuration.
        /// </summary>
        public class PluginElement : ConfigurationElement
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
            /// Gets or sets the type of the plugin file.
            /// </summary>
            /// <value>The full Type definition of the plugin</value>
            [ConfigurationProperty("type", DefaultValue = "", IsRequired = true)]
            public string Type
            {
                get { return (string)this["type"]; }

                set { this["type"] = value; }
            }

            /// <summary>
            /// Gets the <see cref="T:ImageProcessor.Web.Config.ImageProcessingSection.SettingElementCollection"/>.
            /// </summary>
            /// <value>
            /// The <see cref="T:ImageProcessor.Web.Config.ImageProcessingSection.SettingElementCollection"/>.
            /// </value>
            [ConfigurationProperty("settings", IsRequired = false)]
            public SettingElementCollection Settings
            {
                get
                {
                    return this["settings"] as SettingElementCollection;
                }
            }
        }

        /// <summary>
        /// Represents a PluginElementCollection collection configuration element within the configuration.
        /// </summary>
        public class PluginElementCollection : ConfigurationElementCollection
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
                get { return "plugin"; }
            }

            /// <summary>
            /// Gets or sets the <see cref="T:ImageProcessor.Web.Config.ImageProcessingSection.PluginElement"/>
            /// at the specified index within the collection.
            /// </summary>
            /// <param name="index">The index at which to get the specified object.</param>
            /// <returns>
            /// The the <see cref="T:ImageProcessor.Web.Config.ImageProcessingSection.PluginElement"/>
            /// at the specified index within the collection.
            /// </returns>
            public PluginElement this[int index]
            {
                get
                {
                    return (PluginElement)BaseGet(index);
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
            /// Creates a new Plugin configuration element.
            /// </summary>
            /// <returns>
            /// A new Plugin configuration element.
            /// </returns>
            protected override ConfigurationElement CreateNewElement()
            {
                return new PluginElement();
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
                return ((PluginElement)element).Name;
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
            /// Gets or sets the <see cref="T:ImageProcessor.Web.Config.ImageProcessingSection.SettingElement"/>
            /// at the specified index within the collection.
            /// </summary>
            /// <param name="index">The index at which to get the specified object.</param>
            /// <returns>
            /// The the <see cref="T:ImageProcessor.Web.Config.ImageProcessingSection.SettingElement"/>
            /// at the specified index within the collection.
            /// </returns>
            public SettingElement this[int index]
            {
                get
                {
                    return (SettingElement)BaseGet(index);
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
            public new SettingElement this[string key]
            {
                get { return (SettingElement)BaseGet(key); }
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
                return ((SettingElement)element).Key;
            }

            /// <summary>
            /// Creates a new SettingElement configuration element.
            /// </summary>
            /// <returns>
            /// A new SettingElement configuration element.
            /// </returns>
            protected override ConfigurationElement CreateNewElement()
            {
                return new SettingElement();
            }
        }
    }
}
