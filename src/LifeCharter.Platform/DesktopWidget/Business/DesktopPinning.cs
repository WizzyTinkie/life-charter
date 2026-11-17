using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace LifeCharter.Platform.DesktopWidget.Business;

/// <summary>
/// Keeps a window on the desktop layer on Windows: no taskbar or Alt-Tab entry, never
/// activated by clicks, and always at the bottom of the z-order.
/// </summary>
[SupportedOSPlatform("windows")]
public static partial class DesktopPinning
{
    public const uint WindowPosChangingMessage = 0x0046;

    private const int ExtendedStyleIndex = -20;
    private const nint ToolWindowStyle = 0x00000080;
    private const nint NoActivateStyle = 0x08000000;
    private const nint BottomWindow = 1;
    private const uint NoSize = 0x0001;
    private const uint NoMove = 0x0002;
    private const uint NoZOrder = 0x0004;
    private const uint NoActivate = 0x0010;

    public static void Pin(nint windowHandle)
    {
        var style = GetWindowLongPtr(windowHandle, ExtendedStyleIndex);
        SetWindowLongPtr(windowHandle, ExtendedStyleIndex, style | ToolWindowStyle | NoActivateStyle);
        SetWindowPos(windowHandle, BottomWindow, 0, 0, 0, 0, NoSize | NoMove | NoActivate);
    }

    /// <summary>
    /// Call from a WndProc hook on <see cref="WindowPosChangingMessage"/>: rewrites the
    /// pending z-order change so the window stays at the bottom.
    /// </summary>
    public static void KeepAtBottom(nint windowPosPointer)
    {
        var position = Marshal.PtrToStructure<WindowPos>(windowPosPointer);
        position.InsertAfter = BottomWindow;
        position.Flags &= ~NoZOrder;
        Marshal.StructureToPtr(position, windowPosPointer, fDeleteOld: false);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowPos
    {
        public nint Window;
        public nint InsertAfter;
        public int X;
        public int Y;
        public int Width;
        public int Height;
        public uint Flags;
    }

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static partial nint GetWindowLongPtr(nint window, int index);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static partial nint SetWindowLongPtr(nint window, int index, nint value);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowPos(nint window, nint insertAfter, int x, int y, int width, int height, uint flags);
}
