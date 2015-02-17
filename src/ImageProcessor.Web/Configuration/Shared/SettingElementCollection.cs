// --------------------------------------------------------------------------------------------------------------------
// <copyright file="SettingElementCollection.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Represents a SettingElementCollection collection configuration element within the configuration.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Web.Configuration
{
    using System.Configuration;
    using System.Linq;

    /// <summary>
    /// Represents a SettingElementCollection collection configuration element within the configuration.
    /// </summary>
    public class SettingElementCollection : ConfigurationElementCollection
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
            get { return "setting"; }
        }

        /// <summary>
        /// Gets or sets the <see cref="T:ImageProcessor.Web.Config.ImageSecuritySection.SettingElement"/>
        /// at the specified index within the collection.
        /// </summary>
        /// <param name="index">The index at which to get the specified object.</param>
        /// <returns>
        /// The <see cref="T:ImageProcessor.Web.Config.ImageSecuritySection.SettingElement"/>
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
        /// The <see cref="ConfigurationElement">ConfigurationElement</see> 
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
