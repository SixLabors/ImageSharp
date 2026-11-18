// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Globalization;
using System.Text;

namespace SixLabors.ImageSharp.Comparison.Exceptions;

/// <summary>
/// The exception that is thrown when the difference of a compared image or frame is over the
/// threshold of the comparer.
/// </summary>
public class ImageDifferenceIsOverThresholdException : ImagesSimilarityException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ImageDifferenceIsOverThresholdException"/> class.
    /// </summary>
    /// <param name="reports">The reports of the frames which are over the threshold.</param>
    public ImageDifferenceIsOverThresholdException(IReadOnlyList<ImageSimilarityReport> reports)
        : base("Image difference is over threshold!" + StringifyReports(reports))
        => this.Reports = reports;

    /// <summary>
    /// Gets the reports of the frames which are over the threshold.
    /// </summary>
    public IReadOnlyList<ImageSimilarityReport> Reports { get; }

    private static string StringifyReports(IReadOnlyList<ImageSimilarityReport> reports)
    {
        StringBuilder sb = new();
        sb.Append(Environment.NewLine);

        foreach (ImageSimilarityReport report in reports)
        {
            sb.AppendFormat(CultureInfo.InvariantCulture, "Report ImageFrame {0}: ", report.Index)
              .Append(report)
              .Append(Environment.NewLine);
        }

        return sb.ToString();
    }
}
