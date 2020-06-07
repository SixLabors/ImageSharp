// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc
{
    /// <summary>
    /// Signature Name
    /// </summary>
    internal enum IccSignatureName : uint
    {
        /// <summary>
        /// Unknown signature
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Scene Colorimetry Estimates
        /// </summary>
        SceneColorimetryEstimates = 0x73636F65,             // scoe

        /// <summary>
        /// Scene Appearance Estimates
        /// </summary>
        SceneAppearanceEstimates = 0x73617065,              // sape

        /// <summary>
        /// Focal Plane Colorimetry Estimates
        /// </summary>
        FocalPlaneColorimetryEstimates = 0x66706365,        // fpce

        /// <summary>
        /// Reflection Hardcopy Original Colorimetry
        /// </summary>
        ReflectionHardcopyOriginalColorimetry = 0x72686F63, // rhoc

        /// <summary>
        /// Reflection Print Output Colorimetry
        /// </summary>
        ReflectionPrintOutputColorimetry = 0x72706F63,      // rpoc

        /// <summary>
        /// Perceptual Reference Medium Gamut
        /// </summary>
        PerceptualReferenceMediumGamut = 0x70726D67,        // prmg

        /// <summary>
        /// Film Scanner
        /// </summary>
        FilmScanner = 0x6673636E,                           // fscn

        /// <summary>
        /// Digital Camera
        /// </summary>
        DigitalCamera = 0x6463616D,                         // dcam

        /// <summary>
        /// Reflective Scanner
        /// </summary>
        ReflectiveScanner = 0x7273636E,                     // rscn

        /// <summary>
        /// InkJet Printer
        /// </summary>
        InkJetPrinter = 0x696A6574,                         // ijet

        /// <summary>
        /// Thermal Wax Printer
        /// </summary>
        ThermalWaxPrinter = 0x74776178,                     // twax

        /// <summary>
        /// Electrophotographic Printer
        /// </summary>
        ElectrophotographicPrinter = 0x6570686F,            // epho

        /// <summary>
        /// Electrostatic Printer
        /// </summary>
        ElectrostaticPrinter = 0x65737461,                  // esta

        /// <summary>
        /// Dye Sublimation Printer
        /// </summary>
        DyeSublimationPrinter = 0x64737562,                 // dsub

        /// <summary>
        /// Photographic Paper Printer
        /// </summary>
        PhotographicPaperPrinter = 0x7270686F,              // rpho

        /// <summary>
        /// Film Writer
        /// </summary>
        FilmWriter = 0x6670726E,                            // fprn

        /// <summary>
        /// Video Monitor
        /// </summary>
        VideoMonitor = 0x7669646D,                          // vidm

        /// <summary>
        /// Video Camera
        /// </summary>
        VideoCamera = 0x76696463,                           // vidc

        /// <summary>
        /// Projection Television
        /// </summary>
        ProjectionTelevision = 0x706A7476,                  // pjtv

        /// <summary>
        /// Cathode Ray Tube Display
        /// </summary>
        CathodeRayTubeDisplay = 0x43525420,                 // CRT

        /// <summary>
        /// Passive Matrix Display
        /// </summary>
        PassiveMatrixDisplay = 0x504D4420,                  // PMD

        /// <summary>
        /// Active Matrix Display
        /// </summary>
        ActiveMatrixDisplay = 0x414D4420,                   // AMD

        /// <summary>
        /// Photo CD
        /// </summary>
        PhotoCD = 0x4B504344,                               // KPCD

        /// <summary>
        /// Photographic Image Setter
        /// </summary>
        PhotographicImageSetter = 0x696D6773,               // imgs

        /// <summary>
        /// Gravure
        /// </summary>
        Gravure = 0x67726176,                               // grav

        /// <summary>
        /// Offset Lithography
        /// </summary>
        OffsetLithography = 0x6F666673,                     // offs

        /// <summary>
        /// Silkscreen
        /// </summary>
        Silkscreen = 0x73696C6B,                            // silk

        /// <summary>
        /// Flexography
        /// </summary>
        Flexography = 0x666C6578,                           // flex

        /// <summary>
        /// Motion Picture Film Scanner
        /// </summary>
        MotionPictureFilmScanner = 0x6D706673,              // mpfs

        /// <summary>
        /// Motion Picture Film Recorder
        /// </summary>
        MotionPictureFilmRecorder = 0x6D706672,             // mpfr

        /// <summary>
        /// Digital Motion Picture Camera
        /// </summary>
        DigitalMotionPictureCamera = 0x646D7063,            // dmpc

        /// <summary>
        /// Digital Cinema Projector
        /// </summary>
        DigitalCinemaProjector = 0x64636A70,                // dcpj
    }
}
