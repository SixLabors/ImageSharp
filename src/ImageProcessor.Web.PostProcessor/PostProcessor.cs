// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PostProcessor.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   The image postprocessor.
//   Many thanks to Azure Image Optimizer <see href="https://github.com/ligershark/AzureJobs"/>
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Web.PostProcessor
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Threading.Tasks;

    /// <summary>
    /// The image postprocessor.
    /// Many thanks to Azure Image Optimizer <see href="https://github.com/ligershark/AzureJobs"/>
    /// </summary>
    internal static class PostProcessor
    {
        public async static Task PostProcessImage(string sourceFile)
        {
            string targetFile = Path.GetTempFileName();
            PostProcessingResultEventArgs result = await RunProcessAsync(sourceFile, targetFile);

            if (result != null && result.Saving > 0 && result.ResultFileSize > 0)
            {
                File.Copy(result.ResultFileName, result.OriginalFileName, true);
                File.Delete(result.ResultFileName);
            }
            else
            {
                File.Delete(targetFile);
            }
        }

        private static Task<PostProcessingResultEventArgs> RunProcessAsync(string sourceFile, string targetFile)
        {
            TaskCompletionSource<PostProcessingResultEventArgs> tcs = new TaskCompletionSource<PostProcessingResultEventArgs>();
            ProcessStartInfo start = new ProcessStartInfo("cmd")
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = PostProcessorBootstrapper.WorkingPath,
                Arguments = GetArguments(sourceFile, targetFile),
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            if (string.IsNullOrWhiteSpace(start.Arguments))
            {
                tcs.SetResult(null);
                return tcs.Task;
            }

            Process process = new Process
            {
                StartInfo = start,
                EnableRaisingEvents = true
            };

            process.Exited += (sender, args) =>
            {
                tcs.SetResult(new PostProcessingResultEventArgs(sourceFile, targetFile));
                process.Dispose();
            };

            process.Start();

            return tcs.Task;
        }

        private static string GetArguments(string sourceFile, string targetFile)
        {
            if (!Uri.IsWellFormedUriString(sourceFile, UriKind.RelativeOrAbsolute) && !File.Exists(sourceFile))
            {
                return null;
            }

            string ext;

            string extension = Path.GetExtension(sourceFile);
            if (extension != null)
            {
                ext = extension.ToLowerInvariant();
            }
            else
            {
                return null;
            }

            switch (ext)
            {
                case ".png":
                    return string.Format(CultureInfo.CurrentCulture, "/c png.cmd \"{0}\" \"{1}\"", sourceFile, targetFile);

                case ".jpg":
                case ".jpeg":
                    return string.Format(CultureInfo.CurrentCulture, "/c jpegtran -copy all -optimize -progressive \"{0}\" \"{1}\"", sourceFile, targetFile);

                case ".gif":
                    return string.Format(CultureInfo.CurrentCulture, "/c gifsicle --crop-transparency --no-comments --no-extensions --no-names --optimize=3 --batch \"{0}\" --output=\"{1}\"", sourceFile, targetFile);
            }
            return null;
        }
    }
}
