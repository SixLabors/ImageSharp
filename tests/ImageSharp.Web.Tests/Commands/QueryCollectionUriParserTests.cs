namespace ImageSharp.Web.Tests.Commands
{
    using System;
    using System.Collections.Generic;

    using ImageSharp.Web.Commands;

    using Microsoft.AspNetCore.Http;

    using Xunit;

    public class QueryCollectionUriParserTests
    {
        [Fact]
        public void QueryCollectionParserExtractsCommands()
        {
            IDictionary<string, string> expected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                {"width", "400" },
                {"height", "200" }
            };

            HttpContext context = TestHelpers.CreateHttpContext();
            IDictionary<string, string> actual = new QueryCollectionUriParser().ParseUriCommands(context);

            Assert.Equal(expected, actual);
        }
    }
}