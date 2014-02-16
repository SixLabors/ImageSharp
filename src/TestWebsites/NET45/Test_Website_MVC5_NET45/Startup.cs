using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(Test_Website_MVC5_NET45.Startup))]
namespace Test_Website_MVC5_NET45
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}
