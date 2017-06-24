using LolloGPS.Converters;
using LolloGPS.Data;
using LolloGPS.Data.Runtime;
using LolloGPS.ViewModels;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace LolloGPS.Core
{
    public sealed partial class PointsPanel : Utilz.Controlz.ObservableControl
    {
        #region properties
        public PersistentData PersistentData { get { return App.PersistentData; } }
        public RuntimeData RuntimeData { get { return App.RuntimeData; } }

        public MainVM MainVM
        {
            get { return (MainVM)GetValue(MainVMProperty); }
            set { SetValue(MainVMProperty, value); }
        }
        public static readonly DependencyProperty MainVMProperty =
            DependencyProperty.Register("MainVM", typeof(MainVM), typeof(PointsPanel), new PropertyMetadata(null));
        #endregion properties


        #region lifecycle
        public PointsPanel()
        {
            InitializeComponent();
        }
        #endregion lifecycle


        #region event handlers
        private void OnLatLon_GotFocus(object sender, RoutedEventArgs e)
        {
            //these textboxes have a limited maxlength and the user must not be forced to delete the leading zero whenever they want to type in data
            TextBox tb = (sender as TextBox);
            try
            {
                while (tb.Text.Length > 0 && tb.Text.Substring(0, 1) == "0")
                {
                    tb.Text = tb.Text.Remove(0, 1);
                }
            }
            catch (Exception exc)
            {
                Debug.WriteLine("ERROR in LatLon_GotFocus: " + exc.ToString());
            }
        }

        private void OnLatSign_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            PersistentData.Target.Latitude = -PersistentData.Target.Latitude;
        }

        private void OnLonSign_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            PersistentData.Target.Longitude = -PersistentData.Target.Longitude;
        }


        private void OnLatFloat_LostFocus(object sender, RoutedEventArgs e)
        {
            double dbl;
            if (double.TryParse(LatFloat.Text, NumberStyles.AllowDecimalPoint, CultureInfo.CurrentUICulture, out dbl) && dbl >= 0.0 && dbl <= 90.0)
                PersistentData.Target.Latitude = dbl;
            else if (string.IsNullOrWhiteSpace(LatFloat.Text)) PersistentData.Target.Latitude = 0.0;
            else PersistentData.Target.Latitude = PersistentData.Target.Latitude;
        }

        private void OnLonFloat_LostFocus(object sender, RoutedEventArgs e)
        {
            double dbl;
            if (double.TryParse(LonFloat.Text, NumberStyles.AllowDecimalPoint, CultureInfo.CurrentUICulture, out dbl) && dbl >= 0.0 && dbl <= 180.0)
                PersistentData.Target.Longitude = dbl;
            else if (string.IsNullOrWhiteSpace(LonFloat.Text)) PersistentData.Target.Longitude = 0.0;
            else PersistentData.Target.Longitude = PersistentData.Target.Longitude;
        }


        private void OnLatDeg_LostFocus(object sender, RoutedEventArgs e)
        {
            int intg;
            if (int.TryParse(LatDeg.Text, NumberStyles.None, CultureInfo.CurrentUICulture, out intg) && intg >= 0 && intg <= 90)
                PersistentData.Target.Latitude = AngleConverterHelper.DegMinSecDec_To_Float(intg.ToString(CultureInfo.CurrentUICulture), LatMin.Text, LatSec.Text, LatDec.Text);
            else if (string.IsNullOrWhiteSpace(LatDeg.Text))
                PersistentData.Target.Latitude = AngleConverterHelper.DegMinSecDec_To_Float("0", LatMin.Text, LatSec.Text, LatDec.Text);
            else PersistentData.Target.Latitude = PersistentData.Target.Latitude;
        }

        private void OnLatMin_LostFocus(object sender, RoutedEventArgs e)
        {
            int intg;
            if (int.TryParse(LatMin.Text, NumberStyles.None, CultureInfo.CurrentUICulture, out intg) && intg >= 0 && intg < 60)
                PersistentData.Target.Latitude = AngleConverterHelper.DegMinSecDec_To_Float(LatDeg.Text, intg.ToString(CultureInfo.CurrentUICulture), LatSec.Text, LatDec.Text);
            else if (string.IsNullOrWhiteSpace(LatMin.Text))
                PersistentData.Target.Latitude = AngleConverterHelper.DegMinSecDec_To_Float(LatDeg.Text, "0", LatSec.Text, LatDec.Text);
            else PersistentData.Target.Latitude = PersistentData.Target.Latitude;
        }

        private void OnLatSec_LostFocus(object sender, RoutedEventArgs e)
        {
            int intg;
            if (int.TryParse(LatSec.Text, NumberStyles.None, CultureInfo.CurrentUICulture, out intg) && intg >= 0 && intg < 60)
                PersistentData.Target.Latitude = AngleConverterHelper.DegMinSecDec_To_Float(LatDeg.Text, LatMin.Text, intg.ToString(CultureInfo.CurrentUICulture), LatDec.Text);
            else if (string.IsNullOrWhiteSpace(LatSec.Text))
                PersistentData.Target.Latitude = AngleConverterHelper.DegMinSecDec_To_Float(LatDeg.Text, LatMin.Text, "0", LatDec.Text);
            else PersistentData.Target.Latitude = PersistentData.Target.Latitude;
        }

        private void OnLatDec_LostFocus(object sender, RoutedEventArgs e)
        {
            int intg;
            if (int.TryParse(LatDec.Text, NumberStyles.None, CultureInfo.CurrentUICulture, out intg) && intg >= 0)
                PersistentData.Target.Latitude = AngleConverterHelper.DegMinSecDec_To_Float(LatDeg.Text, LatMin.Text, LatSec.Text, intg.ToString(CultureInfo.CurrentUICulture));
            else if (string.IsNullOrWhiteSpace(LatDec.Text))
                PersistentData.Target.Latitude = AngleConverterHelper.DegMinSecDec_To_Float(LatDeg.Text, LatMin.Text, LatSec.Text, "0");
            else PersistentData.Target.Latitude = PersistentData.Target.Latitude;
        }


        private void OnLonDeg_LostFocus(object sender, RoutedEventArgs e)
        {
            int intg;
            if (int.TryParse(LonDeg.Text, NumberStyles.None, CultureInfo.CurrentUICulture, out intg) && intg >= 0 && intg <= 180)
                PersistentData.Target.Longitude = AngleConverterHelper.DegMinSecDec_To_Float(intg.ToString(CultureInfo.CurrentUICulture), LonMin.Text, LonSec.Text, LonDec.Text);
            else if (string.IsNullOrWhiteSpace(LonDeg.Text))
                PersistentData.Target.Longitude = AngleConverterHelper.DegMinSecDec_To_Float("0", LonMin.Text, LonSec.Text, LonDec.Text);
            else PersistentData.Target.Longitude = PersistentData.Target.Longitude;
        }

        private void OnLonMin_LostFocus(object sender, RoutedEventArgs e)
        {
            int intg;
            if (int.TryParse(LonMin.Text, NumberStyles.None, CultureInfo.CurrentUICulture, out intg) && intg >= 0 && intg < 60)
                PersistentData.Target.Longitude = AngleConverterHelper.DegMinSecDec_To_Float(LonDeg.Text, intg.ToString(CultureInfo.CurrentUICulture), LonSec.Text, LonDec.Text);
            else if (string.IsNullOrWhiteSpace(LonMin.Text))
                PersistentData.Target.Longitude = AngleConverterHelper.DegMinSecDec_To_Float(LonDeg.Text, "0", LonSec.Text, LonDec.Text);
            else PersistentData.Target.Longitude = PersistentData.Target.Longitude;
        }

        private void OnLonSec_LostFocus(object sender, RoutedEventArgs e)
        {
            int intg;
            if (int.TryParse(LonSec.Text, NumberStyles.None, CultureInfo.CurrentUICulture, out intg) && intg >= 0 && intg < 60)
                PersistentData.Target.Longitude = AngleConverterHelper.DegMinSecDec_To_Float(LonDeg.Text, LonMin.Text, intg.ToString(CultureInfo.CurrentUICulture), LonDec.Text);
            else if (string.IsNullOrWhiteSpace(LonSec.Text))
                PersistentData.Target.Longitude = AngleConverterHelper.DegMinSecDec_To_Float(LonDeg.Text, LonMin.Text, "0", LonDec.Text);
            else PersistentData.Target.Longitude = PersistentData.Target.Longitude;
        }

        private void OnLonDec_LostFocus(object sender, RoutedEventArgs e)
        {
            int intg;
            if (int.TryParse(LonDec.Text, NumberStyles.None, CultureInfo.CurrentUICulture, out intg) && intg >= 0)
                PersistentData.Target.Longitude = AngleConverterHelper.DegMinSecDec_To_Float(LonDeg.Text, LonMin.Text, LonSec.Text, intg.ToString(CultureInfo.CurrentUICulture));
            else if (string.IsNullOrWhiteSpace(LonDec.Text))
                PersistentData.Target.Longitude = AngleConverterHelper.DegMinSecDec_To_Float(LonDeg.Text, LonMin.Text, LonSec.Text, "0");
            else PersistentData.Target.Longitude = PersistentData.Target.Longitude;
        }


        private void OnHyperlink_Click(object sender, RoutedEventArgs e)
        {
            MainVM?.NavigateToUri(PersistentData?.Target?.HyperLink);
        }

        private void OnGotoTarget_Click(object sender, RoutedEventArgs e)
        {
            Task cen = MainVM?.CentreOnTargetAsync();
        }

        private void OnSetTargetToCurrentPoint_Click(object sender, RoutedEventArgs e)
        {
            Task set = MainVM?.SetTargetToCurrentAsync();
        }

        private void OnAddTargetToCheckpoints_Click(object sender, RoutedEventArgs e)
        {
            Task uuu = PersistentData.TryAddTargetCloneToCheckpointsAsync();
        }

        /// <summary>
        /// This method completes the binding of IsShowAim
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnAim_Click(object sender, RoutedEventArgs e)
        {
            if (MainVM.PersistentData.IsShowAim)
            {
                MainVM.PersistentData.IsShowAimOnce = false;
                MainVM.PersistentData.IsShowingPivot = false;
            }
        }
        /// <summary>
        /// This method completes the binding of IsShowAim
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnAimOnce_Click(object sender, RoutedEventArgs e)
        {
            if (MainVM.PersistentData.IsShowAim)
            {
                MainVM.PersistentData.IsShowAimOnce = true;
                MainVM.PersistentData.IsShowingPivot = false;
            }
        }

        private void OnClearCheckpoints_Click(object sender, RoutedEventArgs e)
        {
            Task clear = MainVM.PersistentData.ResetCheckpointsAsync();
        }

        private void OnSymbolCircle_Click(object sender, RoutedEventArgs e)
        {
            PersistentData.Target.Symbol = PersistentData.CheckpointSymbols.Circle;
        }

        private void OnSymbolCross_Click(object sender, RoutedEventArgs e)
        {
            PersistentData.Target.Symbol = PersistentData.CheckpointSymbols.Cross;
        }

        private void OnSymbolEcs_Click(object sender, RoutedEventArgs e)
        {
            PersistentData.Target.Symbol = PersistentData.CheckpointSymbols.Ecs;
        }

        private void OnSymbolSquare_Click(object sender, RoutedEventArgs e)
        {
            PersistentData.Target.Symbol = PersistentData.CheckpointSymbols.Square;
        }

        private void OnSymbolTriangle_Click(object sender, RoutedEventArgs e)
        {
            PersistentData.Target.Symbol = PersistentData.CheckpointSymbols.Triangle;
        }
        #endregion event handlers
    }
}