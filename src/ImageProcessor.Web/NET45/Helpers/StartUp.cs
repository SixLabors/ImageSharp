// --------------------------------------------------------------------------------------------------------------------
// <copyright file="StartUp.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Provides methods to handle startup events.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

[assembly: System.Web.PreApplicationStartMethod(typeof(ImageProcessor.Web.Helpers.StartUp), "PreApplicationStart")]

namespace ImageProcessor.Web.Helpers
{
    using ImageProcessor.Web.HttpModules;
    using Microsoft.Web.Infrastructure.DynamicModuleHelper;

    /// <summary>
    /// Provides methods to handle startup events.
    /// </summary>
    public static class StartUp
    {
        /// <summary>
        /// The pre application start.
        /// </summary>
        public static void PreApplicationStart()
        {
            DynamicModuleUtility.RegisterModule(typeof(ImageProcessingModule));
        }
    }
}
