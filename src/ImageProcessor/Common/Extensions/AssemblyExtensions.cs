// --------------------------------------------------------------------------------------------------------------------
// <copyright file="AssemblyExtensions.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encapsulates a series of time saving extension methods to the <see cref="T:System.Reflection.Assembly" /> class.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Common.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    /// <summary>
    /// Encapsulates a series of time saving extension methods to the <see cref="T:System.Reflection.Assembly"/> class.
    /// </summary>
    public static class AssemblyExtensions
    {
        /// <summary>
        /// Gets a collection of loadable types from the given assembly.
        /// Adapted from <see href="http://stackoverflow.com/questions/7889228/how-to-prevent-reflectiontypeloadexception-when-calling-assembly-gettypes"/>
        /// </summary>
        /// <param name="assembly">
        /// The <see cref="System.Reflection.Assembly"/> to load the types from.
        /// </param>
        /// <returns>
        /// The loadable <see cref="System.Collections.Generic.IEnumerable{Type}"/>.
        /// </returns>
        public static IEnumerable<Type> GetLoadableTypes(this Assembly assembly)
        {
            if (assembly == null)
            {
                throw new ArgumentNullException("assembly");
            }

            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t != null);
            }
        }
    }
}