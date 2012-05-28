// -----------------------------------------------------------------------
// <copyright file="RemoteFile.cs" company="James South">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

namespace ImageProcessor.Web.Helpers
{
    #region Using
    using System;
    using System.Diagnostics.Contracts;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Security;
    using System.Text;
    using ImageProcessor.Web.Config;
    #endregion

    /// <summary>
    /// Encapsulates methods used to download files from a website address.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The purpose of this class is so there's one core way of downloading remote files with urls that are from
    /// outside users. There's various areas in application where an attacker could supply an external url to the server
    /// and tie up resources.
    /// </para>
    /// For example, the ImageProcessingModule accepts off-server addresses as a path. An attacker could, for instance, pass the url
    /// to a file that's a few gigs in size, causing the server to get out-of-memory exceptions or some other errors. An attacker
    /// could also use this same method to use one application instance to hammer another site by, again, passing an off-server
    /// address of the victims site to the ImageProcessingModule. 
    /// This class will not throw an exception if the Uri supplied points to a resource local to the running application instance.
    /// <para>
    /// There shouldn't be any security issues there, as the internal WebRequest instance is still calling it remotely. 
    /// Any local files that shouldn't be accessed by this won't be allowed by the remote call.
    /// </para>
    /// Adapted from <see cref="http://blogengine.codeplex.com">BlogEngine.Net</see>
    /// </remarks>
    internal sealed class RemoteFile
    {
        #region Fields
        /// <summary>
        /// The white-list of urls from which to download remote files.
        /// </summary>
        private static readonly Uri[] RemoteFileWhiteList = ImageProcessorConfig.Instance.RemoteFileWhiteList;

        /// <summary>
        /// The length of time, in milliseconds, that a remote file download attempt can last before timing out.
        /// </summary>
        private static readonly int TimeoutMilliseconds = ImageProcessorConfig.Instance.Timeout;

        /// <summary>
        /// The maximum size, in bytes, that a remote file download attempt can download.
        /// </summary>
        private static readonly int MaxBytes = ImageProcessorConfig.Instance.MaxBytes;

        /// <summary>
        /// Whether to allow remote downloads.
        /// </summary>
        private static readonly bool AllowRemoteDownloads = ImageProcessorConfig.Instance.AllowRemoteDownloads;

        /// <summary>
        /// Whether this RemoteFile instance is ignoring remote download rules set in the current application 
        /// instance.
        /// </summary>
        private readonly bool ignoreRemoteDownloadSettings;

        /// <summary>
        /// The <see cref="T:System.Uri">Uri</see> of the remote file being downloaded.
        /// </summary>
        private readonly Uri url;

        /// <summary>
        /// The maximum allowable download size in bytes.
        /// </summary>
        private readonly int maxDownloadSize;

        /// <summary>
        /// The length of time, in milliseconds, that a remote file download attempt can last before timing out.
        /// </summary>
        private int timeoutLength;

        /// <summary>
        /// The <see cref="T:System.Net.WebResponse">WebResponse</see> object used internally for this RemoteFile instance.
        /// </summary>
        private WebRequest webRequest;
        #endregion

        #region Constructors
        /// <summary>
        /// Initializes a new instance of the <see cref="T:ImageProcessor.Web.Helpers.RemoteFile">RemoteFile</see> class. 
        /// </summary>
        /// <param name="filePath">The url of the file to be downloaded.</param>
        /// <param name="ignoreRemoteDownloadSettings">
        /// If set to <see langword="true"/>, then RemoteFile should ignore the current the applications instance's remote download settings; otherwise,<see langword="false"/>.
        /// </param>
        internal RemoteFile(Uri filePath, bool ignoreRemoteDownloadSettings)
        {
            Contract.Requires(filePath != null);

            this.url = filePath;
            this.ignoreRemoteDownloadSettings = ignoreRemoteDownloadSettings;
            this.timeoutLength = TimeoutMilliseconds;
            this.maxDownloadSize = MaxBytes;
        }
        #endregion

        #region Properties
        /// <summary>
        /// Gets a value indicating whether this RemoteFile instance is ignoring remote download rules set in the
        /// current application instance.
        /// <remarks>
        /// This should only be set to true if the supplied url is a verified resource. Use at your own risk.
        /// </remarks>
        /// </summary>
        /// <value>
        /// <see langword="true"/> if this RemoteFile instance is ignoring remote download rules set in the current 
        /// application instance; otherwise, <see langword="false"/>.
        /// </value>
        public bool IgnoreRemoteDownloadSettings
        {
            get
            {
                return this.ignoreRemoteDownloadSettings;
            }
        }

        /// <summary>
        /// Gets the Uri of the remote file being downloaded.
        /// </summary>
        public Uri Uri
        {
            get
            {
                return this.url;
            }
        }

