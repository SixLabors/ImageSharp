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
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Web.Compilation;

    using ImageProcessor.Common.Extensions;
    using ImageProcessor.Processors;
    using ImageProcessor.Web.Caching;
    using ImageProcessor.Web.Processors;
    using ImageProcessor.Web.Services;

    /// <summary>
    /// Encapsulates methods to allow the retrieval of ImageProcessor settings.
    /// <see href="http://csharpindepth.com/Articles/General/Singleton.aspx"/>
    /// </summary>
    public sealed class ImageProcessorConfiguration
    {
        #region Fields
        /// <summary>
        /// A new instance of the <see cref="T:ImageProcessor.Web.Config.ImageProcessorConfig"/> class.
        /// with lazy initialization.
        /// </summary>
        private static readonly Lazy<ImageProcessorConfiguration> Lazy =
                        new Lazy<ImageProcessorConfiguration>(() => new ImageProcessorConfiguration());

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
            this.LoadImageServices();
            this.LoadImageCache();
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
        /// Gets the list of available ImageServices.
        /// </summary>
        public IList<IImageService> ImageServices { get; private set; }

        /// <summary>
        /// Gets the current image cache.
        /// </summary>
        public Type ImageCache { get; private set; }

        /// <summary>
        /// Gets the image cache max days.
        /// </summary>
        public int ImageCacheMaxDays { get; private set; }

        /// <summary>
        /// Gets the image cache settings.
        /// </summary>
        public Dictionary<string, string> ImageCacheSettings { get; private set; }

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
        /// Retrieves the security configuration section from the current application configuration. 
        /// </summary>
        /// <returns>The security configuration section from the current application configuration. </returns>
        internal ImageSecuritySection GetImageSecuritySection()
        {
            return imageSecuritySection ?? (imageSecuritySection = ImageSecuritySection.GetConfiguration());
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

        #region GraphicesProcessors
        /// <summary>
        /// Gets the list of available GraphicsProcessors.
        /// </summary>
        private void LoadGraphicsProcessors()
        {
            if (this.GraphicsProcessors == null)
            {
                if (GetImageProcessingSection().AutoLoadPlugins)
                {
                    Type type = typeof(IWebGraphicsProcessor);
                    try
                    {
                        // Build a list of native IGraphicsProcessor instances.
                        List<Type> availableTypes = BuildManager.GetReferencedAssemblies()
                                                                .Cast<Assembly>()
                                                                .SelectMany(s => s.GetLoadableTypes())
                                                                .Where(t => type.IsAssignableFrom(t) && t.IsClass && !t.IsAbstract)
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
        /// Thrown when an <see cref="IWebGraphicsProcessor"/> cannot be loaded.
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

        /// <summary>
        /// Returns the <see cref="T:ImageProcessor.Web.Config.ImageProcessingSection.SettingElementCollection"/> for the given plugin.
        /// </summary>
        /// <param name="name">
        /// The name of the plugin to get the settings for.
        /// </param>
        /// <returns>
        /// The <see cref="T:ImageProcessor.Web.Config.ImageProcessingSection.SettingElementCollection"/> for the given plugin.
        /// </returns>
        private Dictionary<string, string> GetPluginSettings(string name)
        {
            ImageProcessingSection.PluginElement pluginElement = GetImageProcessingSection()
                .Plugins
                .Cast<ImageProcessingSection.PluginElement>()
                .FirstOrDefault(x => x.Name == name);

            Dictionary<string, string> settings;

            if (pluginElement != null)
            {
                settings = pluginElement.Settings
                    .Cast<SettingElement>()
                    .ToDictionary(setting => setting.Key, setting => setting.Value);
            }
            else
            {
                settings = new Dictionary<string, string>();
            }

            return settings;
        }
        #endregion

        #region ImageServices
        /// <summary>
        /// Gets the list of available ImageServices.
        /// </summary>
        private void LoadImageServices()
        {
            if (this.ImageServices == null)
            {
                if (this.GetImageSecuritySection().AutoLoadServices)
                {
                    Type type = typeof(IImageService);
                    try
                    {
                        // Build a list of native IGraphicsProcessor instances.
                        List<Type> availableTypes = BuildManager.GetReferencedAssemblies()
                                                                .Cast<Assembly>()
                                                                .SelectMany(s => s.GetLoadableTypes())
                                                                .Where(t => type.IsAssignableFrom(t) && t.IsClass && !t.IsAbstract)
                                                                .ToList();

                        // Create them and add.
                        this.ImageServices = availableTypes.Select(x => (Activator.CreateInstance(x) as IImageService)).ToList();

                        // Add the available settings.
                        foreach (IImageService service in this.ImageServices)
                        {
                            string name = service.GetType().Name;
                            service.Settings = this.GetServiceSettings(name);
                            service.WhiteList = this.GetServiceWhitelist(name);
                        }
                    }
                    catch (ReflectionTypeLoadException)
                    {
                        this.LoadImageServicesFromConfiguration();
                    }
                }
                else
                {
                    this.LoadImageServicesFromConfiguration();
                }
            }
        }

        /// <summary>
        /// Loads image services from configuration.
        /// </summary>
        /// <exception cref="TypeLoadException">
        /// Thrown when an <see cref="IGraphicsProcessor"/> cannot be loaded.
        /// </exception>
        private void LoadImageServicesFromConfiguration()
        {
            ImageSecuritySection.ServiceElementCollection services = imageSecuritySection.ImageServices;
            this.ImageServices = new List<IImageService>();
            foreach (ImageSecuritySection.ServiceElement config in services)
            {
                Type type = Type.GetType(config.Type);

                if (type == null)
                {
                    throw new TypeLoadException("Couldn't load IImageService: " + config.Type);
                }

                IImageService imageService = Activator.CreateInstance(type) as IImageService;
                if (!string.IsNullOrWhiteSpace(config.Prefix))
                {
                    if (imageService != null)
                    {
                        imageService.Prefix = config.Prefix;
                    }
                }

                this.ImageServices.Add(imageService);
            }

            // Add the available settings.
            foreach (IImageService service in this.ImageServices)
            {
                string name = service.GetType().Name;
                service.Settings = this.GetServiceSettings(name);
                service.WhiteList = this.GetServiceWhitelist(name);
            }
        }

        /// <summary>
        /// Returns the <see cref="SettingElementCollection"/> for the given plugin.
        /// </summary>
        /// <param name="name">
        /// The name of the plugin to get the settings for.
        /// </param>
        /// <returns>
        /// The <see cref="SettingElementCollection"/> for the given plugin.
        /// </returns>
        private Dictionary<string, string> GetServiceSettings(string name)
        {
            ImageSecuritySection.ServiceElement serviceElement = this.GetImageSecuritySection()
                .ImageServices
                .Cast<ImageSecuritySection.ServiceElement>()
                .FirstOrDefault(x => x.Name == name);

            Dictionary<string, string> settings;

            if (serviceElement != null)
            {
                settings = serviceElement.Settings
                    .Cast<SettingElement>()
                    .ToDictionary(setting => setting.Key, setting => setting.Value);
            }
            else
            {
                settings = new Dictionary<string, string>();
            }

            return settings;
        }

        /// <summary>
        /// Gets the whitelist of <see cref="System.Uri"/> for the given service.
        /// </summary>
        /// <param name="name">
        /// The name of the service to return the whitelist for.
        /// </param>
        /// <returns>
        /// The <see cref="System.Uri"/> array containing the whitelist.
        /// </returns>
        private Uri[] GetServiceWhitelist(string name)
        {
            ImageSecuritySection.ServiceElement serviceElement = this.GetImageSecuritySection()
               .ImageServices
               .Cast<ImageSecuritySection.ServiceElement>()
               .FirstOrDefault(x => x.Name == name);

            Uri[] whitelist = { };
            if (serviceElement != null)
            {
                whitelist = serviceElement.WhiteList.Cast<ImageSecuritySection.SafeUrl>()
                                          .Select(s => s.Url).ToArray();
            }

            return whitelist;
        }
        #endregion

        #region ImageCaches
        /// <summary>
        /// Gets the currently assigned <see cref="IImageCache"/>.
        /// </summary>
        private void LoadImageCache()
        {
            if (this.ImageCache == null)
            {
                string curentCache = GetImageCacheSection().CurrentCache;
                ImageCacheSection.CacheElementCollection caches = imageCacheSection.ImageCaches;

                foreach (ImageCacheSection.CacheElement cache in caches)
                {
                    if (cache.Name == curentCache)
                    {
                        Type type = Type.GetType(cache.Type);

                        if (type == null)
                        {
                            throw new TypeLoadException("Couldn't load IImageCache: " + cache.Type);
                        }

                        this.ImageCache = type;
                        this.ImageCacheMaxDays = cache.MaxDays;
                        this.ImageCacheSettings = cache.Settings
                                                       .Cast<SettingElement>()
                                                       .ToDictionary(setting => setting.Key, setting => setting.Value);
                        break;
                    }
                }
            }
        }
        #endregion
        #endregion
    }
}