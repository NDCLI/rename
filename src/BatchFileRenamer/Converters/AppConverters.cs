using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using BatchFileRenamer.Models;

namespace BatchFileRenamer.Converters
{
    public class StatusToBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush PendingBrush = new((Color)ColorConverter.ConvertFromString("#94A3B8")); // Gray
        private static readonly SolidColorBrush ValidBrush = new((Color)ColorConverter.ConvertFromString("#10B981"));   // Green
        private static readonly SolidColorBrush ConflictBrush = new((Color)ColorConverter.ConvertFromString("#EF4444"));// Red
        private static readonly SolidColorBrush RenamedBrush = new((Color)ColorConverter.ConvertFromString("#3B82F6")); // Blue
        private static readonly SolidColorBrush RolledBackBrush = new((Color)ColorConverter.ConvertFromString("#F59E0B")); // Amber
        private static readonly SolidColorBrush SkippedBrush = new((Color)ColorConverter.ConvertFromString("#64748B")); // Slate

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is RenameStatus status)
            {
                return status switch
                {
                    RenameStatus.Valid => ValidBrush,
                    RenameStatus.Conflict or RenameStatus.Failed => ConflictBrush,
                    RenameStatus.Renamed => RenamedBrush,
                    RenameStatus.RolledBack => RolledBackBrush,
                    RenameStatus.Skipped => SkippedBrush,
                    _ => PendingBrush
                };
            }

            return PendingBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw exoticException();
        private Exception exoticException() => new NotImplementedException();
    }

    public class StatusToBackgroundBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush PendingBg = new((Color)ColorConverter.ConvertFromString("#1E293B"));
        private static readonly SolidColorBrush ValidBg = new((Color)ColorConverter.ConvertFromString("#064E3B"));   // Dark Green
        private static readonly SolidColorBrush ConflictBg = new((Color)ColorConverter.ConvertFromString("#7F1D1D"));// Dark Red
        private static readonly SolidColorBrush RenamedBg = new((Color)ColorConverter.ConvertFromString("#1E3A8A")); // Dark Blue
        private static readonly SolidColorBrush RolledBackBg = new((Color)ColorConverter.ConvertFromString("#78350F"));// Dark Amber

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is RenameStatus status)
            {
                return status switch
                {
                    RenameStatus.Valid => ValidBg,
                    RenameStatus.Conflict or RenameStatus.Failed => ConflictBg,
                    RenameStatus.Renamed => RenamedBg,
                    RenameStatus.RolledBack => RolledBackBg,
                    _ => PendingBg
                };
            }

            return PendingBg;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class StatusToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is RenameStatus status)
            {
                return status switch
                {
                    RenameStatus.Valid => "Hợp lệ",
                    RenameStatus.Conflict => "Xung đột",
                    RenameStatus.Failed => "Lỗi",
                    RenameStatus.Renamed => "Đã đổi tên",
                    RenameStatus.RolledBack => "Đã khôi phục",
                    RenameStatus.Skipped => "Bỏ qua",
                    _ => "Chờ xử lý"
                };
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class BooleanToVisibilityConverter : IValueConverter
    {
        public bool Invert { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool boolVal = value is bool b && b;
            if (Invert) boolVal = !boolVal;
            return boolVal ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b ? !b : true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b ? !b : true;
        }
    }

    public class PathToFileNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string path && !string.IsNullOrEmpty(path))
            {
                return Path.GetFileName(path);
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
