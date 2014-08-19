// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ImageProcessorBootstrapper.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   The ImageProcessor bootstrapper.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Configuration
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    using ImageProcessor.Common.Exceptions;
    using ImageProcessor.Common.Extensions;
    using ImageProcessor.Imaging.Formats;

    /// <summary>
    /// The ImageProcessor bootstrapper.
    /// </summary>
    public class ImageProcessorBootstrapper
    {
        /// <summary>
        /// A new instance Initializes a new instance of the <see cref="ImageProcessorBootstrapper"/> class.
        /// with lazy initialization.
        /// </summary>
        private static readonly Lazy<ImageProcessorBootstrapper> Lazy =
                        new Lazy<ImageProcessorBootstrapper>(() => new ImageProcessorBootstrapper());

        /// <summary>
        /// Prevents a default instance of the <see cref="ImageProcessorBootstrapper"/> class from being created.
        /// </summary>
        private ImageProcessorBootstrapper()
        {
            this.NativeBinaryFactory = new NativeBinaryFactory();
            this.LoadSupportedImageFormats();
        }

        /// <summary>
        /// Gets the current instance of the <see cref="ImageProcessorBootstrapper"/> class.
        /// </summary>
        public static ImageProcessorBootstrapper Instance
        {
            get
            {
                return Lazy.Value;
            }
        }

        /// <summary>
        /// Gets the supported image formats.
        /// </summary>
        public IEnumerable<ISupportedImageFormat> SupportedImageFormats { get; private set; }

        /// <summary>
        /// Gets the native binary factory for registering embedded (unmanaged) binaries.
        /// </summary>
        public NativeBinaryFactory NativeBinaryFactory { get; private set; }

        /// <summary>
        /// Creates a list, using reflection, of supported image formats that ImageProcessor can run.
        /// </summary>
        private void LoadSupportedImageFormats()
        {
            if (this.SupportedImageFormats == null)
            {
                Type type = typeof(ISupportedImageFormat);

                // Get any referenced but not used assemblies.
                Assembly executingAssembly = Assembly.GetExecutingAssembly();
                string targetBasePath = Path.GetDirectoryName(new Uri(executingAssembly.Location).LocalPath);

                // ReSharper disable once AssignNullToNotNullAttribute
                FileInfo[] files = new DirectoryInfo(targetBasePath).GetFiles("*.dll", SearchOption.AllDirectories);

                HashSet<string> found = new HashSet<string>();
                foreach (FileInfo fileInfo in files)
                {
                    try
                    {
                        AssemblyName assemblyName = AssemblyName.GetAssemblyName(fileInfo.FullName);

                        if (!AppDomain.CurrentDomain.GetAssemblies()
                            .Any(a => AssemblyName.ReferenceMatchesDefinition(assemblyName, a.GetName())))
                        {
                            // In a web app, this assembly will automatically be bound from the 
                            // Asp.Net Temporary folder from where the site actually runs.
                            Assembly.Load(assemblyName);
                            this.LoadReferencedAssemblies(found, Assembly.Load(assemblyName));
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log the exception for debugging only. There could be any old junk 
                        // thrown in to the bin folder by someone else.
                        Debug.WriteLine(ex.Message);
                    }
                }

                List<Type> availableTypes = AppDomain.CurrentDomain
                .GetAssemblies()
                .SelectMany(a => a.GetLoadableTypes())
                .Where(t => type.IsAssignableFrom(t) && t.IsClass && !t.IsAbstract)
                .ToList();

                this.SupportedImageFormats = availableTypes
                    .Select(f => (Activator.CreateInstance(f) as ISupportedImageFormat)).ToList();
            }
        }

        /// <summary>
        /// Loads any referenced assemblies into the current application domain.
        /// </summary>
        /// <param name="found">
        /// The collection containing the name of already found assemblies.
        /// </param>
        /// <param name="assembly">
        /// The assembly to load from.
        /// </param>
        private void LoadReferencedAssemblies(HashSet<string> found, Assembly assembly)
        {
            // Used to avoid duplicates 
            ArrayList results = new ArrayList();

            // Resulting info 
            Stack stack = new Stack();

            // Stack of names
            // Store root assembly (level 0) directly into results list 
            stack.Push(assembly.ToString());

            // Do a pre-order, non-recursive traversal 
            while (stack.Count > 0)
            {
                string info = (string)stack.Pop();

                // Get next assembly 
                if (!found.Contains(info))
                {
                    found.Add(info);
                    results.Add(info);

                    // Store it to results ArrayList
                    Assembly child = Assembly.Load(info);
                    AssemblyName[] subchild = child.GetReferencedAssemblies();

                    for (int i = subchild.Length - 1; i >= 0; --i)
                    {
                        stack.Push(subchild[i].ToString());
                    }
                }
            }
        }
    }
}
