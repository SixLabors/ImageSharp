// --------------------------------------------------------------------------------------------------------------------
// <copyright file="SettingElement.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Represents a SettingElement configuration element within the configuration.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Web.Configuration
{
    using System.Configuration;

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
}
