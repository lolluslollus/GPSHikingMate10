using LolloBaseUserControls;
using LolloGPS.Data;
using LolloGPS.Data.Runtime;
using LolloGPS.GPSInteraction;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace LolloGPS.Core
{
	public sealed partial class PointsPanel : ObservableControl
    {
		#region properties
		public PersistentData PersistentData { get { return App.PersistentData; } }
        public RuntimeData MyRuntimeData { get { return App.MyRuntimeData; } }

        private Main_VM _myVM = Main_VM.GetInstance();
        public Main_VM MyVM { get { return _myVM; } }
		#endregion properties

		#region events       
		public event EventHandler CentreOnTargetRequested;
        private void RaiseCentreOnTarget()
        {
            CentreOnTargetRequested?.Invoke(this, EventArgs.Empty);
        }
        public event EventHandler CentreOnLandmarksRequested;
        private void RaiseCentreOnLandmarks()
        {
            CentreOnLandmarksRequested?.Invoke(this, EventArgs.Empty);
        }
        #endregion events

        public PointsPanel()
        {
            InitializeComponent();
        }

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

        private void OnLatFloat_LostFocus(object sender, RoutedEventArgs e)
        {
            double dbl;
            if (double.TryParse(LatFloat.Text, out dbl) && dbl >= -90.0 && dbl <= 90) PersistentData.Target.Latitude = dbl;
            else if (string.IsNullOrWhiteSpace(LatFloat.Text)) PersistentData.Target.Latitude = 0.0;
            else PersistentData.Target.Latitude = PersistentData.Target.Latitude;
        }

        private void OnLonFloat_LostFocus(object sender, RoutedEventArgs e)
        {
            double dbl;
            if (double.TryParse(LonFloat.Text, out dbl) && dbl >= -180.0 && dbl <= 180) PersistentData.Target.Longitude = dbl;
            else if (string.IsNullOrWhiteSpace(LonFloat.Text)) PersistentData.Target.Longitude = 0.0;
            else PersistentData.Target.Longitude = PersistentData.Target.Longitude;
        }

        private void OnLatMin_LostFocus(object sender, RoutedEventArgs e)
        {
            int intg;
            if (int.TryParse(LatMin.Text, out intg) && intg >= 0 && intg < 60)
                PersistentData.Target.Latitude = AngleConverterHelper.DegMinSecDec_To_Float(LatDeg.Text, LatMin.Text, LatSec.Text, LatDec.Text);
            else if (string.IsNullOrWhiteSpace(LatMin.Text))
                PersistentData.Target.Latitude = AngleConverterHelper.DegMinSecDec_To_Float(LatDeg.Text, "0", LatSec.Text, LatDec.Text);
            else PersistentData.Target.Latitude = PersistentData.Target.Latitude;
        }

        private void OnLatSec_LostFocus(object sender, RoutedEventArgs e)
        {
            int intg;
            if (int.TryParse(LatSec.Text, out intg) && intg >= 0 && intg < 60)
                PersistentData.Target.Latitude = AngleConverterHelper.DegMinSecDec_To_Float(LatDeg.Text, LatMin.Text, LatSec.Text, LatDec.Text);
            else if (string.IsNullOrWhiteSpace(LatSec.Text))
                PersistentData.Target.Latitude = AngleConverterHelper.DegMinSecDec_To_Float(LatDeg.Text, LatMin.Text, "0", LatDec.Text);
            else PersistentData.Target.Latitude = PersistentData.Target.Latitude;
        }

        private void OnLatDec_LostFocus(object sender, RoutedEventArgs e)
        {
            int intg;
            if (int.TryParse(LatDec.Text, out intg) && intg >= 0)
                PersistentData.Target.Latitude = AngleConverterHelper.DegMinSecDec_To_Float(LatDeg.Text, LatMin.Text, LatSec.Text, LatDec.Text);
            else if (string.IsNullOrWhiteSpace(LatDec.Text))
                PersistentData.Target.Latitude = AngleConverterHelper.DegMinSecDec_To_Float(LatDeg.Text, LatMin.Text, LatSec.Text, "0");
            else PersistentData.Target.Latitude = PersistentData.Target.Latitude;
        }

        private void OnLatDeg_LostFocus(object sender, RoutedEventArgs e)
        {
            int intg;
            if (int.TryParse(LatDeg.Text, out intg) && intg >= -90 && intg <= 90)
                PersistentData.Target.Latitude = AngleConverterHelper.DegMinSecDec_To_Float(LatDeg.Text, LatMin.Text, LatSec.Text, LatDec.Text);
            else if (string.IsNullOrWhiteSpace(LatDeg.Text))
                PersistentData.Target.Latitude = AngleConverterHelper.DegMinSecDec_To_Float("0", LatMin.Text, LatSec.Text, LatDec.Text);
            else PersistentData.Target.Latitude = PersistentData.Target.Latitude;
        }

        private void OnLonMin_LostFocus(object sender, RoutedEventArgs e)
        {
            int intg;
            if (int.TryParse(LonMin.Text, out intg) && intg >= 0 && intg < 60)
                PersistentData.Target.Longitude = AngleConverterHelper.DegMinSecDec_To_Float(LonDeg.Text, LonMin.Text, LonSec.Text, LonDec.Text);
            else if (string.IsNullOrWhiteSpace(LonMin.Text))
                PersistentData.Target.Longitude = AngleConverterHelper.DegMinSecDec_To_Float(LonDeg.Text, "0", LonSec.Text, LonDec.Text);
            else PersistentData.Target.Longitude = PersistentData.Target.Longitude;
        }

        private void OnLonSec_LostFocus(object sender, RoutedEventArgs e)
        {
            int intg;
            if (int.TryParse(LonSec.Text, out intg) && intg >= 0 && intg < 60)
                PersistentData.Target.Longitude = AngleConverterHelper.DegMinSecDec_To_Float(LonDeg.Text, LonMin.Text, LonSec.Text, LonDec.Text);
            else if (string.IsNullOrWhiteSpace(LonSec.Text))
                PersistentData.Target.Longitude = AngleConverterHelper.DegMinSecDec_To_Float(LonDeg.Text, LonMin.Text, "0", LonDec.Text);
            else PersistentData.Target.Longitude = PersistentData.Target.Longitude;
        }

        private void OnLonDec_LostFocus(object sender, RoutedEventArgs e)
        {
            int intg;
            if (int.TryParse(LonDec.Text, out intg) && intg >= 0)
                PersistentData.Target.Longitude = AngleConverterHelper.DegMinSecDec_To_Float(LonDeg.Text, LonMin.Text, LonSec.Text, LonDec.Text);
            else if (string.IsNullOrWhiteSpace(LonDec.Text))
                PersistentData.Target.Longitude = AngleConverterHelper.DegMinSecDec_To_Float(LonDeg.Text, LonMin.Text, LonSec.Text, "0");
            else PersistentData.Target.Longitude = PersistentData.Target.Longitude;
        }

        private void OnLonDeg_LostFocus(object sender, RoutedEventArgs e)
        {
            int intg;
            if (int.TryParse(LonDeg.Text, out intg) && intg >= -180 && intg <= 180)
                PersistentData.Target.Longitude = AngleConverterHelper.DegMinSecDec_To_Float(LonDeg.Text, LonMin.Text, LonSec.Text, LonDec.Text);
            else if (string.IsNullOrWhiteSpace(LonDeg.Text))
                PersistentData.Target.Longitude = AngleConverterHelper.DegMinSecDec_To_Float("0", LonMin.Text, LonSec.Text, LonDec.Text);
            else PersistentData.Target.Longitude = PersistentData.Target.Longitude;
        }

        private void OnGotoTarget_Click(object sender, RoutedEventArgs e)
        {
            RaiseCentreOnTarget();
        }

        private async void OnSetTargetToCurrentPoint_Click(object sender, RoutedEventArgs e)
        {
            GPSInteractor gpsInteractor = GPSInteractor.GetInstance(PersistentData);
			if (gpsInteractor == null) return;

            Task vibrate = Task.Run(() => App.ShortVibration());
            var currrent = await gpsInteractor.GetGeoLocationAppendingHistoryAsync();
            if (currrent != null)
            {
				Task upd = currrent.UpdateHumanDescriptionAsync(PersistentData?.Target?.HumanDescription).ContinueWith(delegate
				{
					// this is not a clone, ie it is the same object that is added to the history a few lines above. This is meant to be a nice feature.
					Task add = PersistentData?.TryAddPointToLandmarksAsync(currrent);
				});
            }
        }

        private void OnAddTargetToLandmarks_Click(object sender, RoutedEventArgs e)
        {
            Task uuu = PersistentData.TryAddTargetCloneToLandmarksAsync();
        }

        private void OnAim_Click(object sender, RoutedEventArgs e)
        {
            if (MyVM.MyPersistentData.IsShowAim)
            {
                MyVM.MyPersistentData.IsShowAimOnce = false;
                MyVM.MyPersistentData.IsShowingPivot = false;
            }
        }
        private void OnAimOnce_Click(object sender, RoutedEventArgs e)
        {
            if (MyVM.MyPersistentData.IsShowAim)
            {
                MyVM.MyPersistentData.IsShowAimOnce = true;
                MyVM.MyPersistentData.IsShowingPivot = false;
            }
        }

		private void OnClearLandmarks_Click(object sender, RoutedEventArgs e)
		{
			Task clear = MyVM.MyPersistentData.ResetLandmarksAsync();
		}
		#endregion event handlers
	}
}
