using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Routing;

namespace Test_Website_NET45
{
    using System.Diagnostics;
    using System.Threading.Tasks;
    using System.Web.UI.WebControls;

    using ImageProcessor.Web.Helpers;
    using ImageProcessor.Web.HttpModules;

    // Note: For instructions on enabling IIS6 or IIS7 classic mode, 
    // visit http://go.microsoft.com/?LinkId=9394801
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();

            WebApiConfig.Register(GlobalConfiguration.Configuration);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);

            // Test the post processing event.
            //ImageProcessingModule.OnPostProcessing += (sender, args) => Debug.WriteLine(args.CachedImagePath);

            //ImageProcessingModule.OnProcessQuerystring += (sender, args) =>
            //    {
            //        if (!args.RawUrl.Contains("penguins"))
            //        {
            //            return args.Querystring += "watermark=protected&color=fff&fontsize=36&fontopacity=70textshadow=true&fontfamily=arial";
            //        }

            //        return args.Querystring;
            //    };
        }

        private async void WritePath(object sender, PostProcessingEventArgs e)
        {
            await Task.Run(() => Debug.WriteLine(e.CachedImagePath));
        }
    }
}