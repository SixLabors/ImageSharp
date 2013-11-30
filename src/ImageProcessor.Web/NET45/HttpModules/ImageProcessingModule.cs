// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ImageProcessingModule.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Processes any image requests within the web application.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System.Linq;
using System.Security.Cryptography;
using System.Security.Policy;
using System.Text;

namespace ImageProcessor.Web.HttpModules
{
    #region Using
    using System;
    using System.IO;
    using System.Net;
    using System.Reflection;
    using System.Security;
    using System.Security.Permissions;
    using System.Security.Principal;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web;
    using System.Web.Hosting;
    using System.Web.Security;

    using ImageProcessor.Helpers.Extensions;
    using ImageProcessor.Imaging;
    using ImageProcessor.Web.Caching;
    using ImageProcessor.Web.Config;
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
        /// The value to prefix any remote image requests with to ensure they get captured.
        /// </summary>
        private static readonly string RemotePrefix = ImageProcessorConfig.Instance.RemotePrefix;

        /// <summary>
        /// The assembly version.
        /// </summary>
        private static readonly string AssemblyVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();

        /// <summary>
        /// The value that acts as a basis to check that the startup code has only been ran once.
        /// </summary>
        private static int initCheck;

        /// <summary>
        /// A value indicating whether the application has started.
        /// </summary>
        private readonly bool hasModuleInitialized = initCheck == 1;
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
            if (!this.hasModuleInitialized)
            {
                Interlocked.CompareExchange(ref initCheck, 1, 0);
                DiskCache.CreateDirectories();
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
            // Nothing to dispose.
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
        private async Task ProcessImageAsync(HttpContext context)
        {
            HttpRequest request = context.Request;

            // Fixes issue 10.
            bool isRemote = request.Path.EndsWith(RemotePrefix, StringComparison.OrdinalIgnoreCase);
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
                        x => x.ExtensionLess == true && requestPath.StartsWith(x.Url.AbsoluteUri));

                    if (validExtensionLessUrl)
                    {
                        extensionLessExtension = RemoteFile.RemoteFileWhiteListExtensions.First(
                        x => x.ExtensionLess == true && requestPath.StartsWith(x.Url.AbsoluteUri)).ImageFormat;

                    }
                }
            }
            else
            {
                requestPath = HostingEnvironment.MapPath(request.Path);
                queryString = HttpUtility.UrlDecode(request.QueryString.ToString());
            }

            // Only process requests that pass our sanitizing filter.
            if ((ImageUtils.IsValidImageExtension(requestPath) || validExtensionLessUrl ) && !string.IsNullOrWhiteSpace(queryString))
            {
                string fullPath = string.Format("{0}?{1}", requestPath, queryString);
                string imageName = Path.GetFileName(requestPath);

                if (validExtensionLessUrl && !string.IsNullOrWhiteSpace(extensionLessExtension))
                {
                    fullPath = requestPath;

                    if (!string.IsNullOrWhiteSpace(urlParameters))
                    {
                        //TODO: Add hash for querystring parameters    
                        HashAlgorithm algorithm = MD5.Create();  // SHA1.Create()
                        var hashCode = algorithm.ComputeHash(Encoding.UTF8.GetBytes(urlParameters));
                        StringBuilder sb = new StringBuilder();
                        foreach (byte b in hashCode)
                            sb.Append(b.ToString("X2"));
                        imageName += sb.ToString();
                        fullPath += sb.ToString();
                    }

                    imageName += "." + extensionLessExtension;
                    fullPath += extensionLessExtension + "?" + queryString;
                }

                // Create a new cache to help process and cache the request.
                DiskCache cache = new DiskCache(request, requestPath, fullPath, imageName, isRemote);

                // Since we are now rewriting the path we need to check again that the current user has access
                // to the rewritten path.
                // Get the user for the current request
                // If the user is anonymous or authentication doesn't work for this suffix avoid a NullReferenceException 
                // in the UrlAuthorizationModule by creating a generic identity.
                string virtualCachedPath = cache.GetVirtualCachedPath();

                IPrincipal user = context.User
                                  ?? new GenericPrincipal(new GenericIdentity(string.Empty, string.Empty), new string[0]);

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
                        // Process the image.
                        using (ImageFactory imageFactory = new ImageFactory())
                        {
                            if (isRemote)
                            {
                                Uri uri = new Uri(requestPath + "?" + urlParameters);

                                RemoteFile remoteFile = new RemoteFile(uri, false);

                                // Prevent response blocking.
                                WebResponse webResponse = await remoteFile.GetWebResponseAsync().ConfigureAwait(false);

                                using (MemoryStream memoryStream = new MemoryStream())
                                {
                                    using (WebResponse response = webResponse)
                                    {
                                        using (Stream responseStream = response.GetResponseStream())
                                        {
                                            if (responseStream != null)
                                            {
                                                // Trim the cache.
                                                await cache.TrimCachedFoldersAsync();

                                                responseStream.CopyTo(memoryStream);

                                                imageFactory.Load(memoryStream)
                                                    .AddQueryString(queryString)
                                                    .Format(ImageUtils.GetImageFormat(imageName))
                                                    .AutoProcess().Save(cache.CachedPath);

                                                // Ensure that the LastWriteTime property of the source and cached file match.
                                                DateTime dateTime = await cache.SetCachedLastWriteTimeAsync();

                                                // Add to the cache.
                                                await cache.AddImageToCacheAsync(dateTime);
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                // Trim the cache.
                                await cache.TrimCachedFoldersAsync();

                                imageFactory.Load(fullPath).AutoProcess().Save(cache.CachedPath);

                                // Ensure that the LastWriteTime property of the source and cached file match.
                                DateTime dateTime = await cache.SetCachedLastWriteTimeAsync();

                                // Add to the cache.
                                await cache.AddImageToCacheAsync(dateTime);
                            }
                        }
                    }

                    // Store the response type in the context for later retrieval.
                    context.Items[CachedResponseTypeKey] = ImageUtils.GetResponseType(fullPath).ToDescription();
                    string incomingEtag = context.Request.Headers["If-None-Match"];

                    if (incomingEtag != null && !isNewOrUpdated)
                    {
                        // Explicitly set the Content-Length header so the client doesn't wait for
                        // content but keeps the connection open for other requests
                        context.Response.AddHeader("Content-Length", "0");
                        context.Response.StatusCode = (int)HttpStatusCode.NotModified;
                        context.Response.SuppressContent = true;

                        this.SetHeaders(context, (string)context.Items[CachedResponseTypeKey]);

                        if (!isRemote)
                        {
                            return;
                        }
                    }

                    // The cached file is valid so just rewrite the path.
                    context.RewritePath(cache.GetVirtualCachedPath(), false);
                }
                else
                {
                    throw new HttpException(403, "Access denied");
                }
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

            int maxDays = DiskCache.MaxFileCachedDuration;

            cache.SetExpires(DateTime.Now.ToUniversalTime().AddDays(maxDays));
            cache.SetMaxAge(new TimeSpan(maxDays, 0, 0, 0));
            cache.SetRevalidation(HttpCacheRevalidation.AllCaches);
        }
        #endregion
    }
}