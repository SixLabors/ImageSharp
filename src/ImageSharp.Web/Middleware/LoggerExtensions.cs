// <copyright file="LoggerExtensions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Web.Middleware
{
    using System;

    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Extensions methods for the <see cref="ILogger"/> interface.
    /// </summary>
    internal static class LoggerExtensions
    {
        private static readonly Action<ILogger, string, Exception> LogProcessingErrorAction;
        private static readonly Action<ILogger, string, Exception> LogResolveFailedAction;
        private static readonly Action<ILogger, string, string, Exception> LogServedAction;
        private static readonly Action<ILogger, string, Exception> LogPathNotModifiedAction;
        private static readonly Action<ILogger, string, Exception> LogPreconditionFailedAction;

        /// <summary>
        /// Initializes static members of the <see cref="LoggerExtensions"/> class.
        /// </summary>
        static LoggerExtensions()
        {
            LogProcessingErrorAction = LoggerMessage.Define<string>(
                logLevel: LogLevel.Error,
                eventId: 1,
                formatString: "The image '{Uri}' could not be processed");

            LogResolveFailedAction = LoggerMessage.Define<string>(
                logLevel: LogLevel.Error,
                eventId: 2,
                formatString: "The image '{Uri}' could not be resolved");

            LogServedAction = LoggerMessage.Define<string, string>(
                logLevel: LogLevel.Information,
                eventId: 3,
                formatString: "Sending image. Request uri: '{Uri}'. Cached Key: '{Key}'");

            LogPathNotModifiedAction = LoggerMessage.Define<string>(
                logLevel: LogLevel.Information,
                eventId: 4,
                formatString: "The image '{Uri}' was not modified");

            LogPreconditionFailedAction = LoggerMessage.Define<string>(
                logLevel: LogLevel.Information,
                eventId: 5,
                formatString: "Precondition for image '{Uri}' failed");
        }

        /// <summary>
        /// Logs that a given image request could not be processed.
        /// </summary>
        /// <param name="logger">The type used to perform logging</param>
        /// <param name="uri">The full request uri</param>
        /// <param name="exception">The captured exception</param>
        public static void LogImageProcessingFailed(this ILogger logger, string uri, Exception exception)
        {
            LogProcessingErrorAction(logger, uri, exception);
        }

        /// <summary>
        /// Logs that a given image could not be resolved.
        /// </summary>
        /// <param name="logger">The type used to perform logging</param>
        /// <param name="uri">The full request uri</param>
        public static void LogImageResolveFailed(this ILogger logger, string uri)
        {
            LogResolveFailedAction(logger, uri, null);
        }

        /// <summary>
        /// Logs that a given image request has been served.
        /// </summary>
        /// <param name="logger">The type used to perform logging</param>
        /// <param name="uri">The full request uri</param>
        /// <param name="key">The cached image key</param>
        public static void LogImageServed(this ILogger logger, string uri, string key)
        {
            LogServedAction(logger, uri, key, null);
        }

        /// <summary>
        /// Logs that a given image request has not been modified.
        /// </summary>
        /// <param name="logger">The type used to perform logging</param>
        /// <param name="uri">The full request uri</param>
        public static void LogImageNotModified(this ILogger logger, string uri)
        {
            LogPathNotModifiedAction(logger, uri, null);
        }

        /// <summary>
        /// Logs that access to a given image request has been denied.
        /// </summary>
        /// <param name="logger">The type used to perform logging</param>
        /// <param name="uri">The full request uri</param>
        public static void LogImagePreconditionFailed(this ILogger logger, string uri)
        {
            LogPreconditionFailedAction(logger, uri, null);
        }
    }
}