using System;
using UnityEngine;
using VRGIN.Visuals;

namespace VRGIN.Core
{
    public interface IVRManagerContext
    {
        string GuiLayer { get; }

        string UILayer { get; }

        int UILayerMask { get; }

        int IgnoreMask { get; }

        Color PrimaryColor { get; }

        IMaterialPalette Materials { get; }

        VRSettings Settings { get; }

        string InvisibleLayer { get; }

        bool SimulateCursor { get; }

        bool GUIAlternativeSortingMode { get; }

        Type VoiceCommandType { get; }

        float GuiNearClipPlane { get; }

        float GuiFarClipPlane { get; }

        float NearClipPlane { get; }

        float UnitToMeter { get; }

        bool EnforceDefaultGUIMaterials { get; }

        bool ConfineMouse { get; }

        GUIType PreferredGUI { get; }

        bool ForceIMGUIOnScreen { get; }
    }
}
