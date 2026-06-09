using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace PlugHub.Wpf
{
    public sealed class RevitUiPalette
    {
        public RevitUiPalette(bool isDark)
        {
            IsDark = isDark;

            WindowBackground = Brush(isDark ? Color.FromRgb(41, 43, 46) : Color.FromRgb(245, 246, 248));
            PanelBackground = Brush(isDark ? Color.FromRgb(50, 53, 56) : Color.FromRgb(255, 255, 255));
            SurfaceBackground = Brush(isDark ? Color.FromRgb(58, 61, 65) : Color.FromRgb(250, 251, 252));
            ControlBackground = Brush(isDark ? Color.FromRgb(61, 64, 68) : Color.FromRgb(255, 255, 255));
            ControlHoverBackground = Brush(isDark ? Color.FromRgb(68, 72, 77) : Color.FromRgb(243, 247, 250));
            ControlPressedBackground = Brush(isDark ? Color.FromRgb(75, 80, 86) : Color.FromRgb(232, 239, 246));
            BorderBrush = Brush(isDark ? Color.FromRgb(92, 97, 104) : Color.FromRgb(198, 204, 211));
            StrongBorderBrush = Brush(isDark ? Color.FromRgb(126, 133, 142) : Color.FromRgb(150, 158, 168));
            TextBrush = Brush(isDark ? Color.FromRgb(239, 242, 246) : Color.FromRgb(31, 35, 41));
            MutedTextBrush = Brush(isDark ? Color.FromRgb(190, 197, 205) : Color.FromRgb(92, 99, 112));
            SubtleTextBrush = Brush(isDark ? Color.FromRgb(156, 164, 174) : Color.FromRgb(115, 123, 135));
            AccentBrush = Brush(isDark ? Color.FromRgb(91, 170, 226) : Color.FromRgb(0, 120, 212));
            AccentSoftBrush = Brush(isDark ? Color.FromRgb(42, 69, 91) : Color.FromRgb(219, 237, 252));
            AccentPressedBrush = Brush(isDark ? Color.FromRgb(30, 89, 130) : Color.FromRgb(199, 226, 246));
            AccentForegroundBrush = Brush(Color.FromRgb(255, 255, 255));
            SelectionBrush = Brush(isDark ? Color.FromRgb(35, 70, 98) : Color.FromRgb(220, 238, 252));
            AlternatingRowBrush = Brush(isDark ? Color.FromRgb(55, 58, 62) : Color.FromRgb(247, 249, 252));
            HeaderBackground = Brush(isDark ? Color.FromRgb(61, 65, 70) : Color.FromRgb(239, 242, 246));
            ChipBackground = Brush(isDark ? Color.FromRgb(63, 68, 74) : Color.FromRgb(242, 244, 247));
            DisabledTextBrush = Brush(isDark ? Color.FromRgb(125, 132, 140) : Color.FromRgb(146, 153, 163));
            SuccessBrush = Brush(isDark ? Color.FromRgb(69, 168, 108) : Color.FromRgb(22, 137, 65));
            UpdateBrush = Brush(isDark ? Color.FromRgb(91, 170, 226) : Color.FromRgb(0, 120, 212));
            DangerBrush = Brush(isDark ? Color.FromRgb(232, 95, 86) : Color.FromRgb(180, 43, 36));
            DangerSoftBrush = Brush(isDark ? Color.FromRgb(88, 48, 45) : Color.FromRgb(253, 232, 230));
            DangerHoverBrush = Brush(isDark ? Color.FromRgb(112, 57, 52) : Color.FromRgb(248, 211, 209));
        }

        public bool IsDark { get; }
        public Brush WindowBackground { get; }
        public Brush PanelBackground { get; }
        public Brush SurfaceBackground { get; }
        public Brush ControlBackground { get; }
        public Brush ControlHoverBackground { get; }
        public Brush ControlPressedBackground { get; }
        public Brush BorderBrush { get; }
        public Brush StrongBorderBrush { get; }
        public Brush TextBrush { get; }
        public Brush MutedTextBrush { get; }
        public Brush SubtleTextBrush { get; }
        public Brush AccentBrush { get; }
        public Brush AccentSoftBrush { get; }
        public Brush AccentPressedBrush { get; }
        public Brush AccentForegroundBrush { get; }
        public Brush SelectionBrush { get; }
        public Brush AlternatingRowBrush { get; }
        public Brush HeaderBackground { get; }
        public Brush ChipBackground { get; }
        public Brush DisabledTextBrush { get; }
        public Brush SuccessBrush { get; }
        public Brush UpdateBrush { get; }
        public Brush DangerBrush { get; }
        public Brush DangerSoftBrush { get; }
        public Brush DangerHoverBrush { get; }

        private static SolidColorBrush Brush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }

    public static class RevitUiTheme
    {
        private static readonly Lazy<RevitUiPalette> CurrentPalette = new Lazy<RevitUiPalette>(() => new RevitUiPalette(IsDarkTheme()));

        public static RevitUiPalette Current
        {
            get { return CurrentPalette.Value; }
        }

        public static void Apply(Window window)
        {
            if (window == null) return;

            var palette = Current;
            window.Background = palette.WindowBackground;
            window.FontFamily = new FontFamily("Segoe UI");
            window.FontSize = 12.0;
            TextOptions.SetTextFormattingMode(window, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(window, TextRenderingMode.Auto);
            window.Resources.MergedDictionaries.Add(CreateResources(palette));
        }

        private static ResourceDictionary CreateResources(RevitUiPalette palette)
        {
            var resources = new ResourceDictionary();
            AddSystemColorResources(resources, palette);
            resources.Add(typeof(Button), ButtonStyle(palette));
            resources.Add(typeof(TextBox), TextBoxStyle(palette));
            resources.Add(typeof(ComboBox), ComboBoxStyle(palette));
            resources.Add(typeof(ComboBoxItem), ComboBoxItemStyle(palette));
            resources.Add(typeof(CheckBox), CheckBoxStyle(palette));
            resources.Add(typeof(TabControl), TabControlStyle(palette));
            resources.Add(typeof(TabItem), TabItemStyle(palette));
            resources.Add(typeof(DataGrid), DataGridStyle(palette));
            resources.Add(typeof(DataGridRow), DataGridRowStyle(palette));
            resources.Add(typeof(DataGridColumnHeader), DataGridColumnHeaderStyle(palette));
            resources.Add(typeof(ListBox), ListBoxStyle(palette));
            resources.Add(typeof(ListBoxItem), ListBoxItemStyle(palette));
            resources.Add(typeof(ContextMenu), ContextMenuStyle(palette));
            resources.Add(typeof(MenuItem), MenuItemStyle(palette));
            return resources;
        }

        private static void AddSystemColorResources(ResourceDictionary resources, RevitUiPalette palette)
        {
            resources[SystemColors.WindowBrushKey] = palette.ControlBackground;
            resources[SystemColors.WindowTextBrushKey] = palette.TextBrush;
            resources[SystemColors.ControlBrushKey] = palette.ControlBackground;
            resources[SystemColors.ControlTextBrushKey] = palette.TextBrush;
            resources[SystemColors.HighlightBrushKey] = palette.SelectionBrush;
            resources[SystemColors.HighlightTextBrushKey] = palette.TextBrush;
            resources[SystemColors.MenuBrushKey] = palette.PanelBackground;
            resources[SystemColors.MenuTextBrushKey] = palette.TextBrush;
            resources[SystemColors.GrayTextBrushKey] = palette.DisabledTextBrush;
            resources[SystemColors.HotTrackBrushKey] = palette.AccentBrush;
        }

        private static Style ButtonStyle(RevitUiPalette palette)
        {
            var style = new Style(typeof(Button));
            style.Setters.Add(new Setter(Control.BackgroundProperty, palette.ControlBackground));
            style.Setters.Add(new Setter(Control.ForegroundProperty, palette.TextBrush));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, palette.BorderBrush));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 3, 8, 3)));
            style.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 28.0));
            style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
            style.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));

            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(Control.BackgroundProperty, palette.ControlHoverBackground));
            hover.Setters.Add(new Setter(Control.BorderBrushProperty, palette.StrongBorderBrush));
            style.Triggers.Add(hover);

            var pressed = new Trigger { Property = ButtonBase.IsPressedProperty, Value = true };
            pressed.Setters.Add(new Setter(Control.BackgroundProperty, palette.ControlPressedBackground));
            pressed.Setters.Add(new Setter(Control.BorderBrushProperty, palette.AccentBrush));
            style.Triggers.Add(pressed);

            var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
            disabled.Setters.Add(new Setter(Control.ForegroundProperty, palette.DisabledTextBrush));
            disabled.Setters.Add(new Setter(Control.BackgroundProperty, palette.SurfaceBackground));
            style.Triggers.Add(disabled);
            return style;
        }

        private static Style TextBoxStyle(RevitUiPalette palette)
        {
            var style = new Style(typeof(TextBox));
            style.Setters.Add(new Setter(Control.BackgroundProperty, palette.ControlBackground));
            style.Setters.Add(new Setter(Control.ForegroundProperty, palette.TextBrush));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, palette.BorderBrush));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(5, 2, 5, 2)));
            style.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
            style.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 26.0));

            var focused = new Trigger { Property = UIElement.IsKeyboardFocusWithinProperty, Value = true };
            focused.Setters.Add(new Setter(Control.BorderBrushProperty, palette.AccentBrush));
            style.Triggers.Add(focused);
            return style;
        }

        private static Style ComboBoxStyle(RevitUiPalette palette)
        {
            var style = new Style(typeof(ComboBox));
            style.Setters.Add(new Setter(Control.BackgroundProperty, palette.ControlBackground));
            style.Setters.Add(new Setter(Control.ForegroundProperty, palette.TextBrush));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, palette.BorderBrush));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(4, 1, 4, 1)));
            style.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 26.0));

            var focused = new Trigger { Property = UIElement.IsKeyboardFocusWithinProperty, Value = true };
            focused.Setters.Add(new Setter(Control.BorderBrushProperty, palette.AccentBrush));
            style.Triggers.Add(focused);
            return style;
        }

        private static Style ComboBoxItemStyle(RevitUiPalette palette)
        {
            var style = new Style(typeof(ComboBoxItem));
            style.Setters.Add(new Setter(Control.BackgroundProperty, palette.ControlBackground));
            style.Setters.Add(new Setter(Control.ForegroundProperty, palette.TextBrush));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(6, 3, 6, 3)));
            style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
            style.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
            style.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 26.0));
            style.Setters.Add(new Setter(Control.TemplateProperty, ComboBoxItemTemplate(palette)));

            var highlighted = new Trigger { Property = ComboBoxItem.IsHighlightedProperty, Value = true };
            highlighted.Setters.Add(new Setter(Control.BackgroundProperty, palette.SelectionBrush));
            highlighted.Setters.Add(new Setter(Control.ForegroundProperty, palette.TextBrush));
            style.Triggers.Add(highlighted);

            var selected = new Trigger { Property = Selector.IsSelectedProperty, Value = true };
            selected.Setters.Add(new Setter(Control.BackgroundProperty, palette.SelectionBrush));
            selected.Setters.Add(new Setter(Control.ForegroundProperty, palette.TextBrush));
            style.Triggers.Add(selected);

            var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
            disabled.Setters.Add(new Setter(Control.ForegroundProperty, palette.DisabledTextBrush));
            style.Triggers.Add(disabled);
            return style;
        }

        private static ControlTemplate ComboBoxItemTemplate(RevitUiPalette palette)
        {
            var root = new FrameworkElementFactory(typeof(Border));
            root.Name = "RootBorder";
            root.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            root.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));

            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
            content.SetValue(FrameworkElement.HorizontalAlignmentProperty, new TemplateBindingExtension(Control.HorizontalContentAlignmentProperty));
            content.SetValue(FrameworkElement.VerticalAlignmentProperty, new TemplateBindingExtension(Control.VerticalContentAlignmentProperty));
            content.SetBinding(ContentPresenter.ContentProperty, new Binding("Content") { RelativeSource = RelativeSource.TemplatedParent });
            content.SetBinding(ContentPresenter.ContentTemplateProperty, new Binding("ContentTemplate") { RelativeSource = RelativeSource.TemplatedParent });
            content.SetBinding(ContentPresenter.ContentTemplateSelectorProperty, new Binding("ContentTemplateSelector") { RelativeSource = RelativeSource.TemplatedParent });
            content.SetBinding(ContentPresenter.ContentStringFormatProperty, new Binding("ContentStringFormat") { RelativeSource = RelativeSource.TemplatedParent });
            root.AppendChild(content);

            var template = new ControlTemplate(typeof(ComboBoxItem)) { VisualTree = root };

            var highlighted = new Trigger { Property = ComboBoxItem.IsHighlightedProperty, Value = true };
            highlighted.Setters.Add(new Setter(Border.BackgroundProperty, palette.SelectionBrush, "RootBorder"));
            highlighted.Setters.Add(new Setter(Control.ForegroundProperty, palette.TextBrush));
            template.Triggers.Add(highlighted);

            var selected = new Trigger { Property = Selector.IsSelectedProperty, Value = true };
            selected.Setters.Add(new Setter(Border.BackgroundProperty, palette.SelectionBrush, "RootBorder"));
            selected.Setters.Add(new Setter(Control.ForegroundProperty, palette.TextBrush));
            template.Triggers.Add(selected);

            var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
            disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.58, "RootBorder"));
            disabled.Setters.Add(new Setter(Control.ForegroundProperty, palette.DisabledTextBrush));
            template.Triggers.Add(disabled);
            return template;
        }

        private static Style CheckBoxStyle(RevitUiPalette palette)
        {
            var style = new Style(typeof(CheckBox));
            style.Setters.Add(new Setter(Control.ForegroundProperty, palette.TextBrush));
            style.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
            return style;
        }

        private static Style TabControlStyle(RevitUiPalette palette)
        {
            var style = new Style(typeof(TabControl));
            style.Setters.Add(new Setter(Control.BackgroundProperty, palette.WindowBackground));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, palette.BorderBrush));
            return style;
        }

        private static Style TabItemStyle(RevitUiPalette palette)
        {
            var style = new Style(typeof(TabItem));
            style.Setters.Add(new Setter(Control.BackgroundProperty, palette.SurfaceBackground));
            style.Setters.Add(new Setter(Control.ForegroundProperty, palette.MutedTextBrush));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, palette.BorderBrush));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(12, 5, 12, 5)));
            style.Setters.Add(new Setter(Control.TemplateProperty, TabItemTemplate(palette)));

            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(Control.BackgroundProperty, palette.ControlHoverBackground));
            hover.Setters.Add(new Setter(Control.ForegroundProperty, palette.TextBrush));
            style.Triggers.Add(hover);

            var selected = new Trigger { Property = Selector.IsSelectedProperty, Value = true };
            selected.Setters.Add(new Setter(Control.BackgroundProperty, palette.PanelBackground));
            selected.Setters.Add(new Setter(Control.ForegroundProperty, palette.TextBrush));
            selected.Setters.Add(new Setter(Control.BorderBrushProperty, palette.AccentBrush));
            style.Triggers.Add(selected);
            return style;
        }

        private static ControlTemplate TabItemTemplate(RevitUiPalette palette)
        {
            var root = new FrameworkElementFactory(typeof(Border));
            root.Name = "RootBorder";
            root.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            root.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
            root.SetValue(Border.BorderThicknessProperty, new Thickness(1, 1, 1, 0));
            root.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));

            var header = new FrameworkElementFactory(typeof(ContentPresenter));
            header.Name = "HeaderHost";
            header.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
            header.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            header.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            header.SetBinding(ContentPresenter.ContentProperty, new Binding("Header") { RelativeSource = RelativeSource.TemplatedParent });
            header.SetBinding(ContentPresenter.ContentTemplateProperty, new Binding("HeaderTemplate") { RelativeSource = RelativeSource.TemplatedParent });
            header.SetBinding(ContentPresenter.ContentStringFormatProperty, new Binding("HeaderStringFormat") { RelativeSource = RelativeSource.TemplatedParent });
            root.AppendChild(header);

            var template = new ControlTemplate(typeof(TabItem)) { VisualTree = root };

            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(Border.BackgroundProperty, palette.ControlHoverBackground, "RootBorder"));
            hover.Setters.Add(new Setter(Control.ForegroundProperty, palette.TextBrush));
            template.Triggers.Add(hover);

            var selected = new Trigger { Property = Selector.IsSelectedProperty, Value = true };
            selected.Setters.Add(new Setter(Border.BackgroundProperty, palette.PanelBackground, "RootBorder"));
            selected.Setters.Add(new Setter(Border.BorderBrushProperty, palette.AccentBrush, "RootBorder"));
            selected.Setters.Add(new Setter(Control.ForegroundProperty, palette.TextBrush));
            template.Triggers.Add(selected);

            var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
            disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.58, "RootBorder"));
            template.Triggers.Add(disabled);
            return template;
        }

        private static Style DataGridStyle(RevitUiPalette palette)
        {
            var style = new Style(typeof(DataGrid));
            style.Setters.Add(new Setter(Control.BackgroundProperty, palette.PanelBackground));
            style.Setters.Add(new Setter(Control.ForegroundProperty, palette.TextBrush));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, palette.BorderBrush));
            style.Setters.Add(new Setter(DataGrid.AlternatingRowBackgroundProperty, palette.AlternatingRowBrush));
            style.Setters.Add(new Setter(DataGrid.RowBackgroundProperty, palette.PanelBackground));
            style.Setters.Add(new Setter(DataGrid.GridLinesVisibilityProperty, DataGridGridLinesVisibility.Horizontal));
            return style;
        }

        private static Style DataGridRowStyle(RevitUiPalette palette)
        {
            var style = new Style(typeof(DataGridRow));
            style.Setters.Add(new Setter(Control.BackgroundProperty, palette.PanelBackground));
            style.Setters.Add(new Setter(Control.ForegroundProperty, palette.TextBrush));

            var selected = new Trigger { Property = DataGridRow.IsSelectedProperty, Value = true };
            selected.Setters.Add(new Setter(Control.BackgroundProperty, palette.SelectionBrush));
            selected.Setters.Add(new Setter(Control.ForegroundProperty, palette.TextBrush));
            style.Triggers.Add(selected);
            return style;
        }

        private static Style DataGridColumnHeaderStyle(RevitUiPalette palette)
        {
            var style = new Style(typeof(DataGridColumnHeader));
            style.Setters.Add(new Setter(Control.BackgroundProperty, palette.HeaderBackground));
            style.Setters.Add(new Setter(Control.ForegroundProperty, palette.TextBrush));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, palette.BorderBrush));
            style.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(6, 4, 6, 4)));
            return style;
        }

        private static Style ListBoxStyle(RevitUiPalette palette)
        {
            var style = new Style(typeof(ListBox));
            style.Setters.Add(new Setter(Control.BackgroundProperty, palette.PanelBackground));
            style.Setters.Add(new Setter(Control.ForegroundProperty, palette.TextBrush));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, palette.BorderBrush));
            return style;
        }

        private static Style ListBoxItemStyle(RevitUiPalette palette)
        {
            var style = new Style(typeof(ListBoxItem));
            style.Setters.Add(new Setter(Control.ForegroundProperty, palette.TextBrush));

            var selected = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
            selected.Setters.Add(new Setter(Control.BackgroundProperty, palette.SelectionBrush));
            style.Triggers.Add(selected);
            return style;
        }

        private static Style ContextMenuStyle(RevitUiPalette palette)
        {
            var style = new Style(typeof(ContextMenu));
            style.Setters.Add(new Setter(Control.BackgroundProperty, palette.PanelBackground));
            style.Setters.Add(new Setter(Control.ForegroundProperty, palette.TextBrush));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, palette.BorderBrush));
            return style;
        }

        private static Style MenuItemStyle(RevitUiPalette palette)
        {
            var style = new Style(typeof(MenuItem));
            style.Setters.Add(new Setter(Control.ForegroundProperty, palette.TextBrush));
            style.Setters.Add(new Setter(Control.BackgroundProperty, palette.PanelBackground));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 4, 14, 4)));
            style.Setters.Add(new Setter(Control.TemplateProperty, MenuItemTemplate(palette)));

            var hover = new Trigger { Property = MenuItem.IsHighlightedProperty, Value = true };
            hover.Setters.Add(new Setter(Control.BackgroundProperty, palette.SelectionBrush));
            style.Triggers.Add(hover);
            return style;
        }

        private static ControlTemplate MenuItemTemplate(RevitUiPalette palette)
        {
            var root = new FrameworkElementFactory(typeof(Border));
            root.Name = "RootBorder";
            root.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));

            var grid = new FrameworkElementFactory(typeof(Grid));
            grid.SetValue(FrameworkElement.MinWidthProperty, 150.0);
            root.AppendChild(grid);

            var contentColumn = new FrameworkElementFactory(typeof(ColumnDefinition));
            contentColumn.SetValue(ColumnDefinition.WidthProperty, new GridLength(1, GridUnitType.Star));
            grid.AppendChild(contentColumn);

            var arrowColumn = new FrameworkElementFactory(typeof(ColumnDefinition));
            arrowColumn.SetValue(ColumnDefinition.WidthProperty, new GridLength(18));
            grid.AppendChild(arrowColumn);

            var header = new FrameworkElementFactory(typeof(ContentPresenter));
            header.Name = "HeaderHost";
            header.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
            header.SetValue(FrameworkElement.MarginProperty, new TemplateBindingExtension(Control.PaddingProperty));
            header.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            header.SetBinding(ContentPresenter.ContentProperty, new Binding("Header") { RelativeSource = RelativeSource.TemplatedParent });
            header.SetBinding(ContentPresenter.ContentTemplateProperty, new Binding("HeaderTemplate") { RelativeSource = RelativeSource.TemplatedParent });
            header.SetBinding(ContentPresenter.ContentStringFormatProperty, new Binding("HeaderStringFormat") { RelativeSource = RelativeSource.TemplatedParent });
            grid.AppendChild(header);

            var arrow = new FrameworkElementFactory(typeof(TextBlock));
            arrow.Name = "SubmenuArrow";
            arrow.SetValue(Grid.ColumnProperty, 1);
            arrow.SetValue(TextBlock.TextProperty, ">");
            arrow.SetValue(TextBlock.ForegroundProperty, palette.MutedTextBrush);
            arrow.SetValue(TextBlock.FontSizeProperty, 11.0);
            arrow.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            arrow.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            arrow.SetValue(UIElement.VisibilityProperty, Visibility.Collapsed);
            grid.AppendChild(arrow);

            var popup = new FrameworkElementFactory(typeof(Popup));
            popup.Name = "PART_Popup";
            popup.SetValue(Popup.AllowsTransparencyProperty, true);
            popup.SetValue(Popup.FocusableProperty, false);
            popup.SetValue(Popup.PlacementProperty, PlacementMode.Right);
            popup.SetValue(Popup.PopupAnimationProperty, PopupAnimation.Fade);
            popup.SetBinding(Popup.IsOpenProperty, new Binding("IsSubmenuOpen") { RelativeSource = RelativeSource.TemplatedParent });

            var popupBorder = new FrameworkElementFactory(typeof(Border));
            popupBorder.SetValue(Border.BackgroundProperty, palette.PanelBackground);
            popupBorder.SetValue(Border.BorderBrushProperty, palette.BorderBrush);
            popupBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            popup.AppendChild(popupBorder);

            var scroll = new FrameworkElementFactory(typeof(ScrollViewer));
            scroll.SetValue(ScrollViewer.CanContentScrollProperty, true);
            popupBorder.AppendChild(scroll);

            var presenter = new FrameworkElementFactory(typeof(ItemsPresenter));
            presenter.SetValue(KeyboardNavigation.DirectionalNavigationProperty, KeyboardNavigationMode.Cycle);
            scroll.AppendChild(presenter);
            grid.AppendChild(popup);

            var template = new ControlTemplate(typeof(MenuItem)) { VisualTree = root };

            var highlighted = new Trigger { Property = MenuItem.IsHighlightedProperty, Value = true };
            highlighted.Setters.Add(new Setter(Border.BackgroundProperty, palette.SelectionBrush, "RootBorder"));
            template.Triggers.Add(highlighted);

            var hasItems = new Trigger { Property = ItemsControl.HasItemsProperty, Value = true };
            hasItems.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Visible, "SubmenuArrow"));
            template.Triggers.Add(hasItems);

            var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
            disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.55, "RootBorder"));
            template.Triggers.Add(disabled);

            return template;
        }

        private static bool IsDarkTheme()
        {
            bool isDark;
            if (TryReadRevitTheme(out isDark))
            {
                return isDark;
            }

            return WindowsAppsUseDarkTheme();
        }

        private static bool TryReadRevitTheme(out bool isDark)
        {
            isDark = false;
            var managerType = Type.GetType("Autodesk.Revit.UI.UIThemeManager, RevitAPIUI", false);
            if (managerType == null) return false;

            foreach (var propertyName in new[] { "CurrentTheme", "Theme", "UITheme", "IsDarkTheme", "IsDarkMode" })
            {
                var property = managerType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static);
                if (property == null || property.GetIndexParameters().Length != 0) continue;
                try
                {
                    if (TryParseThemeValue(property.GetValue(null, null), out isDark))
                    {
                        return true;
                    }
                }
                catch (Exception)
                {
                    return false;
                }
            }

            foreach (var methodName in new[] { "GetCurrentTheme", "GetTheme" })
            {
                var method = managerType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
                if (method == null) continue;
                try
                {
                    if (TryParseThemeValue(method.Invoke(null, null), out isDark))
                    {
                        return true;
                    }
                }
                catch (Exception)
                {
                    return false;
                }
            }

            return false;
        }

        private static bool TryParseThemeValue(object? value, out bool isDark)
        {
            isDark = false;
            if (value is bool boolValue)
            {
                isDark = boolValue;
                return true;
            }

            var text = Convert.ToString(value) ?? string.Empty;
            if (text.IndexOf("Dark", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                isDark = true;
                return true;
            }

            if (text.IndexOf("Light", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                isDark = false;
                return true;
            }

            return false;
        }

        private static bool WindowsAppsUseDarkTheme()
        {
            try
            {
                var value = Registry.GetValue(
                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                    "AppsUseLightTheme",
                    1);
                if (value is int intValue)
                {
                    return intValue == 0;
                }

                var text = Convert.ToString(value) ?? string.Empty;
                int parsed;
                return int.TryParse(text, out parsed) && parsed == 0;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
