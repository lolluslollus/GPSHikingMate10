using System;
using System.Collections.ObjectModel;
using System.Globalization;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
using System.Diagnostics;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI;

namespace LolloGPS.Controlz
{
    public class IsSelectedToBackgroundConverter : IValueConverter
    {
        public static readonly SolidColorBrush SelectedBrush = ((SolidColorBrush)(Application.Current.Resources["SystemControlHighlightListAccentLowBrush"]));
        public static readonly SolidColorBrush UnselectedBrush = ((SolidColorBrush)(Application.Current.Resources["FlyoutBackgroundThemeBrush"]));

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (!(value is bool) || !((bool)value)) return UnselectedBrush;
            return SelectedBrush;
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new Exception("this is a one-way binding, it should never come here");
        }
    }
}