// --------------------------------------------------------------------------------------------------------------------
// <copyright file="NativeBinaryFactory.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Controls the loading and unloading of any native binaries required by ImageProcessor.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Configuration
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    /// <summary>
    /// Controls the loading and unloading of any native binaries required by ImageProcessor.
    /// </summary>
    public class NativeBinaryFactory : IDisposable
    {
        /// <summary>
        /// Whether the process is running in 64bit mode. Used for calling the correct dllimport method.
        /// </summary>
        private static readonly bool Is64Bit = Environment.Is64BitProcess;

        /// <summary>
        /// The native binaries.
        /// </summary>
        private static ConcurrentDictionary<string, IntPtr> nativeBinaries;

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
        /// Initializes a new instance of the <see cref="NativeBinaryFactory"/> class.
        /// </summary>
        public NativeBinaryFactory()
        {
            nativeBinaries = new ConcurrentDictionary<string, IntPtr>();
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="T:ImageProcessor.Configuration.NativeBinaryFactory">ImageFactory</see> class. 
        /// </summary>
        /// <remarks>
        /// Use C# destructor syntax for finalization code.
        /// This destructor will run only if the Dispose method 
        /// does not get called.
        /// It gives your base class the opportunity to finalize.
        /// Do not provide destructors in types derived from this class.
        /// </remarks>
        ~NativeBinaryFactory()
        {
            // Do not re-create Dispose clean-up code here.
            // Calling Dispose(false) is optimal in terms of
            // readability and maintainability.
            this.Dispose(false);
        }

        /// <summary>
        /// Gets a value indicating whether the operating environment is 64 bit.
        /// </summary>
        public bool Is64BitEnvironment
        {
            get
            {
                return Is64Bit;
            }
        }

        /// <summary>
        /// Registers any embedded native (unmanaged) binaries required by ImageProcessor.
        /// </summary>
        /// <param name="name">
        /// The name of the native binary.
        /// </param>
        /// <param name="resourceBytes">
        /// The resource bytes containing the native binary.
        /// </param>
        /// <exception cref="ApplicationException">
        /// Thrown if the binary cannot be registered.
        /// </exception>
        public void RegisterNativeBinary(string name, byte[] resourceBytes)
        {
            nativeBinaries.GetOrAdd(
                name,
                b =>
                {
                    IntPtr pointer;
                    string folder = Is64BitEnvironment ? "x64" : "x86";
                    Assembly assembly = Assembly.GetExecutingAssembly();
                    string targetBasePath = new Uri(assembly.Location).LocalPath;
                    string targetPath = Path.GetFullPath(Path.Combine(targetBasePath, "..\\" + folder + "\\" + name));

                    // Copy the file across if necessary.
                    FileInfo fileInfo = new FileInfo(targetPath);
                    bool rewrite = true;
                    if (fileInfo.Exists)
                    {
                        byte[] existing = File.ReadAllBytes(targetPath);

                        if (resourceBytes.SequenceEqual(existing))
                        {
                            rewrite = false;
                        }
                    }

                    if (rewrite)
                    {
                        // ReSharper disable once AssignNullToNotNullAttribute
                        DirectoryInfo directoryInfo = new DirectoryInfo(Path.GetDirectoryName(targetPath));
                        if (!directoryInfo.Exists)
                        {
                            directoryInfo.Create();
                        }

                        File.WriteAllBytes(targetPath, resourceBytes);
                    }

                    try
                    {
#if !__MonoCS__
                        // Load the binary into memory.
                        pointer = NativeMethods.LoadLibrary(targetPath);
#else
                        // Load the binary into memory. The second parameter forces it to load immediately.
                        pointer = NativeMethods.dlopen(targetPath, 2);
#endif
                    }
                    catch (Exception ex)
                    {
                        throw new ApplicationException(ex.Message);
                    }

                    if (pointer == IntPtr.Zero)
                    {
                        throw new ApplicationException("Cannot load " + name);
                    }

                    return pointer;
                });
        }

        /// <summary>
        /// Disposes the object and frees resources for the Garbage Collector.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);

            // This object will be cleaned up by the Dispose method.
            // Therefore, you should call GC.SuppressFinalize to
            // take this object off the finalization queue 
            // and prevent finalization code for this object
            // from executing a second time.
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the object and frees resources for the Garbage Collector.
        /// </summary>
        /// <param name="disposing">If true, the object gets disposed.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (this.isDisposed)
            {
                return;
            }

            if (disposing)
            {
                // Dispose of any managed resources here.
            }

            // Call the appropriate methods to clean up
            // unmanaged resources here.
            this.FreeNativeBinaries();

            // Note disposing is done.
            this.isDisposed = true;
        }

        /// <summary>
        /// Frees the reference to the native binaries.
        /// </summary>
        private void FreeNativeBinaries()
        {
            foreach (KeyValuePair<string, IntPtr> nativeBinary in nativeBinaries)
            {
                IntPtr pointer = nativeBinary.Value;

#if !__MonoCS__
                // According to http://stackoverflow.com/a/2445558/427899 you need to call this twice.
                NativeMethods.FreeLibrary(pointer);
                NativeMethods.FreeLibrary(pointer);
#else
                NativeMethods.dlclose(pointer);
#endif
            }
        }
    }
}
