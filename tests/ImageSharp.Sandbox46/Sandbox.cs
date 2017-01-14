using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageSharp
{
    using Xunit;

    public class Sandbox
    {
        [Fact]
        public void HelloImage()
        {
            Image img = new Image(10, 20);
            Assert.Equal(10, img.Width);
        }
    }
}
