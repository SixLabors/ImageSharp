// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ImageProcessingSection.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Represents an image processing section within a configuration file.
//   Nested syntax adapted from <see href="http://tneustaedter.blogspot.co.uk/2011/09/how-to-create-one-or-more-nested.html" />
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Web.Configuration
{
    using System.Configuration;
    using System.IO;
    using System.Xml;

    using ImageProcessor.Web.Helpers;

    /// <summary>
    /// Represents an image processing section within a configuration file.
    /// Nested syntax adapted from <see href="http://tneustaedter.blogspot.co.uk/2011/09/how-to-create-one-or-more-nested.html"/>
    /// </summary>
    public sealed class ImageProcessingSection : ConfigurationSection
    {
        /// <summary>
        /// Gets or sets a value indicating whether to preserve exif meta data.
        /// </summary>
        [ConfigurationProperty("preserveExifMetaData", IsRequired = false, DefaultValue = false)]
        public bool PreserveExifMetaData
        {
            get { return (bool)this["preserveExifMetaData"]; }
            set { this["preserveExifMetaData"] = value; }
        }

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

        /// <summary>
        /// Gets or sets a value indicating whether to auto load plugins.
        /// </summary>
        public bool AutoLoadPlugins { get; set; }

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
                imageProcessingSection.AutoLoadPlugins = false;
                return imageProcessingSection;
            }

            string section = ResourceHelpers.ResourceAsString("ImageProcessor.Web.Configuration.Resources.processing.config.transform");
            XmlReader reader = new XmlTextReader(new StringReader(section));
            imageProcessingSection = new ImageProcessingSection();
            imageProcessingSection.DeserializeSection(reader);
            imageProcessingSection.AutoLoadPlugins = true;
            return imageProcessingSection;
        }

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
                get { return "preset"; }
            }

            /// <summary>
            /// Gets or sets the <see cref="T:ImageProcessor.Web.Config.ImageProcessingSection.PresetElement"/>
            /// at the specified index within the collection.
            /// </summary>
            /// <param name="index">
            /// The index at which to get the specified object.
            /// </param>
            /// <returns>
            /// The <see cref="T:ImageProcessor.Web.Config.ImageProcessingSection.PresetElement"/>
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
            /// The <see cref="ConfigurationElement">ConfigurationElement</see> 
            /// to return the key for.
            /// </param>
            /// <returns>
            /// The element key for a specified PluginElement configuration element.
            /// </returns>
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
                get { return "plugin"; }
            }

            /// <summary>
            /// Gets or sets the <see cref="T:ImageProcessor.Web.Config.ImageProcessingSection.PluginElement"/>
            /// at the specified index within the collection.
            /// </summary>
            /// <param name="index">
            /// The index at which to get the specified object.
            /// </param>
            /// <returns>
            /// The <see cref="T:ImageProcessor.Web.Config.ImageProcessingSection.PluginElement"/>
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
            /// The <see cref="ConfigurationElement">ConfigurationElement</see> 
            /// to return the key for.
            /// </param>
            /// <returns>
            /// The element key for a specified PluginElement configuration element.
            /// </returns>
            protected override object GetElementKey(ConfigurationElement element)
            {
                return ((PluginElement)element).Name;
            }
        }
    }
}
