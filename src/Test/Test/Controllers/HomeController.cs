using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Test.Controllers
{
    using System.IO;
    using System.Threading.Tasks;
    using System.Web.Hosting;

    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            ViewBag.Message = "Welcome to ASP.NET MVC!";

            return View();
        }

        public ActionResult About()
        {
            List<string> images = new List<string>();

            const string Path = "/images/";
            string folder = HostingEnvironment.MapPath(Path);
            if (folder != null)
            {
                DirectoryInfo directoryInfo = new DirectoryInfo(folder);

                if (directoryInfo.Exists)
                {
                    // Get all the files in the cache ordered by LastAccessTime - oldest first.
                    List<FileInfo> fileInfos = directoryInfo.EnumerateFiles("*", SearchOption.AllDirectories).OrderBy(x => x.LastAccessTime).ToList();

                    int counter = fileInfos.Count;

                    Parallel.ForEach(
                        fileInfos,
                        fileInfo => images.Add(Path + fileInfo.Name));
                }
            }

            return View(images);
        }
    }
}
