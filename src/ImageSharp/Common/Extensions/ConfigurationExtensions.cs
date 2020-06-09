// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Threading.Tasks;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Contains extension methods for <see cref="Configuration"/>
    /// </summary>
    internal static class ConfigurationExtensions
    {
        /// <summary>
        /// Creates a <see cref="ParallelOptions"/> object based on <paramref name="configuration"/>,
        /// having <see cref="ParallelOptions.MaxDegreeOfParallelism"/> set to <see cref="Configuration.MaxDegreeOfParallelism"/>
        /// </summary>
        public static ParallelOptions GetParallelOptions(this Configuration configuration)
        {
            return new ParallelOptions { MaxDegreeOfParallelism = configuration.MaxDegreeOfParallelism };
        }
    }
}
