using System.Windows;
using System.Windows.Media;

namespace PlugHub.Revit2020
{
    internal static class DefaultRibbonIconProvider
    {
        public static ImageSource CreateSmallIcon()
        {
            return CreateIcon(16, 2.0);
        }

        public static ImageSource CreateLargeIcon()
        {
            return CreateIcon(32, 4.0);
        }

        private static ImageSource CreateIcon(double size, double padding)
        {
            var group = new DrawingGroup();
            using (var context = group.Open())
            {
                var background = new SolidColorBrush(Color.FromRgb(26, 115, 232));
                var accent = new SolidColorBrush(Color.FromRgb(48, 196, 141));
                var foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                background.Freeze();
                accent.Freeze();
                foreground.Freeze();

                var radius = size * 0.18;
                var shell = new Rect(padding, padding, size - padding * 2, size - padding * 2);
                context.DrawRoundedRectangle(background, null, shell, radius, radius);

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

            group.Freeze();
            var image = new DrawingImage(group);
            image.Freeze();
            return image;
        }
    }
}
