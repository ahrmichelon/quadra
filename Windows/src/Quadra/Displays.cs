using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Quadra;

/// <summary>A display. Positions are in physical pixels; the work area leaves out the taskbar.</summary>
public sealed record Display(IntPtr Handle, string DeviceName, string Name, RectD Bounds, RectD WorkArea,
                               double Dpi, bool IsPrimary)
{
    /// <summary>1 at 100% scaling, 1.5 at 150% and so on.</summary>
    public double Scale => Dpi / 96.0;

    public bool SameAs(Display? other) => other != null && other.DeviceName == DeviceName;
}

internal static class Displays
{
    /// <summary>Every display, from left to right (top to bottom when stacked), the order shown in the panel.</summary>
    public static List<Display> All()
    {
        var handles = new List<IntPtr>();
        Native.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (monitor, _, _, _) => { handles.Add(monitor); return true; },
                                   IntPtr.Zero);
        var names = FriendlyNames();
        var list = new List<Display>();
        foreach (var handle in handles)
        {
            var info = new Native.MONITORINFOEX { cbSize = Marshal.SizeOf<Native.MONITORINFOEX>() };
            if (!Native.GetMonitorInfo(handle, ref info)) continue;
            uint dpi = Native.GetDpiForMonitor(handle, Native.MDT_EFFECTIVE_DPI, out uint dpiX, out _) == 0 ? dpiX : 96;
            names.TryGetValue(info.szDevice, out var name);
            list.Add(new Display(handle, info.szDevice, name ?? "", info.rcMonitor.ToRectD(), info.rcWork.ToRectD(),
                                 dpi, (info.dwFlags & Native.MONITORINFOF_PRIMARY) != 0));
        }
        list.Sort((a, b) => WindowGeometry.IsOrderedBefore(a.Bounds, b.Bounds) ? -1
                          : WindowGeometry.IsOrderedBefore(b.Bounds, a.Bounds) ? 1 : 0);
        // Displays without a readable name get "Display 2" and so on.
        return list.Select((d, i) => d.Name.Length > 0 ? d : d with { Name = Loc.Format("Display_Number", i + 1) })
                   .ToList();
    }

    /// <summary>The display that holds most of the rectangle (the nearest one when it touches none).</summary>
    public static Display ForRect(RectD rect, IReadOnlyList<Display> all)
    {
        if (WindowGeometry.IndexOfLargestOverlap(rect, all.Select(d => d.Bounds).ToList()) is int i) return all[i];
        return ForPoint(rect.CenterX, rect.CenterY, all);
    }

    /// <summary>The display under a point, or the nearest one.</summary>
    public static Display ForPoint(double x, double y, IReadOnlyList<Display> all)
    {
        var handle = Native.MonitorFromPoint(new Native.POINT((int)Math.Round(x), (int)Math.Round(y)),
                                             Native.MONITOR_DEFAULTTONEAREST);
        return all.FirstOrDefault(d => d.Handle == handle) ?? all.FirstOrDefault(d => d.IsPrimary) ?? all[0];
    }

    /// <summary>
    /// Names such as "DELL U3419W" for each display, by device name (\\.\DISPLAY1).
    /// Built-in laptop screens usually have no name and get "Built-in display".
    /// </summary>
    private static Dictionary<string, string> FriendlyNames()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (Native.GetDisplayConfigBufferSizes(Native.QDC_ONLY_ACTIVE_PATHS, out uint pathCount, out uint modeCount) != 0)
                return result;
            var paths = new Native.DISPLAYCONFIG_PATH_INFO[pathCount];
            var modes = new Native.DISPLAYCONFIG_MODE_INFO[modeCount];
            if (Native.QueryDisplayConfig(Native.QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths, ref modeCount, modes,
                                          IntPtr.Zero) != 0)
                return result;

            for (int i = 0; i < pathCount; i++)
            {
                var path = paths[i];
                var source = new Native.DISPLAYCONFIG_SOURCE_DEVICE_NAME
                {
                    header = new Native.DISPLAYCONFIG_DEVICE_INFO_HEADER
                    {
                        type = Native.DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME,
                        size = (uint)Marshal.SizeOf<Native.DISPLAYCONFIG_SOURCE_DEVICE_NAME>(),
                        adapterId = path.sourceInfo.adapterId,
                        id = path.sourceInfo.id,
                    },
                };
                if (Native.DisplayConfigGetSourceName(ref source) != 0) continue;

                var target = new Native.DISPLAYCONFIG_TARGET_DEVICE_NAME
                {
                    header = new Native.DISPLAYCONFIG_DEVICE_INFO_HEADER
                    {
                        type = Native.DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME,
                        size = (uint)Marshal.SizeOf<Native.DISPLAYCONFIG_TARGET_DEVICE_NAME>(),
                        adapterId = path.targetInfo.adapterId,
                        id = path.targetInfo.id,
                    },
                };
                if (Native.DisplayConfigGetTargetName(ref target) != 0) continue;

                string name = target.monitorFriendlyDeviceName?.Trim() ?? "";
                bool builtIn = target.outputTechnology is Native.DISPLAYCONFIG_OUTPUT_TECHNOLOGY_INTERNAL
                    or Native.DISPLAYCONFIG_OUTPUT_TECHNOLOGY_DISPLAYPORT_EMBEDDED
                    or Native.DISPLAYCONFIG_OUTPUT_TECHNOLOGY_UDI_EMBEDDED;
                if (name.Length == 0 && builtIn) name = Loc.Get("Display_BuiltIn");
                // With mirrored displays one source has several targets; keep the first name.
                if (name.Length > 0 && !string.IsNullOrEmpty(source.viewGdiDeviceName))
                    result.TryAdd(source.viewGdiDeviceName, name);
            }
        }
        catch (Exception e)
        {
            Log.Write($"Couldn't read the display names: {e.Message}");
        }
        return result;
    }
}
