using MonoMod.RuntimeDetour;
using MonoMod.RuntimeDetour.Platforms;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace MonoMod.RuntimeDetour.Platforms {
    // This is based on the Core 3.0 implementation because they are nearly identical, save for how to get the GUID
#if !MONOMOD_INTERNAL
    public
#endif
    class DetourRuntimeNET50Platform : DetourRuntimeNETCore30Platform {
        // As of .NET 5, this GUID is found at src/coreclr/src/inc/corinfo.h as JITEEVersionIdentifier
        public static new readonly Guid JitVersionGuid = new Guid("a5eec3a4-4176-43a7-8c2b-a05b551d4f49");
    }
}
