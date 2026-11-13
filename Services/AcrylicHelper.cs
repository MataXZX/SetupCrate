using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace SetupCrate.Services;

public static class AcrylicHelper
{
    private const int WcaAccentPolicy = 19;
    private const int DwmwaWindowCornerPreference = 33;

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public int AccentState;
        public int AccentFlags;
        public int GradientColor;
        public int AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public int Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public static void Apply(Window window, bool glass)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;

            var corner = glass ? 2 : 1;
            DwmSetWindowAttribute(hwnd, DwmwaWindowCornerPreference, ref corner, sizeof(int));

            var backdrop = glass ? 3 : 1;
            DwmSetWindowAttribute(hwnd, 38, ref backdrop, sizeof(int));

            var darkTitle = glass ? 1 : 0;
            DwmSetWindowAttribute(hwnd, 20, ref darkTitle, sizeof(int));

            var policy = new AccentPolicy
            {
                AccentState = glass ? 4 : 0,
                AccentFlags = 2,
                GradientColor = glass ? unchecked((int)0xB4180F0A) : 0,
                AnimationId = 0
            };

            var size = Marshal.SizeOf<AccentPolicy>();
            var pointer = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(policy, pointer, false);
                var data = new WindowCompositionAttributeData
                {
                    Attribute = WcaAccentPolicy,
                    Data = pointer,
                    SizeOfData = size
                };
                SetWindowCompositionAttribute(hwnd, ref data);
            }
            finally
            {
                Marshal.FreeHGlobal(pointer);
            }
        }
        catch
        {
        }
    }
}
