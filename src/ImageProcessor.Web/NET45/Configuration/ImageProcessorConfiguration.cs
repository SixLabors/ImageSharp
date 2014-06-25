// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ImageProcessorConfiguration.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encapsulates methods to allow the retrieval of ImageProcessor settings.
//   <see href="http://csharpindepth.com/Articles/General/Singleton.aspx" />
// </summary>
// --------------------------------------------------------------------------------------------------------------------
namespace ImageProcessor.Web.Configuration
{
    #region Using
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Web.Compilation;
    using ImageProcessor.Processors;
    using ImageProcessor.Web.Processors;

    #endregion

    /// <summary>
    /// Encapsulates methods to allow the retrieval of ImageProcessor settings.
    /// <see href="http://csharpindepth.com/Articles/General/Singleton.aspx"/>
    /// </summary>
    public sealed class ImageProcessorConfiguration
    {
        #region Fields
        /// <summary>
        /// A new instance Initializes a new instance of the <see cref="T:ImageProcessor.Web.Config.ImageProcessorConfig"/> class.
        /// with lazy initialization.
        /// </summary>
        private static readonly Lazy<ImageProcessorConfiguration> Lazy =
                        new Lazy<ImageProcessorConfiguration>(() => new ImageProcessorConfiguration());

        /// <summary>
        /// A collection of the <see cref="T:ImageProcessor.Web.Config.ImageProcessingSection.SettingElementCollection"/> elements 
        /// for available plugins.
        /// </summary>
        private static readonly ConcurrentDictionary<string, Dictionary<string, string>> PluginSettings =
            new ConcurrentDictionary<string, Dictionary<string, string>>();

        /// <summary>
        /// A collection of the processing presets defined in the configuration. 
        /// for available plugins.
        /// </summary>
        private static readonly ConcurrentDictionary<string, string> PresetSettings = new ConcurrentDictionary<string, string>();

        /// <summary>
        /// The processing configuration section from the current application configuration. 
        /// </summary>
        private static ImageProcessingSection imageProcessingSection;

        /// <summary>
        /// The cache configuration section from the current application configuration. 
        /// </summary>
        private static ImageCacheSection imageCacheSection;

        /// <summary>
        /// The security configuration section from the current application configuration. 
        /// </summary>
        private static ImageSecuritySection imageSecuritySection;
        #endregion

        #region Constructors

        /// <summary>
        /// Prevents a default instance of the <see cref="ImageProcessorConfiguration"/> class from being created.
        /// </summary>
        private ImageProcessorConfiguration()
        {
            this.LoadGraphicsProcessors();
        }
        #endregion

        #region Properties
        /// <summary>
        /// Gets the current instance of the <see cref="ImageProcessorConfiguration"/> class.
        /// </summary>
        public static ImageProcessorConfiguration Instance
        {
            get
            {
                return Lazy.Value;
            }
        }

        /// <summary>
        /// Gets the list of available GraphicsProcessors.
        /// </summary>
        public IList<IWebGraphicsProcessor> GraphicsProcessors { get; private set; }

        /// <summary>
        /// Gets a value indicating whether to preserve exif meta data.
        /// </summary>
        public bool PreserveExifMetaData
        {
            get
            {
                return GetImageProcessingSection().PreserveExifMetaData;
            }
        }

        #region Caching
        /// <summary>
        /// Gets the maximum number of days to store images in the cache.
        /// </summary>
        public int MaxCacheDays
        {
            get
            {
                return GetImageCacheSection().MaxDays;
            }
        }

        /// <summary>
        /// Gets or the virtual path of the cache folder.
        /// </summary>
        /// <value>The virtual path of the cache folder.</value>
        public string VirtualCachePath
        {
            get
            {
                return GetImageCacheSection().VirtualPath;
            }
        }
        #endregion

        #region Security
        /// <summary>
        /// Gets a list of white listed url[s] that images can be downloaded from.
        /// </summary>
        public Uri[] RemoteFileWhiteList
        {
            get
            {
                return GetImageSecuritySection().WhiteList.Cast<ImageSecuritySection.SafeUrl>().Select(x => x.Url).ToArray();
            }
        }

        /// <summary>
        /// Gets a list of image extensions for url[s] with no extension.
        /// </summary>
        public ImageSecuritySection.SafeUrl[] RemoteFileWhiteListExtensions
        {
            get
            {
                return GetImageSecuritySection().WhiteList.Cast<ImageSecuritySection.SafeUrl>().ToArray();
            }
        }

        /// <summary>
        /// Gets a value indicating whether the current application is allowed to download remote files.
        /// </summary>
        public bool AllowRemoteDownloads
        {
            get
            {
                return GetImageSecuritySection().AllowRemoteDownloads;
            }
        }

        /// <summary>
        /// Gets the maximum length to wait in milliseconds before throwing an error requesting a remote file.
        /// </summary>
        public int Timeout
        {
            get
            {
                return GetImageSecuritySection().Timeout;
            }
        }

        /// <summary>
        /// Gets the maximum allowable size in bytes of e remote file to process.
        /// </summary>
        public int MaxBytes
        {
            get
            {
                return GetImageSecuritySection().MaxBytes;
            }
        }

        /// <summary>
        /// Gets the remote prefix for external files for the application.
        /// </summary>
        public string RemotePrefix
        {
            get
            {
                return GetImageSecuritySection().RemotePrefix;
            }
        }
        #endregion
        #endregion

