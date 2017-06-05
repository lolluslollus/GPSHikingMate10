using System;
using System.Threading.Tasks;
using Utilz;
using Utilz.Controlz;
using Windows.Devices.Sensors;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media.Imaging;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace GPSHikingMate10.Views
{
    public sealed partial class CompassControl : OpenableObservableControl
    {
        private const int MIN_MIN_REPORT_INTERVAL = 16;
        private Compass _compass;
        private int _desiredReportInterval;
        private DispatcherTimerPlus _dispatcherTimer;

        private static readonly BitmapImage BadImage = new BitmapImage(new Uri("ms-appx:///Assets/compass-outer-bad-90.png"));
        private static readonly BitmapImage MediumImage = new BitmapImage(new Uri("ms-appx:///Assets/compass-outer-medium-90.png"));
        private static readonly BitmapImage GoodImage = new BitmapImage(new Uri("ms-appx:///Assets/compass-outer-good-90.png"));

        public CompassControl()
        {
            this.InitializeComponent();
            _compass = Compass.GetDefault();
        }

        protected override Task OpenMayOverrideAsync(object args = null)
        {
            if (_compass != null)
            {
                LayoutRoot.Visibility = Visibility.Visible;
                // Select a report interval that is both suitable for the purposes of the app and supported by the sensor.
                // This value will be used later to activate the sensor.
                int minReportInterval = (int)_compass.MinimumReportInterval;
                _desiredReportInterval = minReportInterval > MIN_MIN_REPORT_INTERVAL ? minReportInterval : MIN_MIN_REPORT_INTERVAL;

                DisplayCurrentReading();
                _dispatcherTimer?.Dispose();
                _dispatcherTimer = new DispatcherTimerPlus(DisplayCurrentReading, _desiredReportInterval);
                _dispatcherTimer.Start();

                _compass.ReadingChanged += OnCompass_ReadingChanged;
            }
            else
            {
                LayoutRoot.Visibility = Visibility.Collapsed;
            }
            return Task.CompletedTask;
        }

        protected override Task CloseMayOverrideAsync(object args = null)
        {
            _dispatcherTimer?.Dispose();
            var compass = _compass; if (compass != null) compass.ReadingChanged -= OnCompass_ReadingChanged;
            return Task.CompletedTask;
        }

        private void OnCompass_ReadingChanged(Compass sender, CompassReadingChangedEventArgs args)
        {
            DisplayCurrentReading();
        }

        private void DisplayCurrentReading()
        {
            var compass = _compass;
            if (compass == null) return;

            try
            {
                var reading = compass.GetCurrentReading();

                if (reading == null || reading.HeadingTrueNorth == null)
                    SetHeadingNone();
                else
                {
                    SetHeading((double)reading.HeadingTrueNorth, reading.HeadingAccuracy);
                    //switch (reading.HeadingAccuracy)
                    //{
                    //    case MagnetometerAccuracy.Unknown:
                    //        SetHeadingNone();
                    //        break;
                    //    case MagnetometerAccuracy.Unreliable:
                    //        SetHeadingNone();
                    //        break;
                    //    case MagnetometerAccuracy.Approximate:
                    //        SetHeading((double)reading.HeadingTrueNorth, true);
                    //        break;
                    //    case MagnetometerAccuracy.High:
                    //        SetHeading((double)reading.HeadingTrueNorth, false);
                    //        break;
                    //    default:
                    //        SetHeadingNone();
                    //        break;
                    //}
                }
            }
            catch { }
        }
        private async void SetHeadingNone()
        {
            await RunInUiThreadAsync(() => { Heading.Rotation = 0.0; LayoutRoot.Visibility = Visibility.Collapsed; }).ConfigureAwait(false);
        }
        //private async void SetHeading(double newValue, bool isUncertain)
        //{
        //	await RunInUiThreadAsync(() => { Heading.Rotation = -newValue; LayoutRoot.Visibility = Visibility.Visible; LayoutRoot.Opacity = isUncertain ? .5 : 1.0; }).ConfigureAwait(false);
        //}
        private async void SetHeading(double newValue, MagnetometerAccuracy accuracy)
        {
            BitmapImage source = null;
            switch (accuracy)
            {
                case MagnetometerAccuracy.High:
                    source = GoodImage;
                    break;
                case MagnetometerAccuracy.Approximate:
                    source = MediumImage;
                    break;
                default:
                    source = BadImage;
                    break;
            }
            await RunInUiThreadAsync(() => { Heading.Rotation = -newValue; LayoutRoot.Visibility = Visibility.Visible; MyImage.Source = source; }).ConfigureAwait(false);
        }
    }
}
