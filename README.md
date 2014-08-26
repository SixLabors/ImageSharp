ImageProcessor
===============

[![Build status](https://ci.appveyor.com/api/projects/status/8ypr7527dnao04yr)](https://ci.appveyor.com/project/JamesSouth/imageprocessor)

Imageprocessor is a lightweight library written in C# that allows you to manipulate images on-the-fly using .NET 4.5+

It's fast, extensible, easy to use, comes bundled with some great features and is fully open source.

For full documentation please see [http://imageprocessor.org/](http://imageprocessor.org/)

##Contributing to ImageProcessor
Contribution is most welcome! I mean, that's what this is all about isn't it?

Things I would :heart: people to help me out with:

 - Unit tests. I need to get some going for all the regular expressions within the ImageProcessor core plus anywhere else we can think of. If that's your bag please contribute.
 - Documentation. Nobody likes doing docs, I've written a lot but my prose is clumsy and verbose. If you think you can make things clearer or you spot any mistakes please submit a pull request (Guide to how the docs work below).
 - Async. I'd love someone with real async chops to have a look at the ImageProcessor.Web. It works and it works damn well but I'm no expert on threading so I'm sure someone can improve on it. 

**Just remember to StyleCop all things! :oncoming_police_car:**

##RoadMap
I want the next version of ImageProcessor to run on all devices. Sadly it looks like using `System.Drawing` will not allow me to do that so I need to have a look at using the classes within [System.Windows.Media.Imaging](http://msdn.microsoft.com/en-us/library/System.Windows.Media.Imaging(v=vs.110).aspx) This is a **LOT** of work so any help that could be offered would be greatly appreciated.

##Documentation

ImageProcessor's documentation, included in this repo in the gh_pages directory, is built with [Jekyll](http://jekyllrb.com) and publicly hosted on GitHub Pages at <http://imageprocessor.org>. The docs may also be run locally.

### Running documentation locally
1. If necessary, [install Jekyll](http://jekyllrb.com/docs/installation) (requires v2.2.x).
  - **Windows users:** Read [this unofficial guide](https://github.com/juthilo/run-jekyll-on-windows/) to get Jekyll up and running without problems. 
2. From the root `/ImageProcessor` directory, run `jekyll serve` in the command line.
3. Open <http://localhost:4000> in your browser to navigate to your site.
Learn more about using Jekyll by reading its [documentation](http://jekyllrb.com/docs/home/).
