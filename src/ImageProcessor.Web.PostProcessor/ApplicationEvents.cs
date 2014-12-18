// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ImageProcessingModule.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   PostProcesses any image requests within the web application.
//   Many thanks to Azure Image Optimizer <see href="https://github.com/ligershark/AzureJobs"/>
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System.Web;

[assembly: PreApplicationStartMethod(typeof(ImageProcessor.Web.PostProcessor.ApplicationEvents), "Start")]
namespace ImageProcessor.Web.PostProcessor
{
    using ImageProcessor.Web.Helpers;
    using ImageProcessor.Web.HttpModules;

    /// <summary>
    /// PostProcesses any image requests within the web application.
    /// Many thanks to Azure Image Optimizer <see href="https://github.com/ligershark/AzureJobs"/>
    /// </summary>
    public static class ApplicationEvents
    {
        public static void Start()
        {
            ImageProcessingModule.OnPostProcessing += PostProcess;
        }

        private static async void PostProcess(object sender, PostProcessingEventArgs e)
        {
            await PostProcessor.PostProcessImage(e.CachedImagePath);
        }
    }
}
