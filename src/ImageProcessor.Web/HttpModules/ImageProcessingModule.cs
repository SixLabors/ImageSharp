// -----------------------------------------------------------------------
// <copyright file="ImageProcessingModule.cs" company="James South">
//     Copyright (c) James South.
//     Dual licensed under the MIT or GPL Version 2 licenses.
// </copyright>
// -----------------------------------------------------------------------

namespace ImageProcessor.Web.HttpModules
{
    #region Using
    using System;
    using System.IO;
    using System.Net;
    using System.Web;
    using System.Web.Hosting;
    using ImageProcessor.Helpers.Extensions;
    using ImageProcessor.Imaging;
    using ImageProcessor.Web.Caching;
    using ImageProcessor.Web.Config;
    using ImageProcessor.Web.Helpers;
    #endregion

    /// <summary>
    /// Processes any image requests within the web application.
    /// </summary>
    public class ImageProcessingModule : IHttpModule
    {
        #region Fields
        /// <summary>
        /// The key for storing the response type of the current image.
        /// </summary>
        private const string CachedResponseTypeKey = "CACHED_IMAGE_RESPONSE_TYPE";

        /// <summary>
        /// The value to prefix any remote image requests with to ensure they get captured.
        /// </summary>
        private static readonly string RemotePrefix = ImageProcessorConfig.Instance.RemotePrefix;

        /// <summary>
        /// The object to lock against.
        /// </summary>
        private static readonly object SyncRoot = new object();
        #endregion

        #region IHttpModule Members
        /// <summary>
        /// Initializes a module and prepares it to handle requests.
        /// </summary>
        /// <param name="context">An <see cref="T:System.Web.HttpApplication"/> that provides access to the methods, properties, and events common to all application objects within an ASP.NET application </param>
        public void Init(HttpApplication context)
        {
            context.BeginRequest += this.ContextBeginRequest;
            context.PreSendRequestHeaders += this.ContextPreSendRequestHeaders;
        }

        /// <summary>
        /// Disposes of the resources (other than memory) used by the module that implements <see cref="T:System.Web.IHttpModule"/>.
        /// </summary>
        public void Dispose()
        {
            // Nothing to dispose.
        }
        #endregion

