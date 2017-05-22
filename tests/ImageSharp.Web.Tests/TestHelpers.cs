namespace ImageSharp.Web.Tests
{
    using Microsoft.AspNetCore.Http;

    public class TestHelpers
    {
        public static HttpContext CreateHttpContext()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Path = "/testwebsite.com/image-12345.jpeg";
            httpContext.Request.QueryString = new QueryString("?width=400&height=200");
            return httpContext;
        }

        public static HttpContext CreateHttpContext(string path, string query)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Path = path;
            httpContext.Request.QueryString = new QueryString(query);
            return httpContext;
        }
    }
}
