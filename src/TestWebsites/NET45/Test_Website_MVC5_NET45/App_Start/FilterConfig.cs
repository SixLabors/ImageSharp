using System.Web;
using System.Web.Mvc;

namespace Test_Website_MVC5_NET45
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
        }
    }
}
