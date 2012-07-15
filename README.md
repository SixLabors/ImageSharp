ImageProcessor
===============

ImageProcessor is a library for on the fly processing of image files using Asp.Net 4 written in c#.

The library architecture is highly extensible and allows for easy extension.

Core plugins at present include:

 - Resize 
 - Crop
 - Quality (The quality to set the output for jpeg files)
 - Filter (Image filters including sepia, greyscale, blackwhite, lomograph, comic)
 - Vignette (Adds a vignette effect to images)
 - Format (Sets the output format)
 - Alpha (Sets opacity)
 - Watermark (Set a text watermark)

The library consists of two binaries: **ImageProcessor.dll** and **ImageProcessor.Web.dll**.

**ImageProcessor.dll** contains all the core functionality that allows for image manipulation via the `ImageFactory` class. This has a fluent API which allows you to easily chain methods to deliver the desired output.

e.g.

    // Read a file and resize it.
    byte[] photoBytes = File.ReadAllBytes(file);
    int quality = 90;
    ImageFormat format = ImageFormat.Jpeg;
    int thumbnailSize = 150;
        
    using (var inStream = new MemoryStream(photoBytes))
    {
        using (var outStream = new MemoryStream())
        {
            using (ImageFactory imageFactory = new ImageFactory())
            {
                // Load, resize and save an image.
                imageFactory.Load(inStream).Format(format).Quality(quality).Resize(thumbnailSize, 0).Save(outStream);
            }
        }
    }

**ImageProcessor.Web.dll** contains a HttpModule which captures internal and external requests automagically processing them based on values captured through querystring parameters.

Using the HttpModule requires no code writing at all. Just reference the binaries and add the relevant sections to the web.config

Image requests suffixed with QueryString parameters will then be processed and cached to the server allowing for easy and efficient parsing of following requests.

The parsing engine for the HttpModule is incredibly flexible and will **allow you to add querystring parameters in any order.**

Installation
============

Installation is simple. Download the zip file from the downloads section and copy the two binaries into your bin folder. Then copy the example configuration values from the `config.txt` into your `web.config` to enable the processor. A Nuget package will be created once I've read the manual to allow simpler installation in the future.

Usage
=====

Heres a few examples of the syntax for processing images.

Resize
======

    <img src="/images.yourimage.jpg?width=200" alt="your resized image"/>

Will resize your image to 200px wide whilst keeping the correct aspect ratio.

Remote files can also be requested and cached by prefixing the src with a value set in the web.config 

e.g.

    <img src="remote.axd/http://url/images.yourimage.jpg?width=200" alt="your resized image"/>

Will resize your remote image to 200px wide whilst keeping the correct aspect ratio.

Changing both width and height requires the following syntax:

    <img src="/images.yourimage.jpg?resize=width-200|height-200" alt="your resized image"/>

Crop
====

Cropping an image is easy. Simply pass the top-left and bottom-right coordinates and the processor will work out the rest.

e.g.

    <img src="/images.yourimage.jpg?crop=5-5-200-200" alt="your cropped image"/>
    
Alpha Transparency
==================
Imageprocessor can adjust the alpha transparency of png images. Simply pass the desired percentage value (without the '%') to the processor.

e.g.

    <img src="/images.yourimage.jpg?alpha=60" alt="60% alpha image"/>
    
Filters
=======

Everybody loves adding filter effects to photgraphs so we've baked some popular ones into Imageprocessor.

Current filters include:

  - blackwhite
  - comic
  - lomograph
  - greyscale
  - polaroid
  - sepia
  - gotham
  - hisatch
  - losatch

e.g.

    <img src="/images.yourimage.jpg?filter=polaroid" alt="classic polaroid look"/>

Watermark
=========

Imageprocessor can add watermark text to your images with a wide range of options. Each option can be ommited and can be added in any order. Just ensure that they are pipe "|" separated:

  - text
  - size (px)
  - font (any installed font)
  - opacity (1-100)
  - style (bold|italic|regular|strikeout|underline)
  - shadow
  - position (x,y)

e.g.

    <img src="/images.yourimage.jpg?watermark=watermark=text-test text|color-fff|size-36|style-italic|opacity-80|position-30-150|shadow-true|font-arial" alt="watermark"/>
    
Internally it uses a class named `TextLayer` to add the watermark text.

Format
======

Imageprocessor will also allow you to change the format of image on-the-fly. This can be handy for reducing the size of requests.

Supported file format just now are:

  - jpg
  - bmp
  - png
  - gif

e.g.

    <img src="/images.yourimage.jpg?format=gif" alt="your image as a gif"/>
    

