// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace System.Runtime.ConstrainedExecution
{
#if !SUPPORTS_CRITICALFINALIZER
    internal abstract class CriticalFinalizerObject
    {
        protected CriticalFinalizerObject()
        {
        }

#pragma warning disable RCS1106 // Remove empty destructor.
        ~CriticalFinalizerObject()
        {
        }
#pragma warning restore RCS1106 // Remove empty destructor.
    }
#endif
}
