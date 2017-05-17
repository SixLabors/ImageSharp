// <copyright file="ImageContext.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Web.Middleware
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Http.Features;
    using Microsoft.AspNetCore.Http.Headers;
    using Microsoft.Extensions.Logging;
    using Microsoft.Net.Http.Headers;

    /// <summary>
    /// Provides information and methods regarding the current image request
    /// </summary>
    internal struct ImageContext
    {
        private readonly HttpRequest request;

        private readonly HttpResponse response;

        private RequestHeaders requestHeaders;

        private ResponseHeaders responseHeaders;

        private DateTimeOffset fileLastModified;
        private long bytesLength;
        private EntityTagHeaderValue etag;

        private PreconditionState ifMatchState;
        private PreconditionState ifNoneMatchState;
        private PreconditionState ifModifiedSinceState;
        private PreconditionState ifUnmodifiedSinceState;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageContext"/> struct.
        /// </summary>
        /// <param name="context">The current HTTP request context</param>
        public ImageContext(HttpContext context)
        {
            this.request = context.Request;
            this.response = context.Response;

            this.requestHeaders = context.Request.GetTypedHeaders();
            this.responseHeaders = context.Response.GetTypedHeaders();

            this.fileLastModified = DateTimeOffset.MinValue;
            this.bytesLength = 0;
            this.etag = null;

            this.ifMatchState = PreconditionState.Unspecified;
            this.ifNoneMatchState = PreconditionState.Unspecified;
            this.ifModifiedSinceState = PreconditionState.Unspecified;
            this.ifUnmodifiedSinceState = PreconditionState.Unspecified;
        }

        /// <summary>
        /// Enumerates the possible precondition states
        /// </summary>
        internal enum PreconditionState
        {
            /// <summary>
            /// Unspeciified
            /// </summary>
            Unspecified,

            /// <summary>
            /// Not modified
            /// </summary>
            NotModified,

            /// <summary>
            /// Should process
            /// </summary>
            ShouldProcess,

            /// <summary>
            /// Precondition Failed
            /// </summary>
            PreconditionFailed,
        }

        /// <summary>
        /// Analyzes the headers for the current request.
        /// </summary>
        /// <param name="lastModified">The point in time when the cached file was last modified.</param>
        /// <param name="length">The length of the cached file in bytes</param>
        public void ComprehendRequestHeaders(DateTimeOffset lastModified, long length)
        {
            this.fileLastModified = lastModified;
            this.bytesLength = length;
            this.ComputeLastModified();

            this.ComputeIfMatch();

            this.ComputeIfModifiedSince();
        }

        /// <summary>
        /// Gets the preconditioned state of the request.
        /// </summary>
        /// <returns>The <see cref="PreconditionState"/></returns>
        public PreconditionState GetPreconditionState()
        {
            return GetMaxPreconditionState(this.ifMatchState, this.ifNoneMatchState, this.ifModifiedSinceState, this.ifUnmodifiedSinceState);
        }

        /// <summary>
        /// Gets a value indicating whther this request is a head request
        /// </summary>
        /// <returns>THe <see cref="bool"/></returns>
        public bool IsHeadRequest()
        {
            return string.Equals("HEAD", this.request.Method, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Set the response status headers
        /// </summary>
        /// <param name="statusCode">The status code</param>
        /// <param name="contentType">The content type</param>
        /// <returns>The <see cref="Task"/></returns>
        public async Task SendStatusAsync(int statusCode, string contentType)
        {
            this.ApplyResponseHeaders(statusCode, contentType);

            // this.logger.LogHandled(statusCode, SubPath);
            await ResponseConstants.CompletedTask;
        }

        /// <summary>
        /// Set the response content
        /// </summary>
        /// <param name="contentType">The content type</param>
        /// <param name="buffer">The cached image buffer</param>
        /// <returns>The <see cref="Task"/></returns>
        public async Task SendAsync(string contentType, byte[] buffer)
        {
            this.ApplyResponseHeaders(ResponseConstants.Status200Ok, contentType);

            // We don't need to directly cancel this, if the client disconnects it will fail silently.
            await this.response.Body.WriteAsync(buffer, 0, buffer.Length, CancellationToken.None);
            if (this.response.Body.CanSeek)
            {
                this.response.Body.Position = 0;
            }
        }

        private static PreconditionState GetMaxPreconditionState(params PreconditionState[] states)
        {
            PreconditionState max = PreconditionState.Unspecified;
            foreach (PreconditionState state in states)
            {
                if (state > max)
                {
                    max = state;
                }
            }

            return max;
        }

        private void ApplyResponseHeaders(int statusCode, string contentType)
        {
            this.response.StatusCode = statusCode;
            if (statusCode < 400)
            {
                // These headers are returned for 200 and 304
                // They are not returned for 412
                if (!string.IsNullOrEmpty(contentType))
                {
                    this.response.ContentType = contentType;
                }

                this.responseHeaders.LastModified = this.fileLastModified;
                this.responseHeaders.ETag = this.etag;
                this.responseHeaders.Headers[HeaderNames.AcceptRanges] = "bytes";

                // TODO: Expires
            }

            if (statusCode == ResponseConstants.Status200Ok)
            {
                // This header is only returned here for 200. It is not returned for 304, and 412
                this.response.ContentLength = this.bytesLength;
            }
        }

        private void ComputeLastModified()
        {
            // Truncate to the second.
            this.fileLastModified = new DateTimeOffset(this.fileLastModified.Year, this.fileLastModified.Month, this.fileLastModified.Day, this.fileLastModified.Hour, this.fileLastModified.Minute, this.fileLastModified.Second, this.fileLastModified.Offset).ToUniversalTime();

            long etagHash = this.fileLastModified.ToFileTime() ^ this.bytesLength;
            this.etag = new EntityTagHeaderValue($"{'\"'}{Convert.ToString(etagHash, 16)}{'\"'}");
        }

        private void ComputeIfMatch()
        {
            // 14.24 If-Match
            IList<EntityTagHeaderValue> ifMatch = this.requestHeaders.IfMatch;

            if (ifMatch != null && ifMatch.Any())
            {
                this.ifMatchState = PreconditionState.PreconditionFailed;
                foreach (EntityTagHeaderValue etag in ifMatch)
                {
                    if (etag.Equals(EntityTagHeaderValue.Any) || etag.Compare(this.etag, true))
                    {
                        this.ifMatchState = PreconditionState.ShouldProcess;
                        break;
                    }
                }
            }

            // 14.26 If-None-Match
            IList<EntityTagHeaderValue> ifNoneMatch = this.requestHeaders.IfNoneMatch;

            if (ifNoneMatch != null && ifNoneMatch.Any())
            {
                this.ifNoneMatchState = PreconditionState.ShouldProcess;
                foreach (EntityTagHeaderValue etag in ifNoneMatch)
                {
                    if (etag.Equals(EntityTagHeaderValue.Any) || etag.Compare(this.etag, true))
                    {
                        this.ifNoneMatchState = PreconditionState.NotModified;
                        break;
                    }
                }
            }
        }

        private void ComputeIfModifiedSince()
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;

            // 14.25 If-Modified-Since
            DateTimeOffset? ifModifiedSince = this.requestHeaders.IfModifiedSince;
            if (ifModifiedSince.HasValue && ifModifiedSince <= now)
            {
                bool modified = ifModifiedSince < this.fileLastModified;
                this.ifModifiedSinceState = modified ? PreconditionState.ShouldProcess : PreconditionState.NotModified;
            }

            // 14.28 If-Unmodified-Since
            DateTimeOffset? ifUnmodifiedSince = this.requestHeaders.IfUnmodifiedSince;
            if (ifUnmodifiedSince.HasValue && ifUnmodifiedSince <= now)
            {
                bool unmodified = ifUnmodifiedSince >= this.fileLastModified;
                this.ifUnmodifiedSinceState = unmodified ? PreconditionState.ShouldProcess : PreconditionState.PreconditionFailed;
            }
        }
    }
}