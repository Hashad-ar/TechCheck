using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using TechCheck.Core;

namespace TechCheck.UI;

public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool flag && !flag;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class DiagnosticStatusBrushConverter : IValueConverter
{
    private static readonly Brush Good = Freeze("#58C894");
    private static readonly Brush Warning = Freeze("#E4B65A");
    private static readonly Brush Failed = Freeze("#E06C75");
    private static readonly Brush Unavailable = Freeze("#8591A0");

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        DiagnosticStatus.Good => Good,
        DiagnosticStatus.Warning => Warning,
        DiagnosticStatus.Failed => Failed,
        _ => Unavailable,
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static Brush Freeze(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }
}

public sealed class DiagnosticStatusGlyphConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        DiagnosticStatus.Good => "✓",
        DiagnosticStatus.Warning => "!",
        DiagnosticStatus.Failed => "×",
        _ => "—",
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
