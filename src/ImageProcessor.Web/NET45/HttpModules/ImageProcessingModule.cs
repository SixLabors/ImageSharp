// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ImageProcessingModule.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Processes any image requests within the web application.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Web.HttpModules
{
    #region Using
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Reflection;
    using System.Security;
    using System.Security.Permissions;
    using System.Security.Principal;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web;
    using System.Web.Hosting;
    using System.Web.Security;

    using ImageProcessor.Core.Common.Extensions;
    using ImageProcessor.Web.Caching;
    using ImageProcessor.Web.Configuration;
    using ImageProcessor.Web.Helpers;
    #endregion

    /// <summary>
    /// Processes any image requests within the web application.
    /// </summary>
    public sealed class ImageProcessingModule : IHttpModule
    {
        #region Fields
        /// <summary>
        /// The key for storing the response type of the current image.
        /// </summary>
        private const string CachedResponseTypeKey = "CACHED_IMAGE_RESPONSE_TYPE_054F217C-11CF-49FF-8D2F-698E8E6EB58F";

        /// <summary>
        /// The regular expression to search strings for.
        /// </summary>
        private static readonly Regex PresetRegex = new Regex(@"preset=[^&]+", RegexOptions.Compiled);

        /// <summary>
        /// The assembly version.
        /// </summary>
        private static readonly string AssemblyVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();

        /// <summary>
        /// The collection of SemaphoreSlims for identifying given locking individual queries.
        /// </summary>
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> SemaphoreSlims = new ConcurrentDictionary<string, SemaphoreSlim>();

        /// <summary>
        /// The value to prefix any remote image requests with to ensure they get captured.
        /// </summary>
        private static string remotePrefix;

        /// <summary>
        /// Whether to preserve exif meta data.
        /// </summary>
        private static bool? preserveExifMetaData;

        /// <summary>
        /// A value indicating whether this instance of the given entity has been disposed.
        /// </summary>
        /// <value><see langword="true"/> if this instance has been disposed; otherwise, <see langword="false"/>.</value>
        /// <remarks>
        /// If the entity is disposed, it must not be disposed a second
        /// time. The isDisposed field is set the first time the entity
        /// is disposed. If the isDisposed field is true, then the Dispose()
        /// method will not dispose again. This help not to prolong the entity's
        /// life in the Garbage Collector.
        /// </remarks>
        private bool isDisposed;
        #endregion

        #region Destructors
        /// <summary>
        /// Finalizes an instance of the <see cref="T:ImageProcessor.Web.HttpModules.ImageProcessingModule"/> class. 
        /// </summary>
        /// <remarks>
        /// Use C# destructor syntax for finalization code.
        /// This destructor will run only if the Dispose method 
        /// does not get called.
        /// It gives your base class the opportunity to finalize.
        /// Do not provide destructors in types derived from this class.
        /// </remarks>
        ~ImageProcessingModule()
        {
            // Do not re-create Dispose clean-up code here.
            // Calling Dispose(false) is optimal in terms of
            // readability and maintainability.
            this.Dispose(false);
        }
        #endregion

        #region IHttpModule Members
        /// <summary>
        /// Initializes a module and prepares it to handle requests.
        /// </summary>
        /// <param name="context">
        /// An <see cref="T:System.Web.HttpApplication"/> that provides 
        /// access to the methods, properties, and events common to all 
        /// application objects within an ASP.NET application
        /// </param>
        public void Init(HttpApplication context)
        {
            if (remotePrefix == null)
            {
                remotePrefix = ImageProcessorConfiguration.Instance.RemotePrefix;
            }

            if (preserveExifMetaData == null)
            {
                preserveExifMetaData = ImageProcessorConfiguration.Instance.PreserveExifMetaData;
            }

#if NET45
            EventHandlerTaskAsyncHelper wrapper = new EventHandlerTaskAsyncHelper(this.PostAuthorizeRequest);
            context.AddOnPostAuthorizeRequestAsync(wrapper.BeginEventHandler, wrapper.EndEventHandler);
#else
            context.PostAuthorizeRequest += this.PostAuthorizeRequest;
#endif
            context.PreSendRequestHeaders += this.ContextPreSendRequestHeaders;
        }

        /// <summary>
        /// Disposes of the resources (other than memory) used by the module that implements <see cref="T:System.Web.IHttpModule"/>.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);

            // This object will be cleaned up by the Dispose method.
            // Therefore, you should call GC.SupressFinalize to
            // take this object off the finalization queue 
            // and prevent finalization code for this object
            // from executing a second time.
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Gets the specific <see cref="T:System.Threading.SemaphoreSlim"/> for the given id.
        /// </summary>
        /// <param name="id">
        /// The id representing the <see cref="T:System.Threading.SemaphoreSlim"/>.
        /// </param>
        /// <returns>
        /// The <see cref="T:System.Threading.Mutex"/> for the given id.
        /// </returns>
        private static SemaphoreSlim GetSemaphoreSlim(string id)
        {
            id = id.ToMD5Fingerprint();
            SemaphoreSlim semaphore = SemaphoreSlims.GetOrAdd(id, new SemaphoreSlim(1, 1));
            return semaphore;
        }

        /// <summary>
        /// Disposes the object and frees resources for the Garbage Collector.
        /// </summary>
        /// <param name="disposing">If true, the object gets disposed.</param>
        private void Dispose(bool disposing)
        {
            if (this.isDisposed)
            {
                return;
            }

            if (disposing)
            {
                // Dispose of any managed resources here.
                foreach (KeyValuePair<string, SemaphoreSlim> semaphore in SemaphoreSlims)
                {
                    semaphore.Value.Dispose();
                }

                SemaphoreSlims.Clear();
            }

            // Call the appropriate methods to clean up
            // unmanaged resources here.
            // Note disposing is done.
            this.isDisposed = true;
        }
        #endregion

#if NET45

        /// <summary>
        /// Occurs when the user for the current request has been authorized.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// An <see cref="T:System.EventArgs">EventArgs</see> that contains the event data.
        /// </param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task"/>.
        /// </returns>
        private Task PostAuthorizeRequest(object sender, EventArgs e)
        {
            HttpContext context = ((HttpApplication)sender).Context;
            return this.ProcessImageAsync(context);
        }

#else

        /// <summary>
        /// Occurs when the user for the current request has been authorized.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="T:System.EventArgs">EventArgs</see> that contains the event data.</param>
        private async void PostAuthorizeRequest(object sender, EventArgs e)
        {
            HttpContext context = ((HttpApplication)sender).Context;
            await this.ProcessImageAsync(context);
        }

#endif

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

                // Set the headers
                this.SetHeaders(context, responseType);

                context.Items[CachedResponseTypeKey] = null;
            }
        }

        #region Private
        /// <summary>
        /// Processes the image.
        /// </summary>
        /// <param name="context">
        /// the <see cref="T:System.Web.HttpContext">HttpContext</see> object that provides 
        /// references to the intrinsic server objects 
        /// </param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task"/>.
        /// </returns>
        [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1122:UseStringEmptyForEmptyStrings", Justification = "Reviewed. Suppression is OK here.")]
        private async Task ProcessImageAsync(HttpContext context)
        {
            HttpRequest request = context.Request;

            // Fixes issue 10.
            bool isRemote = request.Path.EndsWith(remotePrefix, StringComparison.OrdinalIgnoreCase);
            string requestPath = string.Empty;
            string queryString = string.Empty;
            bool validExtensionLessUrl = false;
            string urlParameters = "";
            string extensionLessExtension = "";

            if (isRemote)
            {
                // We need to split the querystring to get the actual values we want.
                string urlDecode = HttpUtility.UrlDecode(request.QueryString.ToString());

                if (!string.IsNullOrWhiteSpace(urlDecode))
                {
                    // UrlDecode seems to mess up in some circumstance.
                    if (urlDecode.IndexOf("://", StringComparison.OrdinalIgnoreCase) == -1)
                    {
                        urlDecode = urlDecode.Replace(":/", "://");
                    }

                    string[] paths = urlDecode.Split('?');

                    requestPath = paths[0];

                    // Handle extension-less urls.
                    if (paths.Count() > 2)
                    {
                        queryString = paths[2];
                        urlParameters = paths[1];
                    }
                    else if (paths.Length > 1)
                    {
                        queryString = paths[1];
                    }

                    validExtensionLessUrl = RemoteFile.RemoteFileWhiteListExtensions.Any(
                        x => x.ExtensionLess && requestPath.StartsWith(x.Url.AbsoluteUri));

                    if (validExtensionLessUrl)
                    {
                        extensionLessExtension = RemoteFile.RemoteFileWhiteListExtensions.First(
                        x => x.ExtensionLess && requestPath.StartsWith(x.Url.AbsoluteUri)).ImageFormat;
                    }
                }
            }
            else
            {
                requestPath = HostingEnvironment.MapPath(request.Path);
                queryString = HttpUtility.UrlDecode(request.QueryString.ToString());
            }

            // Only process requests that pass our sanitizing filter.
            if ((ImageHelpers.IsValidImageExtension(requestPath) || validExtensionLessUrl) && !string.IsNullOrWhiteSpace(queryString))
            {
                // Replace any presets in the querystring with the actual value.
                queryString = this.ReplacePresetsInQueryString(queryString);

                string fullPath = string.Format("{0}?{1}", requestPath, queryString);
                string imageName = Path.GetFileName(requestPath);

                if (validExtensionLessUrl && !string.IsNullOrWhiteSpace(extensionLessExtension))
                {
                    fullPath = requestPath;

                    if (!string.IsNullOrWhiteSpace(urlParameters))
                    {
                        string hashedUrlParameters = urlParameters.ToMD5Fingerprint();

                        // TODO: Add hash for querystring parameters.    
                        imageName += hashedUrlParameters;
                        fullPath += hashedUrlParameters;
                    }

                    imageName += "." + extensionLessExtension;
                    fullPath += extensionLessExtension + "?" + queryString;
                }

                // Create a new cache to help process and cache the request.
                DiskCache cache = new DiskCache(request, requestPath, fullPath, imageName);

                // Since we are now rewriting the path we need to check again that the current user has access
                // to the rewritten path.
                // Get the user for the current request
                // If the user is anonymous or authentication doesn't work for this suffix avoid a NullReferenceException 
                // in the UrlAuthorizationModule by creating a generic identity.
                string virtualCachedPath = cache.GetVirtualCachedPath();

                IPrincipal user = context.User ?? new GenericPrincipal(new GenericIdentity(string.Empty, string.Empty), new string[0]);

                // Do we have permission to call UrlAuthorizationModule.CheckUrlAccessForPrincipal?
                PermissionSet permission = new PermissionSet(PermissionState.None);
                permission.AddPermission(new AspNetHostingPermission(AspNetHostingPermissionLevel.Unrestricted));
                bool hasPermission = permission.IsSubsetOf(AppDomain.CurrentDomain.PermissionSet);
                bool isAllowed = true;

                // Run the rewritten path past the auth system again, using the result as the default "AllowAccess" value
                if (hasPermission && !context.SkipAuthorization)
                {
                    isAllowed = UrlAuthorizationModule.CheckUrlAccessForPrincipal(virtualCachedPath, user, "GET");
                }

                if (isAllowed)
                {
                    // Is the file new or updated?
                    bool isNewOrUpdated = await cache.IsNewOrUpdatedFileAsync();

                    // Only process if the file has been updated.
                    if (isNewOrUpdated)
                    {
                        string cachedPath = cache.CachedPath;

                        // Process the image.
                        using (ImageFactory imageFactory = new ImageFactory(preserveExifMetaData != null && preserveExifMetaData.Value))
                        {
                            if (isRemote)
                            {
                                Uri uri = new Uri(requestPath + "?" + urlParameters);

                                RemoteFile remoteFile = new RemoteFile(uri, false);

                                // Prevent response blocking.
                                WebResponse webResponse = await remoteFile.GetWebResponseAsync().ConfigureAwait(false);

                                SemaphoreSlim semaphore = GetSemaphoreSlim(cachedPath);
                                try
                                {
                                    semaphore.Wait();

                                    using (MemoryStream memoryStream = new MemoryStream())
                                    {
                                        using (WebResponse response = webResponse)
                                        {
                                            using (Stream responseStream = response.GetResponseStream())
                                            {
                                                if (responseStream != null)
                                                {
                                                    responseStream.CopyTo(memoryStream);

                                                    // Reset the position of the stream to ensure we're reading the correct part.
                                                    memoryStream.Position = 0;

                                                    // Process the Image
                                                    imageFactory.Load(memoryStream)
                                                                .AutoProcess(queryString)
                                                                .Save(cachedPath);

                                                    // Store the response type in the context for later retrieval.
                                                    context.Items[CachedResponseTypeKey] = imageFactory.CurrentImageFormat.MimeType;

                                                    // Add to the cache.
                                                    cache.AddImageToCache();

                                                    // Trim the cache.
                                                    await cache.TrimCachedFolderAsync(cachedPath);
                                                }
                                            }
                                        }
                                    }
                                }
                                finally
                                {
                                    semaphore.Release();
                                }
                            }
                            else
                            {
                                // Check to see if the file exists.
                                // ReSharper disable once AssignNullToNotNullAttribute
                                FileInfo fileInfo = new FileInfo(requestPath);

                                if (!fileInfo.Exists)
                                {
                                    throw new HttpException(404, "No image exists at " + fullPath);
                                }

                                SemaphoreSlim semaphore = GetSemaphoreSlim(cachedPath);
                                try
                                {
                                    semaphore.Wait();

                                    // Process the Image
                                    imageFactory.Load(requestPath)
                                                .AutoProcess(queryString)
                                                .Save(cachedPath);

                                    // Store the response type in the context for later retrieval.
                                    context.Items[CachedResponseTypeKey] = imageFactory.CurrentImageFormat.MimeType;

                                    // Add to the cache.
                                    cache.AddImageToCache();

                                    // Trim the cache.
                                    await cache.TrimCachedFolderAsync(cachedPath);
                                }
                                finally
                                {
                                    semaphore.Release();
                                }
                            }
                        }
                    }

                    string incomingEtag = context.Request.Headers["If-None-Match"];

                    if (incomingEtag != null && !isNewOrUpdated)
                    {
                        // Explicitly set the Content-Length header so the client doesn't wait for
                        // content but keeps the connection open for other requests
                        context.Response.AddHeader("Content-Length", "0");
                        context.Response.StatusCode = (int)HttpStatusCode.NotModified;
                        context.Response.SuppressContent = true;
                        context.Response.AddFileDependency(context.Server.MapPath(cache.GetVirtualCachedPath()));
                        this.SetHeaders(context, (string)context.Items[CachedResponseTypeKey]);

                        if (!isRemote)
                        {
                            return;
                        }
                    }

                    string virtualPath = cache.GetVirtualCachedPath();

                    // The cached file is valid so just rewrite the path.
                    context.RewritePath(virtualPath, false);
                }
                else
                {
                    throw new HttpException(403, "Access denied");
                }
            }
            else if (isRemote)
            {
                // Just repoint to the external url.
                HttpContext.Current.Response.Redirect(requestPath);
            }
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

            response.AddHeader("Image-Served-By", "ImageProcessor.Web/" + AssemblyVersion);

            HttpCachePolicy cache = response.Cache;
            cache.SetCacheability(HttpCacheability.Public);
            cache.VaryByHeaders["Accept-Encoding"] = true;
            cache.SetLastModifiedFromFileDependencies();

            int maxDays = DiskCache.MaxFileCachedDuration;

            cache.SetExpires(DateTime.Now.ToUniversalTime().AddDays(maxDays));
            cache.SetMaxAge(new TimeSpan(maxDays, 0, 0, 0));
            cache.SetRevalidation(HttpCacheRevalidation.AllCaches);
        }

        /// <summary>
        /// Replaces preset values stored in the configuration in the querystring.
        /// </summary>
        /// <param name="queryString">
        /// The query string.
        /// </param>
        /// <returns>
        /// The <see cref="string"/> containing the updated querystring.
        /// </returns>
        private string ReplacePresetsInQueryString(string queryString)
        {
            foreach (Match match in PresetRegex.Matches(queryString))
            {
                if (match.Success)
                {
                    string preset = match.Value.Split('=')[1];

                    // We use the processor config system to store the preset values.
                    string replacements = ImageProcessorConfiguration.Instance.GetPresetSettings(preset);
                    queryString = Regex.Replace(queryString, preset, replacements ?? string.Empty);
                }
            }

            return queryString;
        }
        #endregion
    }
}