using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace LolloGPS.Controlz
{
    public static class ResourcesReader
    {
        public static readonly double AppBarButtonWidth;
        public static readonly double AppBarButtonEllipseDiametre;
        static ResourcesReader()
        {
            AppBarButtonWidth = (double)Application.Current.Resources["AppBarButtonWidth"];
            AppBarButtonEllipseDiametre = (double)Application.Current.Resources["AppBarButtonEllipseDiametre"];
        }
    }
    public class AppBarButtonReallyCompact : AppBarButton
    {
        //private static readonly double AppBarButtonWidth;
        //private static readonly double AppBarButtonEllipseDiametre;
        //static AppBarButtonReallyCompact()
        //{
        //    AppBarButtonWidth = (double)Application.Current.Resources["AppBarButtonWidth"];
        //    AppBarButtonEllipseDiametre = (double)Application.Current.Resources["AppBarButtonEllipseDiametre"];
        //}
        public AppBarButtonReallyCompact() : base()
        {
            Loading += OnLoading;
            //Loaded += OnLoaded;
        }

        //private void OnLoaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        //{
        //    Width = IsCompact ? AppBarButtonEllipseDiametre : AppBarButtonWidth;
        //}
        private void OnLoading(Windows.UI.Xaml.FrameworkElement sender, object args)
        {
            Width = IsCompact ? ResourcesReader.AppBarButtonEllipseDiametre : ResourcesReader.AppBarButtonWidth;
        }
    }

    public class AppBarToggleButtonReallyCompact : AppBarToggleButton
    {
        //private static readonly double AppBarButtonWidth;
        //private static readonly double AppBarButtonEllipseDiametre;
        //static AppBarToggleButtonReallyCompact()
        //{
        //    AppBarButtonWidth = (double)Application.Current.Resources["AppBarButtonWidth"];
        //    AppBarButtonEllipseDiametre = (double)Application.Current.Resources["AppBarButtonEllipseDiametre"];
        //}
        public AppBarToggleButtonReallyCompact() : base()
        {
            Loading += OnLoading;
            //Loaded += OnLoaded;
        }
        //private void OnLoaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        //{
        //    Width = IsCompact ? AppBarButtonEllipseDiametre : AppBarButtonWidth;
        //}
        private void OnLoading(Windows.UI.Xaml.FrameworkElement sender, object args)
        {
            Width = IsCompact ? ResourcesReader.AppBarButtonEllipseDiametre : ResourcesReader.AppBarButtonWidth;
        }
    }
}