        #region Methods
        /// <summary>
        /// Returns the processing instructions matching the preset defined in the configuration.
        /// </summary>
        /// <param name="name">
        /// The name of the plugin to get the settings for.
        /// </param>
        /// <returns>
        /// The <see cref="T:Systems.String"/> the processing instructions.
        /// </returns>
        public string GetPresetSettings(string name)
        {
            return PresetSettings.GetOrAdd(
                   name,
                   n =>
                   {
                       ImageProcessingSection.PresetElement presetElement = GetImageProcessingSection()
                       .Presets
                       .Cast<ImageProcessingSection.PresetElement>()
                       .FirstOrDefault(x => x.Name == n);
                       return presetElement != null ? presetElement.Value : null;
                   });
        }

        /// <summary>
        /// Returns the <see cref="T:ImageProcessor.Web.Config.ImageProcessingSection.SettingElementCollection"/> for the given plugin.
        /// </summary>
        /// <param name="name">
        /// The name of the plugin to get the settings for.
        /// </param>
        /// <returns>
        /// The <see cref="T:ImageProcessor.Web.Config.ImageProcessingSection.SettingElementCollection"/> for the given plugin.
        /// </returns>
        public Dictionary<string, string> GetPluginSettings(string name)
        {
            return PluginSettings.GetOrAdd(
                name,
                n =>
                {
                    ImageProcessingSection.PluginElement pluginElement = GetImageProcessingSection()
                        .Plugins
                        .Cast<ImageProcessingSection.PluginElement>()
                        .FirstOrDefault(x => x.Name == n);

                    Dictionary<string, string> settings;

                    if (pluginElement != null)
                    {
                        settings = pluginElement.Settings
                            .Cast<ImageProcessingSection.SettingElement>()
                            .ToDictionary(setting => setting.Key, setting => setting.Value);
                    }
                    else
                    {
                        settings = new Dictionary<string, string>();
                    }

                    return settings;
                });
        }

        /// <summary>
        /// Retrieves the processing configuration section from the current application configuration. 
        /// </summary>
        /// <returns>The processing configuration section from the current application configuration. </returns>
        private static ImageProcessingSection GetImageProcessingSection()
        {
            return imageProcessingSection ?? (imageProcessingSection = ImageProcessingSection.GetConfiguration());
        }

        /// <summary>
        /// Retrieves the caching configuration section from the current application configuration. 
        /// </summary>
        /// <returns>The caching configuration section from the current application configuration. </returns>
        private static ImageCacheSection GetImageCacheSection()
        {
            return imageCacheSection ?? (imageCacheSection = ImageCacheSection.GetConfiguration());
        }

        /// <summary>
        /// Retrieves the security configuration section from the current application configuration. 
        /// </summary>
        /// <returns>The security configuration section from the current application configuration. </returns>
        private static ImageSecuritySection GetImageSecuritySection()
        {
            return imageSecuritySection ?? (imageSecuritySection = ImageSecuritySection.GetConfiguration());
        }

        /// <summary>
        /// Gets the list of available GraphicsProcessors.
        /// </summary>
        private void LoadGraphicsProcessors()
        {
            if (this.GraphicsProcessors == null)
            {
                if (GetImageProcessingSection().Plugins.AutoLoadPlugins)
                {
                    Type type = typeof(IWebGraphicsProcessor);
                    try
                    {
                        // Build a list of native IGraphicsProcessor instances.
                        List<Type> availableTypes = BuildManager.GetReferencedAssemblies()
                                                                .Cast<Assembly>()
                                                                .SelectMany(s => s.GetTypes())
                                                                .Where(t => t != null && type.IsAssignableFrom(t) && t.IsClass && !t.IsAbstract)
                                                                .ToList();

                        // Create them and add.
                        this.GraphicsProcessors = availableTypes.Select(x => (Activator.CreateInstance(x) as IWebGraphicsProcessor)).ToList();

                        // Add the available settings.
                        foreach (IWebGraphicsProcessor webProcessor in this.GraphicsProcessors)
                        {
                            webProcessor.Processor.Settings = this.GetPluginSettings(webProcessor.GetType().Name);
                        }
                    }
                    catch (ReflectionTypeLoadException)
                    {
                        this.LoadGraphicsProcessorsFromConfiguration();
                    }
                }
                else
                {
                    this.LoadGraphicsProcessorsFromConfiguration();
                }
            }
        }

        /// <summary>
        /// Loads graphics processors from configuration.
        /// </summary>
        /// <exception cref="TypeLoadException">
        /// Thrown when an <see cref="IGraphicsProcessor"/> cannot be loaded.
        /// </exception>
        private void LoadGraphicsProcessorsFromConfiguration()
        {
            ImageProcessingSection.PluginElementCollection pluginConfigs = imageProcessingSection.Plugins;
            this.GraphicsProcessors = new List<IWebGraphicsProcessor>();
            foreach (ImageProcessingSection.PluginElement pluginConfig in pluginConfigs)
            {
                Type type = Type.GetType(pluginConfig.Type);

                if (type == null)
                {
                    throw new TypeLoadException("Couldn't load IWebGraphicsProcessor: " + pluginConfig.Type);
                }

                this.GraphicsProcessors.Add(Activator.CreateInstance(type) as IWebGraphicsProcessor);
            }

            // Add the available settings.
            foreach (IWebGraphicsProcessor webProcessor in this.GraphicsProcessors)
            {
                webProcessor.Processor.Settings = this.GetPluginSettings(webProcessor.GetType().Name);
            }
        }
        #endregion
    }
}
