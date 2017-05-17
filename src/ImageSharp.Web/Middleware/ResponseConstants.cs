// <copyright file="ResponseConstants.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Web.Middleware
{
    using System.Threading.Tasks;

    /// <summary>
    /// Contains constants related to HTTP respose codes.
    /// </summary>
    internal static class ResponseConstants
    {
        /// <summary>
        /// The HTTP 200 OK success status response code indicates that the request has succeeded.
        /// </summary>
        internal const int Status200Ok = 200;

        /// <summary>
        /// The HTTP 304 Not Modified client redirection response code indicates that there is no need
        /// to retransmit the requested resources.
        /// </summary>
        internal const int Status304NotModified = 304;

        /// <summary>
        /// The HTTP 412 Precondition Failed client error response code indicates that access to the target
        /// resource has been denied.
        /// </summary>
        internal const int Status412PreconditionFailed = 412;

        /// <summary>
        /// An empty completed task
        /// </summary>
        internal static readonly Task CompletedTask = CreateCompletedTask();

        private static Task CreateCompletedTask()
        {
            var tcs = new TaskCompletionSource<object>();
            tcs.SetResult(null);
            return tcs.Task;
        }
    }
}
