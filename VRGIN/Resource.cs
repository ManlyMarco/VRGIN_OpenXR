using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using VRGIN.Core;

namespace VRGIN
{
    [GeneratedCode("System.Resources.Tools.StronglyTypedResourceBuilder", "16.0.0.0")]
    [DebuggerNonUserCode]
    [CompilerGenerated]
    internal class Resource
    {
        internal Resource() { }

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        internal static CultureInfo Culture
        {
            get => resourceCulture;
            set => resourceCulture = value;
        }

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        internal static ResourceManager ResourceManager
        {
            get
            {
                if (resourceMan == null) resourceMan = new ResourceManager("VRGIN_OpenXR.VRGIN.Resource", typeof(VRGIN.Resource).Assembly);
                return resourceMan;
            }
        }

        internal static byte[] steamvr_2019 => (byte[])ResourceManager.GetObject("steamvr_2019", resourceCulture);

        private static CultureInfo resourceCulture;

        private static ResourceManager resourceMan;
    }
}
