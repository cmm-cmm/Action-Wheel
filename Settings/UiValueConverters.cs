using System;
using Action_Wheel.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Action_Wheel.Settings
{
    public sealed class ColorToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language) =>
            new SolidColorBrush(value is Color color ? color : Color.FromArgb(0, 0, 0, 0));
        public object ConvertBack(object value, Type targetType, object parameter, string language) =>
            throw new NotSupportedException();
    }

    public sealed class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool visible = value is true;
            if (string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase)) visible = !visible;
            return visible ? Visibility.Visible : Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language) =>
            throw new NotSupportedException();
    }

    public sealed class IconPreviewConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, string language) =>
            value is ActionEditModel model
                ? IconFactory.CreateIcon(model.ToActionItem(), 18, new SolidColorBrush(model.ForegroundColor))
                : null;
        public object ConvertBack(object value, Type targetType, object parameter, string language) =>
            throw new NotSupportedException();
    }
}
