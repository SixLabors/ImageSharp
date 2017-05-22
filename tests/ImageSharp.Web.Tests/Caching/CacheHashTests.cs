namespace ImageSharp.Web.Tests.Caching
{
    using System.IO;

    using ImageSharp.Web.Caching;

    using Xunit;

    public class CacheHashTests
    {
        [Fact]
        public void CacheHashEncodesExtensionCorrectly()
        {
            // Expected extension should match the default extension of the installed format
            string input = "http://testwebsite.com/image-12345.jpeg?width=400";
            string expected = ".jpeg";
            string actual = CacheHash.Create(input, Configuration.Default);

            Assert.Equal(expected, Path.GetExtension(actual));

            string input2 = "http://testwebsite.com/image-12345.jpeg?width=400&format=png";
            string expected2 = ".png";
            string actual2 = CacheHash.Create(input2, Configuration.Default);

            Assert.Equal(expected2, Path.GetExtension(actual2));
        }

        [Fact]
        public void CachHashProducesIdenticalResults()
        {
            string input = "http://testwebsite.com/image-12345.jpeg?width=400";
            string expected = CacheHash.Create(input, Configuration.Default);
            string actual = CacheHash.Create(input, Configuration.Default);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void CachHashLengthIsIdentical()
        {
            string input = "http://testwebsite.com/image-12345.jpeg?width=400";
            string input2 = "http://testwebsite.com/image-12345.jpeg";
            int expected = CacheHash.Create(input, Configuration.Default).Length;
            int actual = CacheHash.Create(input2, Configuration.Default).Length;

            Assert.Equal(expected, actual);
        }
    }
}