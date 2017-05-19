// <copyright file="IUriParser.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Web.Commands
{
    using System.Collections.Generic;

    using Microsoft.AspNetCore.Http;

    /// <summary>
    /// Defines a contract for parsing commands from URI's
    /// </summary>
    public interface IUriParser
    {
        /// <summary>
        /// Returns a collection of commands from the current request
        /// </summary>
        /// <param name="context">Encapsulates all HTTP-specific information about an individual HTTP request</param>
        /// <returns>The <see cref="IDictionary{TKey,TValue}"/></returns>
        IDictionary<string, string> ParseUriCommands(HttpContext context);
    }
}