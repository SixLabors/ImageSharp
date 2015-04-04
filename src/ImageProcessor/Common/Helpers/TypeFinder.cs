// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TypeFinder.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   A utility class to find all classes of a certain type by reflection in the current bin folder
//   of the web application.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Common.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Security;
    using System.Threading;

    using ImageProcessor.Common.Extensions;

    /// <summary>
    /// A utility class to find all classes of a certain type by reflection in the current bin folder
    /// of the web application. 
    /// </summary>
    /// <remarks>
    /// Adapted from identically named class within <see href="https://github.com/umbraco/Umbraco-CMS"/>
    /// </remarks>
    internal static class TypeFinder
    {
        /// <summary>
        /// The local filtered assembly cache.
        /// </summary>
        private static readonly HashSet<Assembly> LocalFilteredAssemblyCache = new HashSet<Assembly>();

        /// <summary>
        /// The local filtered assembly cache locker.
        /// </summary>
        private static readonly ReaderWriterLockSlim LocalFilteredAssemblyCacheLocker = new ReaderWriterLockSlim();

        /// <summary>
        /// The reader-writer lock implementation.
        /// </summary>
        private static readonly ReaderWriterLockSlim Locker = new ReaderWriterLockSlim();

        /// <summary>
        /// An assembly filter collection to filter out known types that definitely don't contain types 
        /// we'd like to find or plugins.
        /// Umbraco uses ImageProcessor in it's core so add common exclusion files from that.
        /// </summary>
        /// <remarks>
        /// NOTE the comma versus period... comma delimits the name in an Assembly FullName property so 
        /// if it ends with comma then its an exact name match.
        /// </remarks>
        private static readonly string[] KnownAssemblyExclusionFilter =
            {
                "mscorlib,", "System.", "Antlr3.", "Autofac.",
                "Autofac,", "Castle.", "ClientDependency.",
                "DataAnnotationsExtensions.", "Dynamic,",
                "HtmlDiff,", "Iesi.Collections,", "log4net,",
                "Microsoft.", "Newtonsoft.", "NHibernate.",
                "NHibernate,", "NuGet.", "RouteDebugger,",
                "SqlCE4Umbraco,", "umbraco.",
                "Lucene.", "Examine,", "AutoMapper.",
                "Examine.", "ServiceStack.", "MySql.",
                "HtmlAgilityPack.", "TidyNet.",
                "ICSharpCode.", "CookComputing.",
                "AzureDirectory,", "itextsharp,",
                "UrlRewritingNet.", "HtmlAgilityPack,",
                "MiniProfiler,", "Moq,", "nunit.",
                "TidyNet,", "WebDriver,"
            };

        /// <summary>
        /// A collection of all assemblies.
        /// </summary>
        private static HashSet<Assembly> allAssemblies;

        /// <summary>
        /// The bin folder assemblies.
        /// </summary>
        private static HashSet<Assembly> binFolderAssemblies;

        /// <summary>
        /// Lazily loads a reference to all assemblies and only local assemblies.
        /// This is a modified version of: 
        /// <see href="http://www.dominicpettifer.co.uk/Blog/44/how-to-get-a-reference-to-all-assemblies-in-the--bin-folder"/>
        /// </summary>
        /// <remarks>
        /// We do this because we cannot use AppDomain.Current.GetAssemblies() as this will return only assemblies that have been 
        /// loaded in the CLR, not all assemblies.
        /// See these threads:
        /// <see href="http://issues.umbraco.org/issue/U5-198"/>
        /// <see href="http://stackoverflow.com/questions/3552223/asp-net-appdomain-currentdomain-getassemblies-assemblies-missing-after-app"/>
        /// <see href="http://stackoverflow.com/questions/2477787/difference-between-appdomain-getassemblies-and-buildmanager-getreferencedassembl"/>
        /// </remarks>
        /// <returns>
        /// The <see cref="HashSet{Assembly}"/>.
        /// </returns>
        internal static HashSet<Assembly> GetAllAssemblies()
        {
            using (UpgradeableReadLock locker = new UpgradeableReadLock(Locker))
            {
                if (allAssemblies == null)
                {
                    locker.UpgradeToWriteLock();

                    try
                    {
                        // NOTE: we cannot use AppDomain.CurrentDomain.GetAssemblies() because this only returns assemblies that have
                        // already been loaded in to the app domain, instead we will look directly into the bin folder and load each one.
                        string binFolder = IOHelper.GetRootDirectoryBinFolder();
                        List<string> binAssemblyFiles = Directory.GetFiles(binFolder, "*.dll", SearchOption.TopDirectoryOnly).ToList();
                        HashSet<Assembly> assemblies = new HashSet<Assembly>();

                        foreach (string file in binAssemblyFiles)
                        {
                            try
                            {
                                AssemblyName assemblyName = AssemblyName.GetAssemblyName(file);
                                assemblies.Add(Assembly.Load(assemblyName));
                            }
                            catch (Exception ex)
                            {
                                if (ex is SecurityException || ex is BadImageFormatException)
                                {
                                    // Swallow exception but allow debugging.
                                    Debug.WriteLine(ex.Message);
                                }
                                else
                                {
                                    throw;
                                }
                            }
                        }

                        // If for some reason they are still no assemblies, then use the AppDomain to load in already loaded assemblies.
                        if (!assemblies.Any())
                        {
                            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                            {
                                assemblies.Add(assembly);
                            }
                        }

                        // Here we are trying to get the App_Code assembly
                        string[] fileExtensions = { ".cs", ".vb" };
                        DirectoryInfo appCodeFolder = new DirectoryInfo(IOHelper.MapPath("~/App_code"));

                        // Check if the folder exists and if there are any files in it with the supported file extensions
                        if (appCodeFolder.Exists && fileExtensions.Any(x => appCodeFolder.GetFiles("*" + x).Any()))
                        {
                            Assembly appCodeAssembly = Assembly.Load("App_Code");
                            if (!assemblies.Contains(appCodeAssembly))
                            {
                                assemblies.Add(appCodeAssembly);
                            }
                        }

                        // Now set the allAssemblies
                        allAssemblies = new HashSet<Assembly>(assemblies);
                    }
                    catch (InvalidOperationException e)
                    {
                        if (!(e.InnerException is SecurityException))
                        {
                            throw;
                        }

                        binFolderAssemblies = allAssemblies;
                    }
                }

                return allAssemblies;
            }
        }

        /// <summary>
        /// Returns only assemblies found in the bin folder that have been loaded into the app domain.
        /// </summary>
        /// <returns>
        /// The collection of assemblies.
        /// </returns>
        internal static HashSet<Assembly> GetBinAssemblies()
        {
            if (binFolderAssemblies == null)
            {
                using (new WriteLock(Locker))
                {
                    Assembly[] assemblies = GetAssembliesWithKnownExclusions().ToArray();
                    DirectoryInfo binFolder = Assembly.GetExecutingAssembly().GetAssemblyFile().Directory;
                    // ReSharper disable once PossibleNullReferenceException
                    List<string> binAssemblyFiles = Directory.GetFiles(binFolder.FullName, "*.dll", SearchOption.TopDirectoryOnly).ToList();
                    IEnumerable<AssemblyName> domainAssemblyNames = binAssemblyFiles.Select(AssemblyName.GetAssemblyName);
                    HashSet<Assembly> safeDomainAssemblies = new HashSet<Assembly>();
                    HashSet<Assembly> binFolderAssemblyList = new HashSet<Assembly>();

                    foreach (Assembly assembly in assemblies)
                    {
                        safeDomainAssemblies.Add(assembly);
                    }

                    foreach (AssemblyName assemblyName in domainAssemblyNames)
                    {
                        Assembly foundAssembly = safeDomainAssemblies
                                 .FirstOrDefault(a => a.GetAssemblyFile() == assemblyName.GetAssemblyFile());

                        if (foundAssembly != null)
                        {
                            binFolderAssemblyList.Add(foundAssembly);
                        }
                    }

                    binFolderAssemblies = new HashSet<Assembly>(binFolderAssemblyList);
                }
            }

            return binFolderAssemblies;
        }

        /// <summary>
        /// Return a list of found local Assemblies excluding the known assemblies we don't want to scan 
        /// and excluding the ones passed in and excluding the exclusion list filter, the results of this are
        /// cached for performance reasons.
        /// </summary>
        /// <param name="excludeFromResults">
        /// An <see cref="IEnumerable{Assembly}"/> to exclude.
        /// </param>
        /// <returns>The collection of local assemblies.</returns>
        internal static HashSet<Assembly> GetAssembliesWithKnownExclusions(
            IEnumerable<Assembly> excludeFromResults = null)
        {
            using (UpgradeableReadLock locker = new UpgradeableReadLock(LocalFilteredAssemblyCacheLocker))
            {
                if (LocalFilteredAssemblyCache.Any())
                {
                    return LocalFilteredAssemblyCache;
                }

                locker.UpgradeToWriteLock();

                IEnumerable<Assembly> assemblies = GetFilteredAssemblies(excludeFromResults, KnownAssemblyExclusionFilter);
                foreach (Assembly assembly in assemblies)
                {
                    LocalFilteredAssemblyCache.Add(assembly);
                }

                return LocalFilteredAssemblyCache;
            }
        }

        /// <summary>
        /// Return a distinct list of found local Assemblies and excluding the ones passed in and excluding the exclusion list filter
        /// </summary>
        /// <param name="excludeFromResults">
        /// An <see cref="IEnumerable{Assembly}"/> to exclude.
        /// </param>
        /// <param name="exclusionFilter">
        /// An <see cref="string"/> array containing exclusion filters.
        /// </param>
        /// <returns>The collection of filtered local assemblies.</returns>
        private static IEnumerable<Assembly> GetFilteredAssemblies(
            IEnumerable<Assembly> excludeFromResults = null,
            string[] exclusionFilter = null)
        {
            if (excludeFromResults == null)
            {
                excludeFromResults = new HashSet<Assembly>();
            }

            if (exclusionFilter == null)
            {
                exclusionFilter = new string[] { };
            }

            return GetAllAssemblies()
                .Where(x => !excludeFromResults.Contains(x)
                            && !x.GlobalAssemblyCache
                            && !exclusionFilter.Any(f => x.FullName.StartsWith(f, StringComparison.OrdinalIgnoreCase)));
        }
    }
}
