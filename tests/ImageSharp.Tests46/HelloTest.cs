using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageSharp.Tests
{
    using Xunit;
    using Xunit.Abstractions;

    public class HelloTest
    {
        private ITestOutputHelper output;

        public HelloTest(ITestOutputHelper output)
        {
            this.output = output;
        }
        
        [Fact]
        public void HelloFoo()
        {
            TestFile file = TestFile.Create(TestImages.Jpeg.Calliphora);
            var img = file.CreateImage();
            this.output.WriteLine(img.Width.ToString());
        }
    }
}
