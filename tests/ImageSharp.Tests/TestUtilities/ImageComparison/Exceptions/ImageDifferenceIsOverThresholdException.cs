// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison
{
    public class ImageDifferenceIsOverThresholdException : ImagesSimilarityException
    {
        public ImageSimilarityReport[] Reports { get; }

        public ImageDifferenceIsOverThresholdException(IEnumerable<ImageSimilarityReport> reports)
            : base("Image difference is over threshold!" + StringifyReports(reports))
        {
            this.Reports = reports.ToArray();
        }

        private static string StringifyReports(IEnumerable<ImageSimilarityReport> reports)
        {
            var sb = new StringBuilder();

            sb.Append(Environment.NewLine);

            sb.AppendFormat("Test Environment OS : {0}", GetEnvironmentName());
            sb.Append(Environment.NewLine);

            sb.AppendFormat("Test Environment is CI : {0}", TestEnvironment.RunsOnCI);
            sb.Append(Environment.NewLine);

            sb.AppendFormat("Test Environment is .NET Core : {0}", !TestEnvironment.IsFramework);
            sb.Append(Environment.NewLine);
            
            sb.AppendFormat("Test Environment is Mono : {0}", TestEnvironment.IsMono);
            sb.Append(Environment.NewLine);

            int i = 0;
            foreach (ImageSimilarityReport r in reports)
            {
                sb.Append("Report ImageFrame {i}: ");
                sb.Append(r);
                sb.Append(Environment.NewLine);
                i++;
            }

            return sb.ToString();
        }

        private static string GetEnvironmentName()
        {
            if (TestEnvironment.IsMacOS)
            {
                return "Mac OS";
            }

            if (TestEnvironment.IsMacOS)
            {
                return "Linux";
            }

            if (TestEnvironment.IsWindows)
            {
                return "Windows";
            }

            return "Unknown";
        }
    }
}