        /// <summary>
        /// Occurs as the first event in the HTTP pipeline chain of execution when ASP.NET responds to a request.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="T:System.EventArgs">EventArgs</see> that contains the event data.</param>
        private void ContextBeginRequest(object sender, EventArgs e)
        {
            HttpContext context = ((HttpApplication)sender).Context;

            // Is this a remote file.
            bool isRemote = context.Request.Path.Equals(RemotePrefix, StringComparison.OrdinalIgnoreCase);
            string path = string.Empty;
            string queryString = string.Empty;

            if (isRemote)
            {
                // We need to split the querystring to get the actual values we want.
                string urlDecode = HttpUtility.UrlDecode(context.Request.QueryString.ToString());

                if (urlDecode != null)
                {
                    string[] paths = urlDecode.Split('?');

                    path = paths[0];

                    if (paths.Length > 1)
                    {
                        queryString = paths[1];
                    }
                }
            }
            else
            {
                path = HostingEnvironment.MapPath(context.Request.Path);
                queryString = HttpUtility.UrlDecode(context.Request.QueryString.ToString());
            }

            if (ImageUtils.IsValidImageExtension(path) && !string.IsNullOrWhiteSpace(queryString))
            {
                string fullPath = string.Format("{0}?{1}", path, queryString);
                string imageName = Path.GetFileName(path);
                string cachedPath = DiskCache.GetCachePath(fullPath, imageName);

                if (path != null && this.FileExists(path, isRemote))
                {
                    bool exists = File.Exists(cachedPath);
                    bool updated = DiskCache.IsUpdatedFile(path, cachedPath);

                    if ((exists == false) || (!isRemote && updated))
                    {
                        // Process the image.
                        using (ImageFactory imageFactory = new ImageFactory())
                        {
                            if (isRemote)
                            {
                                Uri uri = new Uri(path);
                                RemoteFile remoteFile = new RemoteFile(uri, false);

                                using (MemoryStream memoryStream = new MemoryStream())
                                {
                                    using (Stream responseStream = remoteFile.GetWebResponse().GetResponseStream())
                                    {
                                        if (responseStream != null)
                                        {
                                            lock (SyncRoot)
                                            {
                                                // Trim the cache.
                                                DiskCache.PurgeFolders();

                                                responseStream.CopyTo(memoryStream);

                                                imageFactory.Load(memoryStream)
                                                    .AddQueryString(queryString)
                                                    .Format(ImageUtils.GetImageFormat(imageName))
                                                    .AutoProcess().Save(cachedPath);

                                                // Ensure that the LastWriteTime property of the source and cached file match.
                                                DateTime dateTime = DiskCache.SetCachedLastWriteTime(path, cachedPath);

                                                // Add to the cache.
                                                DiskCache.AddImageToCache(cachedPath, dateTime);
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                lock (SyncRoot)
                                {
                                    // Trim the cache.
                                    DiskCache.PurgeFolders();

                                    imageFactory.Load(fullPath).AutoProcess().Save(cachedPath);

                                    // Ensure that the LastWriteTime property of the source and cached file match.
                                    DateTime dateTime = DiskCache.SetCachedLastWriteTime(path, cachedPath);

                                    // Add to the cache.
                                    DiskCache.AddImageToCache(cachedPath, dateTime);
                                }
                            }
                        }
                    }

                    context.Items[CachedResponseTypeKey] = ImageUtils.GetResponseType(imageName).ToDescription();

                    // The cached file is valid so just rewrite the path.
                    context.RewritePath(DiskCache.GetVirtualPath(cachedPath, context.Request), false);
                }
            }
        }

        /// <summary>
        /// Occurs just before ASP.NET send HttpHeaders to the client.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="T:System.EventArgs">EventArgs</see> that contains the event data.</param>
        private void ContextPreSendRequestHeaders(object sender, EventArgs e)
        {
            HttpContext context = ((HttpApplication)sender).Context;

            object responseTypeObject = context.Items[CachedResponseTypeKey];

            if (responseTypeObject != null)
            {
                string responseType = (string)responseTypeObject;

                this.SetHeaders(context, responseType);

                context.Items[CachedResponseTypeKey] = null;
            }
        }

        #region Private
        /// <summary>
        /// returns a value indicating whether a file exists.
        /// </summary>
        /// <param name="path">The path to the file to check.</param>
        /// <param name="remote">Whether the file is remote.</param>
        /// <returns>True if the file exists, otherwise false.</returns>
        /// <remarks>If the file is remote the method will always return true.</remarks>
        private bool FileExists(string path, bool remote)
        {
            return remote || File.Exists(path);
        }

        /// <summary>
        /// This will make the browser and server keep the output
        /// in its cache and thereby improve performance.
        /// See http://en.wikipedia.org/wiki/HTTP_ETag
        /// </summary>
        /// <param name="context">
        /// the <see cref="T:System.Web.HttpContext">HttpContext</see> object that provides 
        /// references to the intrinsic server objects 
        /// </param>
        /// <param name="responseType">The HTTP MIME type to to send.</param>
        private void SetHeaders(HttpContext context, string responseType)
        {
            HttpResponse response = context.Response;

            response.ContentType = responseType;

            HttpCachePolicy cache = response.Cache;

            cache.VaryByHeaders["Accept-Encoding"] = true;

            int maxDays = DiskCache.MaxFileCachedDuration;

            cache.SetExpires(DateTime.Now.ToUniversalTime().AddDays(maxDays));
            cache.SetMaxAge(new TimeSpan(maxDays, 0, 0, 0));
            cache.SetRevalidation(HttpCacheRevalidation.AllCaches);

            string incomingEtag = context.Request.Headers["If-None-Match"];

            cache.SetCacheability(HttpCacheability.Public);

            if (incomingEtag == null)
            {
                return;
            }

            response.Clear();
            response.StatusCode = (int)HttpStatusCode.NotModified;
            response.SuppressContent = true;
        }
        #endregion
    }
}