﻿using GPX;
using LolloGPS.Data;
using LolloGPS.Data.Runtime;
using LolloGPS.GPSInteraction;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Storage.Pickers;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace LolloGPS.Core
{
    public sealed partial class PointsPanel : UserControl, INotifyPropertyChanged
    {
        public PersistentData PersistentData { get { return App.PersistentData; } }
        public RuntimeData MyRuntimeData { get { return App.MyRuntimeData; } }

        private Main_VM _myVM = Main_VM.GetInstance();
        public Main_VM MyVM { get { return _myVM; } }
        
        #region events       
        public event PropertyChangedEventHandler PropertyChanged;
        private void RaisePropertyChanged([CallerMemberName] String propertyName = "")
        {
            var listener = PropertyChanged;
            if (listener != null)
            {
                listener(this, new PropertyChangedEventArgs(propertyName));
            }
        }
        public event EventHandler CentreOnTargetRequested;
        private void RaiseCentreOnTarget()
        {
            var listener = CentreOnTargetRequested;
            if (listener != null)
            {
                listener(this, EventArgs.Empty);
            }
        }
        public event EventHandler CentreOnLandmarksRequested;
        private void RaiseCentreOnLandmarks()
        {
            var listener = CentreOnLandmarksRequested;
            if (listener != null)
            {
                listener(this, EventArgs.Empty);
            }
        }
        #endregion events

        public PointsPanel()
        {
            this.InitializeComponent();
        }

        #region event handlers
        private void LatLon_GotFocus(object sender, RoutedEventArgs e)
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

        private void LatFloat_LostFocus(object sender, RoutedEventArgs e)
        {
            Double dbl;
            if (Double.TryParse(LatFloat.Text, out dbl) && dbl >= -90.0 && dbl <= 90) PersistentData.Target.Latitude = dbl;
            else if (String.IsNullOrWhiteSpace(LatFloat.Text)) PersistentData.Target.Latitude = 0.0;
            else PersistentData.Target.Latitude = PersistentData.Target.Latitude;
        }

        private void LonFloat_LostFocus(object sender, RoutedEventArgs e)
        {
            Double dbl;
            if (Double.TryParse(LonFloat.Text, out dbl) && dbl >= -180.0 && dbl <= 180) PersistentData.Target.Longitude = dbl;
            else if (String.IsNullOrWhiteSpace(LonFloat.Text)) PersistentData.Target.Longitude = 0.0;
            else PersistentData.Target.Longitude = PersistentData.Target.Longitude;
        }

        private void LatMin_LostFocus(object sender, RoutedEventArgs e)
        {
            Int32 intg;
            if (Int32.TryParse(LatMin.Text, out intg) && intg >= 0 && intg < 60)
                PersistentData.Target.Latitude = AngleConverterHelper.DegMinSecDec_To_Float(LatDeg.Text, LatMin.Text, LatSec.Text, LatDec.Text);
            else if (String.IsNullOrWhiteSpace(LatMin.Text))
                PersistentData.Target.Latitude = AngleConverterHelper.DegMinSecDec_To_Float(LatDeg.Text, "0", LatSec.Text, LatDec.Text);
            else PersistentData.Target.Latitude = PersistentData.Target.Latitude;
        }

        private void LatSec_LostFocus(object sender, RoutedEventArgs e)
        {
            Int32 intg;
            if (Int32.TryParse(LatSec.Text, out intg) && intg >= 0 && intg < 60)
                PersistentData.Target.Latitude = AngleConverterHelper.DegMinSecDec_To_Float(LatDeg.Text, LatMin.Text, LatSec.Text, LatDec.Text);
            else if (String.IsNullOrWhiteSpace(LatSec.Text))
                PersistentData.Target.Latitude = AngleConverterHelper.DegMinSecDec_To_Float(LatDeg.Text, LatMin.Text, "0", LatDec.Text);
            else PersistentData.Target.Latitude = PersistentData.Target.Latitude;
        }

        private void LatDec_LostFocus(object sender, RoutedEventArgs e)
        {
            Int32 intg;
            if (Int32.TryParse(LatDec.Text, out intg) && intg >= 0)
                PersistentData.Target.Latitude = AngleConverterHelper.DegMinSecDec_To_Float(LatDeg.Text, LatMin.Text, LatSec.Text, LatDec.Text);
            else if (String.IsNullOrWhiteSpace(LatDec.Text))
                PersistentData.Target.Latitude = AngleConverterHelper.DegMinSecDec_To_Float(LatDeg.Text, LatMin.Text, LatSec.Text, "0");
            else PersistentData.Target.Latitude = PersistentData.Target.Latitude;
        }

        private void LatDeg_LostFocus(object sender, RoutedEventArgs e)
        {
            Int32 intg;
            if (Int32.TryParse(LatDeg.Text, out intg) && intg >= -90 && intg <= 90)
                PersistentData.Target.Latitude = AngleConverterHelper.DegMinSecDec_To_Float(LatDeg.Text, LatMin.Text, LatSec.Text, LatDec.Text);
            else if (String.IsNullOrWhiteSpace(LatDeg.Text))
                PersistentData.Target.Latitude = AngleConverterHelper.DegMinSecDec_To_Float("0", LatMin.Text, LatSec.Text, LatDec.Text);
            else PersistentData.Target.Latitude = PersistentData.Target.Latitude;
        }

        private void LonMin_LostFocus(object sender, RoutedEventArgs e)
        {
            Int32 intg;
            if (Int32.TryParse(LonMin.Text, out intg) && intg >= 0 && intg < 60)
                PersistentData.Target.Longitude = AngleConverterHelper.DegMinSecDec_To_Float(LonDeg.Text, LonMin.Text, LonSec.Text, LonDec.Text);
            else if (String.IsNullOrWhiteSpace(LonMin.Text))
                PersistentData.Target.Longitude = AngleConverterHelper.DegMinSecDec_To_Float(LonDeg.Text, "0", LonSec.Text, LonDec.Text);
            else PersistentData.Target.Longitude = PersistentData.Target.Longitude;
        }

        private void LonSec_LostFocus(object sender, RoutedEventArgs e)
        {
            Int32 intg;
            if (Int32.TryParse(LonSec.Text, out intg) && intg >= 0 && intg < 60)
                PersistentData.Target.Longitude = AngleConverterHelper.DegMinSecDec_To_Float(LonDeg.Text, LonMin.Text, LonSec.Text, LonDec.Text);
            else if (String.IsNullOrWhiteSpace(LonSec.Text))
                PersistentData.Target.Longitude = AngleConverterHelper.DegMinSecDec_To_Float(LonDeg.Text, LonMin.Text, "0", LonDec.Text);
            else PersistentData.Target.Longitude = PersistentData.Target.Longitude;
        }

        private void LonDec_LostFocus(object sender, RoutedEventArgs e)
        {
            Int32 intg;
            if (Int32.TryParse(LonDec.Text, out intg) && intg >= 0)
                PersistentData.Target.Longitude = AngleConverterHelper.DegMinSecDec_To_Float(LonDeg.Text, LonMin.Text, LonSec.Text, LonDec.Text);
            else if (String.IsNullOrWhiteSpace(LonDec.Text))
                PersistentData.Target.Longitude = AngleConverterHelper.DegMinSecDec_To_Float(LonDeg.Text, LonMin.Text, LonSec.Text, "0");
            else PersistentData.Target.Longitude = PersistentData.Target.Longitude;
        }

        private void LonDeg_LostFocus(object sender, RoutedEventArgs e)
        {
            Int32 intg;
            if (Int32.TryParse(LonDeg.Text, out intg) && intg >= -180 && intg <= 180)
                PersistentData.Target.Longitude = AngleConverterHelper.DegMinSecDec_To_Float(LonDeg.Text, LonMin.Text, LonSec.Text, LonDec.Text);
            else if (String.IsNullOrWhiteSpace(LonDeg.Text))
                PersistentData.Target.Longitude = AngleConverterHelper.DegMinSecDec_To_Float("0", LonMin.Text, LonSec.Text, LonDec.Text);
            else PersistentData.Target.Longitude = PersistentData.Target.Longitude;
        }

        private void OnGotoTarget_Click(object sender, RoutedEventArgs e)
        {
            RaiseCentreOnTarget();
        }

        private async void OnSetTargetToCurrentPoint_Click(object sender, RoutedEventArgs e)
        {
            GPSInteractor gpsInteractor = new GPSInteractor(PersistentData);
            if (gpsInteractor != null)
            {
                Task vibrate = Task.Run(() => App.ShortVibration());
                var geoPosTuple = await gpsInteractor.GetGeoLocationAsync();
                if (geoPosTuple != null && PersistentData != null)
                {
                    //if (geoPosTuple.Item1) // disable bkg task if the app has no access to location? No, the user might grant it later
                    //{
                    //    Task dis = CoreApplication.MainView.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () => PersistentData.IsBackgroundEnabled = false).AsTask();
                    //}
                    //else
                    //{
                        Task uuu = PersistentData.TryAddPointToLandmarksAsync(geoPosTuple.Item2);
                    //}
                }
            }
        }

        private void OnAddTargetToLandmarks_Click(object sender, RoutedEventArgs e)
        {
            Task uuu = PersistentData.TryAddTargetToLandmarksAsync();
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

        #endregion event handlers
    }
}