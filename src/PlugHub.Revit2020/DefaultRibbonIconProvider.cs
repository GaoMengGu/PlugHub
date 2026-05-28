using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace PlugHub.Revit2020
{
    internal static class DefaultRibbonIconProvider
    {
        private const string BuiltinPrefix = "builtin:";

        public static readonly IReadOnlyList<string> BuiltinIconKeys = new[]
        {
            "default",
            "settings",
            "tool",
            "duct",
            "family",
            "batch",
            "document",
            "warning"
        };

        public static ImageSource CreateSmallIcon()
        {
            return CreateSmallIcon("default");
        }

        public static ImageSource CreateSmallIcon(string key)
        {
            return CreateIcon(NormalizeKey(key), 16, 2.0);
        }

        public static ImageSource CreateLargeIcon()
        {
            return CreateLargeIcon("default");
        }

        public static ImageSource CreateLargeIcon(string key)
        {
            return CreateIcon(NormalizeKey(key), 32, 4.0);
        }

        public static bool TryCreateIcon(string iconPath, bool large, out ImageSource? icon)
        {
            icon = null;
            if (string.IsNullOrWhiteSpace(iconPath) || !iconPath.Trim().StartsWith(BuiltinPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var key = NormalizeKey(iconPath);
            icon = large ? CreateLargeIcon(key) : CreateSmallIcon(key);
            return true;
        }

        public static string ToIconPath(string key)
        {
            return BuiltinPrefix + NormalizeKey(key);
        }

        private static ImageSource CreateIcon(string key, double size, double padding)
        {
            var group = new DrawingGroup();
            using (var context = group.Open())
            {
                var background = new SolidColorBrush(BackgroundFor(key));
                var accent = new SolidColorBrush(AccentFor(key));
                var foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                background.Freeze();
                accent.Freeze();
                foreground.Freeze();

                var radius = size * 0.18;
                var shell = new Rect(padding, padding, size - padding * 2, size - padding * 2);
                context.DrawRoundedRectangle(background, null, shell, radius, radius);
                DrawSymbol(context, key, size, foreground, accent);
            }

            group.Freeze();
            var image = new DrawingImage(group);
            image.Freeze();
            return image;
        }

        private static void DrawSymbol(DrawingContext context, string key, double size, Brush foreground, Brush accent)
        {
            if (string.Equals(key, "settings", StringComparison.OrdinalIgnoreCase))
            {
                DrawSettings(context, size, foreground, accent);
                return;
            }

            if (string.Equals(key, "tool", StringComparison.OrdinalIgnoreCase))
            {
                DrawTool(context, size, foreground, accent);
                return;
            }

            if (string.Equals(key, "duct", StringComparison.OrdinalIgnoreCase))
            {
                DrawDuct(context, size, foreground, accent);
                return;
            }

            if (string.Equals(key, "family", StringComparison.OrdinalIgnoreCase))
            {
                DrawFamily(context, size, foreground, accent);
                return;
            }

            if (string.Equals(key, "batch", StringComparison.OrdinalIgnoreCase))
            {
                DrawBatch(context, size, foreground, accent);
                return;
            }

            if (string.Equals(key, "document", StringComparison.OrdinalIgnoreCase))
            {
                DrawDocument(context, size, foreground, accent);
                return;
            }

            if (string.Equals(key, "warning", StringComparison.OrdinalIgnoreCase))
            {
                DrawWarning(context, size, foreground, accent);
                return;
            }

            DrawHub(context, size, foreground, accent);
        }

        private static void DrawHub(DrawingContext context, double size, Brush foreground, Brush accent)
        {
            var hubRadius = size * 0.11;
            var center = new Point(size * 0.5, size * 0.5);
            var top = new Point(size * 0.5, size * 0.27);
            var left = new Point(size * 0.31, size * 0.63);
            var right = new Point(size * 0.69, size * 0.63);
            var pen = new Pen(foreground, size * 0.08);
            pen.Freeze();

            context.DrawLine(pen, center, top);
            context.DrawLine(pen, center, left);
            context.DrawLine(pen, center, right);
            context.DrawEllipse(foreground, null, center, hubRadius, hubRadius);
            context.DrawEllipse(accent, null, top, hubRadius * 0.9, hubRadius * 0.9);
            context.DrawEllipse(accent, null, left, hubRadius * 0.9, hubRadius * 0.9);
            context.DrawEllipse(accent, null, right, hubRadius * 0.9, hubRadius * 0.9);
        }

        private static void DrawSettings(DrawingContext context, double size, Brush foreground, Brush accent)
        {
            var center = new Point(size * 0.5, size * 0.5);
            var pen = new Pen(foreground, size * 0.08);
            pen.Freeze();
            context.DrawEllipse(null, pen, center, size * 0.18, size * 0.18);
            context.DrawEllipse(foreground, null, center, size * 0.07, size * 0.07);

            for (var index = 0; index < 8; index++)
            {
                var angle = Math.PI * 2 * index / 8;
                var point = new Point(center.X + Math.Cos(angle) * size * 0.28, center.Y + Math.Sin(angle) * size * 0.28);
                context.DrawEllipse(accent, null, point, size * 0.035, size * 0.035);
            }
        }

        private static void DrawTool(DrawingContext context, double size, Brush foreground, Brush accent)
        {
            var pen = new Pen(foreground, size * 0.1);
            pen.Freeze();
            context.DrawLine(pen, new Point(size * 0.32, size * 0.69), new Point(size * 0.68, size * 0.33));
            context.DrawEllipse(accent, null, new Point(size * 0.69, size * 0.31), size * 0.09, size * 0.09);
            context.DrawRoundedRectangle(foreground, null, new Rect(size * 0.25, size * 0.66, size * 0.18, size * 0.09), size * 0.03, size * 0.03);
        }

        private static void DrawDuct(DrawingContext context, double size, Brush foreground, Brush accent)
        {
            var pen = new Pen(foreground, size * 0.06);
            pen.Freeze();
            context.DrawRoundedRectangle(foreground, null, new Rect(size * 0.23, size * 0.38, size * 0.54, size * 0.16), size * 0.04, size * 0.04);
            context.DrawRoundedRectangle(accent, null, new Rect(size * 0.35, size * 0.55, size * 0.3, size * 0.12), size * 0.03, size * 0.03);
            context.DrawLine(pen, new Point(size * 0.5, size * 0.54), new Point(size * 0.5, size * 0.66));
        }

        private static void DrawFamily(DrawingContext context, double size, Brush foreground, Brush accent)
        {
            context.DrawRectangle(foreground, null, new Rect(size * 0.28, size * 0.34, size * 0.44, size * 0.38));
            context.DrawRectangle(accent, null, new Rect(size * 0.36, size * 0.43, size * 0.1, size * 0.1));
            context.DrawRectangle(accent, null, new Rect(size * 0.54, size * 0.43, size * 0.1, size * 0.1));
            context.DrawRectangle(accent, null, new Rect(size * 0.45, size * 0.58, size * 0.1, size * 0.14));
        }

        private static void DrawBatch(DrawingContext context, double size, Brush foreground, Brush accent)
        {
            context.DrawRoundedRectangle(accent, null, new Rect(size * 0.28, size * 0.26, size * 0.34, size * 0.42), size * 0.03, size * 0.03);
            context.DrawRoundedRectangle(foreground, null, new Rect(size * 0.36, size * 0.32, size * 0.34, size * 0.42), size * 0.03, size * 0.03);
            context.DrawRectangle(accent, null, new Rect(size * 0.42, size * 0.43, size * 0.2, size * 0.04));
            context.DrawRectangle(accent, null, new Rect(size * 0.42, size * 0.53, size * 0.16, size * 0.04));
        }

        private static void DrawDocument(DrawingContext context, double size, Brush foreground, Brush accent)
        {
            context.DrawRoundedRectangle(foreground, null, new Rect(size * 0.33, size * 0.24, size * 0.36, size * 0.52), size * 0.03, size * 0.03);
            context.DrawRectangle(accent, null, new Rect(size * 0.4, size * 0.42, size * 0.22, size * 0.04));
            context.DrawRectangle(accent, null, new Rect(size * 0.4, size * 0.52, size * 0.18, size * 0.04));
        }

        private static void DrawWarning(DrawingContext context, double size, Brush foreground, Brush accent)
        {
            var geometry = new StreamGeometry();
            using (var geometryContext = geometry.Open())
            {
                geometryContext.BeginFigure(new Point(size * 0.5, size * 0.24), true, true);
                geometryContext.LineTo(new Point(size * 0.76, size * 0.72), true, false);
                geometryContext.LineTo(new Point(size * 0.24, size * 0.72), true, false);
            }

            geometry.Freeze();
            context.DrawGeometry(foreground, null, geometry);
            context.DrawRectangle(accent, null, new Rect(size * 0.47, size * 0.4, size * 0.06, size * 0.18));
            context.DrawEllipse(accent, null, new Point(size * 0.5, size * 0.64), size * 0.035, size * 0.035);
        }

        private static Color BackgroundFor(string key)
        {
            if (string.Equals(key, "settings", StringComparison.OrdinalIgnoreCase)) return Color.FromRgb(69, 86, 110);
            if (string.Equals(key, "tool", StringComparison.OrdinalIgnoreCase)) return Color.FromRgb(36, 121, 94);
            if (string.Equals(key, "duct", StringComparison.OrdinalIgnoreCase)) return Color.FromRgb(20, 128, 168);
            if (string.Equals(key, "family", StringComparison.OrdinalIgnoreCase)) return Color.FromRgb(108, 84, 162);
            if (string.Equals(key, "batch", StringComparison.OrdinalIgnoreCase)) return Color.FromRgb(176, 102, 38);
            if (string.Equals(key, "document", StringComparison.OrdinalIgnoreCase)) return Color.FromRgb(82, 111, 150);
            if (string.Equals(key, "warning", StringComparison.OrdinalIgnoreCase)) return Color.FromRgb(176, 63, 55);
            return Color.FromRgb(26, 115, 232);
        }

        private static Color AccentFor(string key)
        {
            if (string.Equals(key, "warning", StringComparison.OrdinalIgnoreCase)) return Color.FromRgb(176, 63, 55);
            if (string.Equals(key, "batch", StringComparison.OrdinalIgnoreCase)) return Color.FromRgb(255, 211, 122);
            return Color.FromRgb(48, 196, 141);
        }

        private static string NormalizeKey(string key)
        {
            var normalized = (key ?? string.Empty).Trim();
            if (normalized.StartsWith(BuiltinPrefix, StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(BuiltinPrefix.Length);
            }

            foreach (var builtin in BuiltinIconKeys)
            {
                if (string.Equals(normalized, builtin, StringComparison.OrdinalIgnoreCase))
                {
                    return builtin;
                }
            }

            return "default";
        }
    }
}
