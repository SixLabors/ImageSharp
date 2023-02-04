// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Globalization;
using System.Text;

namespace SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

public class ImageDifferenceIsOverThresholdException : ImagesSimilarityException
{
    public ImageSimilarityReport[] Reports { get; }

    public ImageDifferenceIsOverThresholdException(IEnumerable<ImageSimilarityReport> reports)
        : base("Image difference is over threshold!" + StringifyReports(reports))
        => this.Reports = reports.ToArray();

    private static string StringifyReports(IEnumerable<ImageSimilarityReport> reports)
    {
        StringBuilder sb = new();

        sb.Append(Environment.NewLine);
        sb.AppendFormat(CultureInfo.InvariantCulture, "Test Environment OS : {0}", GetEnvironmentName());
        sb.Append(Environment.NewLine);

        sb.AppendFormat(CultureInfo.InvariantCulture, "Test Environment is CI : {0}", TestEnvironment.RunsOnCI);
        sb.Append(Environment.NewLine);

        sb.AppendFormat(CultureInfo.InvariantCulture, "Test Environment is .NET Core : {0}", !TestEnvironment.IsFramework);
        sb.Append(Environment.NewLine);

        sb.AppendFormat(CultureInfo.InvariantCulture, "Test Environment is Mono : {0}", TestEnvironment.IsMono);
        sb.Append(Environment.NewLine);

        sb.AppendFormat(CultureInfo.InvariantCulture, "Test Environment OS Architecture : {0}", TestEnvironment.OSArchitecture);
        sb.Append(Environment.NewLine);

        sb.AppendFormat(CultureInfo.InvariantCulture, "Test Environment Process Architecture : {0}", TestEnvironment.ProcessArchitecture);
        sb.Append(Environment.NewLine);

        foreach (ImageSimilarityReport r in reports)
        {
            sb.AppendFormat(CultureInfo.InvariantCulture, "Report ImageFrame {0}: ", r.Index)
              .Append(r)
              .Append(Environment.NewLine);
        }

        return sb.ToString();
    }

    private static string GetEnvironmentName()
    {
        if (TestEnvironment.IsMacOS)
        {
            return "MacOS";
        }

        if (TestEnvironment.IsLinux)
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
