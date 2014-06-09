
namespace ImageProcessor.Web.Processors
{
    using System;
    using System.Text.RegularExpressions;
    using ImageProcessor.Processors;

    /// <summary>
    /// Encapsulates methods to add rounded corners to an image.
    /// </summary>
    public class RoundedCorners : IWebGraphicsProcessor
    {
        public Regex RegexPattern { get; private set; }

        public int SortOrder { get; private set; }

        public IGraphicsProcessor Processor { get; private set; }

        public int MatchRegexIndex(string queryString)
        {
            throw new NotImplementedException();
        }
    }
}
