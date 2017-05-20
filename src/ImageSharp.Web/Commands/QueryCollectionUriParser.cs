// <copyright file="QueryCollectionUriParser.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Web.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.WebUtilities;

    /// <summary>
    /// Parses commands from the request querystring.
    /// </summary>
    public class QueryCollectionUriParser : IUriParser
    {
        /// <inheritdoc/>
        public IDictionary<string, string> ParseUriCommands(HttpContext context)
        {
            if (!context.Request.Query.Any())
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            return QueryHelpers.ParseQuery(context.Request.QueryString.ToUriComponent())
                               .ToDictionary(k => k.Key, v => v.Value.ToString());
        }
    }
}