        /// <summary>
        /// Gets or sets the length of time, in milliseconds, that a remote file download attempt can 
        /// last before timing out.
        /// <remarks>
        /// <para>
        /// This value can only be set if the instance is supposed to ignore the remote download settings set
        /// in the current application instance. 
        /// </para>
        /// <para>
        /// Set this value to 0 if there should be no timeout.
        /// </para>
        /// </remarks>
        /// </summary>
        public int TimeoutLength
        {
            get
            {
                return this.IgnoreRemoteDownloadSettings ? this.timeoutLength : TimeoutMilliseconds;
            }

            set
            {
                if (!this.IgnoreRemoteDownloadSettings)
                {
                    throw new SecurityException("Timeout length can not be adjusted on remote files that are abiding by remote download rules");
                }

                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException("TimeoutLength");
                }

                this.timeoutLength = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum download size, in bytes, that a remote file download attempt can be.
        /// <remarks>
        /// <para>
        /// This value can only be set if the instance is supposed to ignore the remote download settings set
        /// in the current application instance. 
        /// </para>
        /// <para>
        /// Set this value to 0 if there should be no timeout.
        /// </para>
        /// </remarks>
        /// </summary>
        public int MaxDownloadSize
        {
            get
            {
                return this.IgnoreRemoteDownloadSettings ? this.maxDownloadSize : MaxBytes;
            }

            set
            {
                if (!this.IgnoreRemoteDownloadSettings)
                {
                    throw new SecurityException("Max Download Size can not be adjusted on remote files that are abiding by remote download rules");
                }

                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException("MaxDownloadSize");
                }

                this.timeoutLength = value;
            }
        }
        #endregion

        #region Methods
        #region Public
        /// <summary>
        /// Returns the <see cref="T:System.Net.WebResponse">WebResponse</see> used to download this file.
        /// <remarks>
        /// <para>
        /// This method is meant for outside users who need specific access to the WebResponse this class
        /// generates. They're responsible for disposing of it.
        /// </para>
        /// </remarks>
        /// </summary>
        /// <returns>The <see cref="T:System.Net.WebResponse">WebResponse</see> used to download this file.</returns>
        public WebResponse GetWebResponse()
        {
            WebResponse response = this.GetWebRequest().GetResponse();

            long contentLength = response.ContentLength;

            // WebResponse.ContentLength doesn't always know the value, it returns -1 in this case.
            if (contentLength == -1)
            {
                // Response headers may still have the Content-Length inside of it.
                string headerContentLength = response.Headers["Content-Length"];

                if (!string.IsNullOrWhiteSpace(headerContentLength))
                {
                    contentLength = long.Parse(headerContentLength, CultureInfo.InvariantCulture);
                }
            }

            // We don't need to check the url here since any external urls are available only from the web.config.
            if ((this.MaxDownloadSize > 0) && (contentLength > this.MaxDownloadSize))
            {
                response.Close();
                throw new SecurityException("An attempt to download a remote file has been halted because the file is larger than allowed.");
            }

            return response;
        }

        /// <summary>
        /// Returns the remote file as a String.       
        /// <remarks>
        /// This returns the resulting stream as a string as passed through a StreamReader.
        /// </remarks>
        /// </summary>
        /// <returns>The remote file as a String.</returns>
        public string GetFileAsString()
        {
            using (WebResponse response = this.GetWebResponse())
            {
                Stream responseStream = response.GetResponseStream();

                if (responseStream != null)
                {
                    // Pipe the stream to a stream reader with the required encoding format.
                    using (StreamReader reader = new StreamReader(responseStream, Encoding.UTF8))
                    {
                        return reader.ReadToEnd();
                    }
                }

                return string.Empty;
            }
        }
        #endregion

        #region Private
        /// <summary>
        /// Performs a check to see whether the application is able to download remote files.
        /// </summary>
        private void CheckCanDownload()
        {
            if (!this.IgnoreRemoteDownloadSettings && !AllowRemoteDownloads)
            {
                throw new SecurityException("application is not configured to allow remote file downloads.");
            }
        }

        /// <summary>
        /// Creates the WebRequest object used internally for this RemoteFile instance.
        /// </summary>
        /// <returns>
        /// <para>
        /// The WebRequest should not be passed outside of this instance, as it will allow tampering. Anyone
        /// that needs more fine control over the downloading process should probably be using the WebRequest
        /// class on its own.
        /// </para>
        /// </returns>
        private WebRequest GetWebRequest()
        {
            // Check downloads are allowed.
            this.CheckCanDownload();

            // Check the url is from a whitelisted location.
            this.CheckSafeUrlLocation();

            if (this.webRequest == null)
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(this.Uri);
                request.Headers["Accept-Encoding"] = "gzip";
                request.Headers["Accept-Language"] = "en-us";
                request.Credentials = CredentialCache.DefaultNetworkCredentials;
                request.AutomaticDecompression = DecompressionMethods.GZip;

                if (this.TimeoutLength > 0)
                {
                    request.Timeout = this.TimeoutLength;
                }

                this.webRequest = request;
            }

            return this.webRequest;
        }

        /// <summary>
        /// Returns a value indicating whether the current url is in a list of safe download locations.
        /// </summary>
        private void CheckSafeUrlLocation()
        {
            bool validUrl = RemoteFileWhiteList.Any(item => item.Host.ToUpperInvariant().Equals(this.url.Host.ToUpperInvariant()));

            if (!validUrl)
            {
                throw new SecurityException("application is not configured to allow remote file downloads from this domain.");
            }
        }
        #endregion
        #endregion
    }
}


