using System;
using Action_Wheel.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;

namespace Action_Wheel.Settings
{
    public sealed class StatusLevelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language) => value switch
        {
            StatusLevel.Success => InfoBarSeverity.Success,
            StatusLevel.Warning => InfoBarSeverity.Warning,
            StatusLevel.Error => InfoBarSeverity.Error,
            _ => InfoBarSeverity.Informational,
        };

        public object ConvertBack(object value, Type targetType, object parameter, string language) =>
            throw new NotSupportedException();
    }
}
