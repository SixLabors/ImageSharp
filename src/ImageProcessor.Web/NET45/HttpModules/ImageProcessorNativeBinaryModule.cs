// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ImageProcessorNativeBinaryModule.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   The image processing native binary module.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Web.HttpModules
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Web;

    using ImageProcessor.Web.Helpers;

    /// <summary>
    /// Controls the loading and unloading of any native binaries required by ImageProcessor.Web.
    /// </summary>
    public sealed class ImageProcessorNativeBinaryModule : IHttpModule
    {
        /// <summary>
        /// Whether the process is running in 64bit mode. Used for calling the correct dllimport method.
        /// </summary>
        private static readonly bool Is64Bit = Environment.Is64BitProcess;

        /// <summary>
        /// The object to lock against.
        /// </summary>
        private static readonly object SyncRoot = new object();

        /// <summary>
        /// The native binaries.
        /// </summary>
        private static readonly List<IntPtr> NativeBinaries = new List<IntPtr>();

        /// <summary>
        /// A value indicating whether this instance of the given entity has been disposed.
        /// </summary>
        /// <value><see langword="true"/> if this instance has been disposed; otherwise, <see langword="false"/>.</value>
        /// <remarks>
        /// If the entity is disposed, it must not be disposed a second
        /// time. The isDisposed field is set the first time the entity
        /// is disposed. If the isDisposed field is true, then the Dispose()
        /// method will not dispose again. This help not to prolong the entity's
        /// life in the Garbage Collector.
        /// </remarks>
        private bool isDisposed;

        /// <summary>
        /// Disposes of the resources (other than memory) used by the module that implements 
        /// <see cref="T:System.Web.IHttpModule" />.
        /// </summary>
        public void Dispose()
        {
            if (this.isDisposed)
            {
                return;
            }

            // Call the appropriate methods to clean up
            // unmanaged resources here.
            lock (SyncRoot)
            {
                this.FreeNativeBinaries();
            }

            // Note disposing is done.
            this.isDisposed = true;
        }

        /// <summary>
        /// Initializes a module and prepares it to handle requests.
        /// </summary>
        /// <param name="context">An <see cref="T:System.Web.HttpApplication" /> that provides access to 
        /// the methods, properties, and events common to all application objects within an ASP.NET application</param>
        public void Init(HttpApplication context)
        {
        }

        /// <summary>
        /// Loads any native ImageProcessor binaries.
        /// </summary>
        public void LoadNativeBinaries()
        {
            lock (SyncRoot)
            {
                this.RegisterNativeBinaries();
            }
        }

        /// <summary>
        /// Registers any native binaries.
        /// </summary>
        /// <exception cref="ApplicationException">
        /// Thrown when a native binary cannot be loaded.
        /// </exception>
        private void RegisterNativeBinaries()
        {
            if (NativeBinaries.Any())
            {
                return;
            }

            string folder = Is64Bit ? "x64" : "x86";
            string sourcePath = HttpContext.Current.Server.MapPath("~/bin/" + folder);
            Assembly assembly = Assembly.GetExecutingAssembly();
            string targetBasePath = new Uri(assembly.Location).LocalPath;

            DirectoryInfo directoryInfo = new DirectoryInfo(sourcePath);
            if (directoryInfo.Exists)
            {
                foreach (FileInfo fileInfo in directoryInfo.EnumerateFiles("*.dll"))
                {
                    if (fileInfo.Name.ToUpperInvariant().StartsWith("IMAGEPROCESSOR"))
                    {
                        IntPtr pointer;
                        string targetPath = Path.GetFullPath(Path.Combine(targetBasePath, "..\\" + folder + "\\" + fileInfo.Name));

                        File.Copy(sourcePath, targetPath, true);

                        try
                        {
                            // Load the binary into memory.
                            pointer = NativeMethods.LoadLibrary(targetPath);
                        }
                        catch (Exception ex)
                        {
                            throw new ApplicationException(ex.Message);
                        }

                        if (pointer == IntPtr.Zero)
                        {
                            throw new ApplicationException("Cannot load " + fileInfo.Name);
                        }

                        NativeBinaries.Add(pointer);
                    }
                }
            }
        }

        /// <summary>
        /// Frees the reference to the native binaries.
        /// </summary>
        private void FreeNativeBinaries()
        {
            foreach (IntPtr nativeBinary in NativeBinaries)
            {
                // According to http://stackoverflow.com/a/2445558/427899 you need to call this twice.
                NativeMethods.FreeLibrary(nativeBinary);
                NativeMethods.FreeLibrary(nativeBinary);
            }
        }
    }
}
