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
    using System.Threading.Tasks;
    using System.Web;
    using System.Web.Hosting;
    using System.Web.Security;

    using ImageProcessor.Web.Caching;
    using ImageProcessor.Web.Configuration;
    using ImageProcessor.Web.Extensions;
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
        /// The key for storing the file dependency of the current image.
        /// </summary>
        private const string CachedResponseFileDependency = "CACHED_IMAGE_DEPENDENCY_054F217C-11CF-49FF-8D2F-698E8E6EB58F";

        /// <summary>
        /// The regular expression to search strings for.
        /// </summary>
        private static readonly Regex PresetRegex = new Regex(@"preset=[^&]+", RegexOptions.Compiled);

        /// <summary>
        /// The assembly version.
        /// </summary>
        private static readonly string AssemblyVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();

        /// <summary>
        /// The locker for preventing duplicate requests.
        /// </summary>
        private static readonly AsyncDeDuperLock Locker = new AsyncDeDuperLock();

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

#if NET45 && !__MonoCS__
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
            }

            // Call the appropriate methods to clean up
            // unmanaged resources here.
            // Note disposing is done.
            this.isDisposed = true;
        }
        #endregion

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

        /// <summary>
        /// Occurs just before ASP.NET send HttpHeaders to the client.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="T:System.EventArgs">EventArgs</see> that contains the event data.</param>
        private void ContextPreSendRequestHeaders(object sender, EventArgs e)
        {
            HttpContext context = ((HttpApplication)sender).Context;

            object responseTypeObject = context.Items[CachedResponseTypeKey];
            object dependencyFileObject = context.Items[CachedResponseFileDependency];

            if (responseTypeObject != null && dependencyFileObject != null)
            {
                string responseType = (string)responseTypeObject;
                List<string> dependencyFiles = (List<string>)dependencyFileObject;

                // Set the headers
                this.SetHeaders(context, responseType, dependencyFiles);

                context.Items[CachedResponseTypeKey] = null;
                context.Items[CachedResponseFileDependency] = null;
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
                DiskCache cache = new DiskCache(requestPath, fullPath, imageName);
                string cachedPath = cache.CachedPath;

                // Since we are now rewriting the path we need to check again that the current user has access
                // to the rewritten path.
                // Get the user for the current request
                // If the user is anonymous or authentication doesn't work for this suffix avoid a NullReferenceException 
                // in the UrlAuthorizationModule by creating a generic identity.
                string virtualCachedPath = cache.VirtualCachedPath;

                IPrincipal user = context.User ?? new GenericPrincipal(new GenericIdentity(string.Empty, string.Empty), new string[0]);

                // Do we have permission to call UrlAuthorizationModule.CheckUrlAccessForPrincipal?
                PermissionSet permission = new PermissionSet(PermissionState.None);
                permission.AddPermission(new AspNetHostingPermission(AspNetHostingPermissionLevel.Unrestricted));
                bool hasPermission = permission.IsSubsetOf(AppDomain.CurrentDomain.PermissionSet);

                bool isAllowed = true;

                // Run the rewritten path past the authorization system again.
                // We can then use the result as the default "AllowAccess" value
                if (hasPermission && !context.SkipAuthorization)
                {
                    isAllowed = UrlAuthorizationModule.CheckUrlAccessForPrincipal(virtualCachedPath, user, "GET");
                }

                if (isAllowed)
                {
                    // Is the file new or updated?
                    bool isNewOrUpdated = await cache.IsNewOrUpdatedFileAsync(cachedPath);

                    // Only process if the file has been updated.
                    if (isNewOrUpdated)
                    {
                        // Process the image.
                        using (ImageFactory imageFactory = new ImageFactory(preserveExifMetaData != null && preserveExifMetaData.Value))
                        {
                            if (isRemote)
                            {
                                using (await Locker.LockAsync(cachedPath))
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
                                                    responseStream.CopyTo(memoryStream);

                                                    // Reset the position of the stream to ensure we're reading the correct part.
                                                    memoryStream.Position = 0;

                                                    // Process the Image
                                                    imageFactory.Load(memoryStream)
                                                                .AutoProcess(queryString)
                                                                .Save(cachedPath);

                                                    // Add to the cache.
                                                    cache.AddImageToCache(cachedPath);

                                                    // Trim the cache.
                                                    await cache.TrimCachedFolderAsync(cachedPath);

                                                    // Store the response type and cache dependency in the context for later retrieval.
                                                    context.Items[CachedResponseTypeKey] = imageFactory.CurrentImageFormat.MimeType;
                                                    context.Items[CachedResponseFileDependency] = new List<string> { cachedPath };
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                using (await Locker.LockAsync(cachedPath))
                                {
                                    // Check to see if the file exists.
                                    // ReSharper disable once AssignNullToNotNullAttribute
                                    FileInfo fileInfo = new FileInfo(requestPath);

                                    if (!fileInfo.Exists)
                                    {
                                        throw new HttpException(404, "No image exists at " + fullPath);
                                    }

                                    // Process the Image
                                    imageFactory.Load(requestPath)
                                                .AutoProcess(queryString)
                                                .Save(cachedPath);

                                    // Add to the cache.
                                    cache.AddImageToCache(cachedPath);

                                    // Trim the cache.
                                    await cache.TrimCachedFolderAsync(cachedPath);

                                    // Store the response type and cache dependencies in the context for later retrieval.
                                    context.Items[CachedResponseTypeKey] = imageFactory.CurrentImageFormat.MimeType;
                                    context.Items[CachedResponseFileDependency] = new List<string> { requestPath, cachedPath };
                                }
                            }
                        }
                    }

                    // Image is from the cache so the mime-type will need to be set.
                    if (context.Items[CachedResponseTypeKey] == null)
                    {
                        string mimetype = ImageHelpers.GetMimeType(cachedPath);

                        if (!string.IsNullOrEmpty(mimetype))
                        {
                            context.Items[CachedResponseTypeKey] = mimetype;
                        }
                    }

                    string incomingEtag = context.Request.Headers["If" + "-None-Match"];

                    if (incomingEtag != null && !isNewOrUpdated)
                    {
                        // Set the Content-Length header so the client doesn't wait for
                        // content but keeps the connection open for other requests.
                        context.Response.AddHeader("Content-Length", "0");
                        context.Response.StatusCode = (int)HttpStatusCode.NotModified;
                        context.Response.SuppressContent = true;

                        if (!isRemote)
                        {
                            // Set the headers and quit.
                            this.SetHeaders(context, (string)context.Items[CachedResponseTypeKey], new List<string> { requestPath, cachedPath });
                            return;
                        }

                        this.SetHeaders(context, (string)context.Items[CachedResponseTypeKey], new List<string> { cachedPath });
                    }

                    // The cached file is valid so just rewrite the path.
                    context.RewritePath(virtualCachedPath, false);
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
        /// <param name="responseType">
        /// The HTTP MIME type to to send.
        /// </param>
        /// <param name="dependencyPaths">
        /// The dependency path for the cache dependency.
        /// </param>
        private void SetHeaders(HttpContext context, string responseType, IEnumerable<string> dependencyPaths)
        {
            HttpResponse response = context.Response;

            response.ContentType = responseType;

            if (response.Headers["Image-Served-By"] == null)
            {
                response.AddHeader("Image-Served-By", "ImageProcessor.Web/" + AssemblyVersion);
            }

            HttpCachePolicy cache = response.Cache;
            cache.SetCacheability(HttpCacheability.Public);
            cache.VaryByHeaders["Accept-Encoding"] = true;

            context.Response.AddFileDependencies(dependencyPaths.ToArray());
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