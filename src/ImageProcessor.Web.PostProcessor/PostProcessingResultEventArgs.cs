// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PostProcessingResultEventArgs.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   The post processing result event arguments.
//   Many thanks to Azure Image Optimizer <see href="https://github.com/ligershark/AzureJobs"/>
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Web.PostProcessor
{
    using System;
    using System.IO;
    using System.Text;

    /// <summary>
    /// The post processing result event arguments.
    /// Many thanks to Azure Image Optimizer <see href="https://github.com/ligershark/AzureJobs"/>
    /// </summary>
    public class PostProcessingResultEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PostProcessingResultEventArgs"/> class.
        /// </summary>
        /// <param name="originalFileName">The original file name.</param>
        /// <param name="resultFileName">The result file name.</param>
        public PostProcessingResultEventArgs(string originalFileName, string resultFileName)
        {
            FileInfo original = new FileInfo(originalFileName);
            FileInfo result = new FileInfo(resultFileName);

            if (original.Exists)
            {
                this.OriginalFileName = original.FullName;
                this.OriginalFileSize = original.Length;
            }

            if (result.Exists)
            {
                this.ResultFileName = result.FullName;
                this.ResultFileSize = result.Length;
            }
        }

        /// <summary>
        /// Gets or sets the original file size in bytes.
        /// </summary>
        public long OriginalFileSize { get; set; }

        /// <summary>
        /// Gets or sets the original file name.
        /// </summary>
        public string OriginalFileName { get; set; }

        /// <summary>
        /// Gets or sets the result file size in bytes.
        /// </summary>
        public long ResultFileSize { get; set; }

        /// <summary>
        /// Gets or sets the result file name.
        /// </summary>
        public string ResultFileName { get; set; }

        /// <summary>
        /// Gets the difference in file size in bytes.
        /// </summary>
        public long Saving
        {
            get { return this.OriginalFileSize - this.ResultFileSize; }
        }

        /// <summary>
        /// Gets the difference in file size as a percentage.
        /// </summary>
        public double Percent
        {
            get
            {
                return Math.Round(100 - ((this.ResultFileSize / (double)this.OriginalFileSize) * 100), 1);
            }
        }

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>
        /// A string that represents the current object.
        /// </returns>
        public override string ToString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("Optimized " + Path.GetFileName(this.OriginalFileName));
            stringBuilder.AppendLine("Before: " + this.OriginalFileSize + " bytes");
            stringBuilder.AppendLine("After: " + this.ResultFileSize + " bytes");
            stringBuilder.AppendLine("Saving: " + this.Saving + " bytes / " + this.Percent + "%");

            return stringBuilder.ToString();
        }
    }
}