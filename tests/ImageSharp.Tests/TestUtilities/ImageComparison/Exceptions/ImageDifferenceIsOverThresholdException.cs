// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Text;

namespace SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

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

        foreach (ImageSimilarityReport r in reports)
        {
            sb.AppendFormat("Report ImageFrame {0}: ", r.Index);
            sb.Append(r);
            sb.Append(Environment.NewLine);
        }

        return sb.ToString();
    }

    private static string GetEnvironmentName()
    {
        if (TestEnvironment.IsMacOS)
        {
            return "MacOS";
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
