using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using VRGIN.Core;
using Debug = System.Diagnostics.Debug;
using IntPtr = System.IntPtr;

namespace VRGIN.Native
{
    public class WindowManager
    {
        private static IntPtr? _Handle;

        public static IntPtr Handle
        {
            get
            {
                if (!_Handle.HasValue)
                {
                    var currentProcess = Process.GetCurrentProcess();
                    var rootWindowsOfProcess = GetRootWindowsOfProcess(currentProcess.Id);

                    if (rootWindowsOfProcess.Count == 0)
                    {
                        // Looks like on some Linux systems no windows are reported
                        // These workarounds are far from perfect but it's better than not working at all
                        try
                        {
                            _Handle = currentProcess.MainWindowHandle;
                            VRLog.Warn("Assuming MainWindowHandle is the main window. Cursor and GUI might have issues!");
                        }
                        catch (Exception e)
                        {
                            VRLog.Error(e);
                        }

                        if (!_Handle.HasValue || _Handle.Value == IntPtr.Zero)
                        {
                            VRLog.Warn("No window handles found! Cursor and GUI are going to have issues!");
                            _Handle = IntPtr.Zero;
                        }
                    }
                    else
                    {
                        var best = double.NegativeInfinity;
                        foreach (var handle in rootWindowsOfProcess)
                        {
                            var score = MainWindowScore(handle);
                            if (score.HasValue && score.Value >= best)
                            {
                                best = score.Value;
                                _Handle = handle;
                            }
                        }

                        if (!_Handle.HasValue)
                        {
                            VRLog.Warn("Fall back to first handle!");
                            _Handle = rootWindowsOfProcess.First();
                        }
                    }
                }

                Debug.Assert(_Handle != null, "_Handle != null");
                return _Handle.Value;
            }
        }

        /// <summary>
        /// Returns a score indicating how likely the given window handle points
        /// to the main game window. This is needed because our versions of Unity
        /// don't offer a good way to find the main window.
        /// </summary>
        /// <param name="handle"></param>
        /// <returns></returns>
        private static double? MainWindowScore(IntPtr handle)
        {
            WindowsInterop.RECT rect = new WindowsInterop.RECT();
            if (!WindowsInterop.GetClientRect(handle, ref rect))
                return null;
            double score = 0;
            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;
            if (width == Screen.width && height == Screen.height)
            {
                score += 1;
            }
            score -= Math.Abs(Math.Log(Screen.width + 1) - Math.Log(width + 1)) +
                     Math.Abs(Math.Log(Screen.height + 1) - Math.Log(height + 1));
            if (GetWindowText(handle).Contains("BepInEx"))
            {
                // Likely a BepInEx console.
                score -= 1;
            }
            return score;
        }

        public static List<IntPtr> GetRootWindowsOfProcess(int process)
        {
            IntPtr[] apRet = (new IntPtr[256]);
            int iCount = 0;
            IntPtr pLast = IntPtr.Zero;
            do {
                pLast = WindowsInterop.FindWindowEx(IntPtr.Zero, pLast, null, null);
                uint iProcess_;
                WindowsInterop.GetWindowThreadProcessId(pLast, out iProcess_);
                if(iProcess_ == process) apRet[iCount++] = pLast;
            } while(pLast != IntPtr.Zero);
            System.Array.Resize(ref apRet, iCount);
            return apRet.ToList();
        }

        private static List<IntPtr> GetChildWindows(IntPtr parent)
        {
            var list = new List<IntPtr>();
            var value = GCHandle.Alloc(list);
            try
            {
                WindowsInterop.Win32Callback callback = EnumWindow;
                WindowsInterop.EnumChildWindows(parent, callback, GCHandle.ToIntPtr(value));
                return list;
            }
            finally
            {
                if (value.IsAllocated) value.Free();
            }
        }

        private static bool EnumWindow(IntPtr handle, IntPtr pointer)
        {
            (GCHandle.FromIntPtr(pointer).Target as List<IntPtr> ?? throw new InvalidCastException("GCHandle Target could not be cast as List<IntPtr>")).Add(handle);
            return true;
        }

        public static string GetWindowText(IntPtr hWnd)
        {
            var windowTextLength = WindowsInterop.GetWindowTextLength(hWnd);
            if (windowTextLength == 0) return string.Empty;
            var stringBuilder = new StringBuilder(windowTextLength + 1);
            WindowsInterop.GetWindowText(hWnd, stringBuilder, stringBuilder.Capacity);
            return stringBuilder.ToString();
        }

        public static void ConfineCursor()
        {
            var rcClip = GetClientRect();
            WindowsInterop.ClipCursor(ref rcClip);
        }

        public static WindowsInterop.RECT GetClientRect()
        {
            var handle = Handle;
            if (handle == IntPtr.Zero)
                return new WindowsInterop.RECT(0, 0, Screen.width, Screen.height);

            var lpRect = default(WindowsInterop.RECT);
            WindowsInterop.GetClientRect(handle, ref lpRect);
            var lpPoint = default(WindowsInterop.POINT);
            WindowsInterop.ClientToScreen(handle, ref lpPoint);
            lpRect.Left = lpPoint.X;
            lpRect.Top = lpPoint.Y;
            lpRect.Right += lpPoint.X;
            lpRect.Bottom += lpPoint.Y;
            return lpRect;
        }

        public static void Activate()
        {
            var handle = Handle;
            if (handle != IntPtr.Zero)
                WindowsInterop.SetForegroundWindow(handle);
        }
    }
}
