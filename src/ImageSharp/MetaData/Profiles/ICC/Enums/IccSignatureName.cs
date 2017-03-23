// <copyright file="IccSignatureName.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
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

        SceneColorimetryEstimates = 0x73636F65,             // scoe

        SceneAppearanceEstimates = 0x73617065,              // sape

        FocalPlaneColorimetryEstimates = 0x66706365,        // fpce

        ReflectionHardcopyOriginalColorimetry = 0x72686F63, // rhoc

        ReflectionPrintOutputColorimetry = 0x72706F63,      // rpoc

        PerceptualReferenceMediumGamut = 0x70726D67,        // prmg

        FilmScanner = 0x6673636E,                           // fscn

        DigitalCamera = 0x6463616D,                         // dcam

        ReflectiveScanner = 0x7273636E,                     // rscn

        InkJetPrinter = 0x696A6574,                         // ijet

        ThermalWaxPrinter = 0x74776178,                     // twax

        ElectrophotographicPrinter = 0x6570686F,            // epho

        ElectrostaticPrinter = 0x65737461,                  // esta

        DyeSublimationPrinter = 0x64737562,                 // dsub

        PhotographicPaperPrinter = 0x7270686F,              // rpho

        FilmWriter = 0x6670726E,                            // fprn

        VideoMonitor = 0x7669646D,                          // vidm

        VideoCamera = 0x76696463,                           // vidc

        ProjectionTelevision = 0x706A7476,                  // pjtv

        CathodeRayTubeDisplay = 0x43525420,                 // CRT

        PassiveMatrixDisplay = 0x504D4420,                  // PMD

        ActiveMatrixDisplay = 0x414D4420,                   // AMD

        PhotoCD = 0x4B504344,                               // KPCD

        PhotographicImageSetter = 0x696D6773,               // imgs

        Gravure = 0x67726176,                               // grav

        OffsetLithography = 0x6F666673,                     // offs

        Silkscreen = 0x73696C6B,                            // silk

        Flexography = 0x666C6578,                           // flex

        MotionPictureFilmScanner = 0x6D706673,              // mpfs

        MotionPictureFilmRecorder = 0x6D706672,             // mpfr

        DigitalMotionPictureCamera = 0x646D7063,            // dmpc

        DigitalCinemaProjector = 0x64636A70,                // dcpj
    }
}
