// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TaskHelpers.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Provides some syntactic sugar to run tasks.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Web.Helpers
{
    #region Using
    using System;
    using System.Threading.Tasks;
    #endregion

    /// <summary>
    /// Provides some syntactic sugar to run tasks.
    /// </summary>
    public sealed class TaskHelpers
    {
        /// <summary>
        /// Queues the specified work to run on the ThreadPool and returns a Task handle for that work.
        /// </summary>
        /// <param name="action">The work to execute asynchronously</param> 
        /// <returns>A Task that represents the work queued to execute in the ThreadPool.</returns>
        /// <exception cref="T:System.ArgumentNullException"> 
        /// The <paramref name="action"/> parameter was null. 
        /// </exception>
        public static Task Run(Action action)
        {
            return Task.Factory.StartNew(action);
        }

        /// <summary> 
        /// Queues the specified work to run on the ThreadPool and returns a proxy for the 
        /// Task(TResult) returned by <paramref name="function"/>.
        /// </summary> 
        /// <typeparam name="TResult">The type of the result returned by the proxy Task.</typeparam>
        /// <param name="function">The work to execute asynchronously</param>
        /// <returns>A Task(TResult) that represents a proxy for the Task(TResult) returned by <paramref name="function"/>.</returns>
        /// <exception cref="T:System.ArgumentNullException"> 
        /// The <paramref name="function"/> parameter was null.
        /// </exception> 
        public static Task<TResult> Run<TResult>(Func<TResult> function)
        {
            return Task<TResult>.Factory.StartNew(function);
        }
    }
}
