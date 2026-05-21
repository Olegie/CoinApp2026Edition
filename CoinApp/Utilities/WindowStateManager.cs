using System;
using System.Windows;

namespace CoinApp.Utilities
{
    public static class WindowStateManager
    {
        public static double Width { get; set; } = 1080;
        public static double Height { get; set; } = 720;
        public static double Top { get; set; } = 100;
        public static double Left { get; set; } = 100;
        public static bool IsMaximized { get; set; } = false;

        public static void Capture(Window window)
        {
            if (window.WindowState == WindowState.Maximized)
            {
                Width = window.RestoreBounds.Width;
                Height = window.RestoreBounds.Height;
                Top = window.RestoreBounds.Top;
                Left = window.RestoreBounds.Left;
                IsMaximized = true;
                return;
            }

            Width = window.Width;
            Height = window.Height;
            Top = window.Top;
            Left = window.Left;
            IsMaximized = false;
        }

        public static void Apply(Window window)
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Width = Width;
            window.Height = Height;
            window.Top = Top;
            window.Left = Left;
            window.WindowState = IsMaximized ? WindowState.Maximized : WindowState.Normal;
        }

        public static void ToggleMaximize(Window window)
        {
            window.WindowState = window.WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }
    }
}
