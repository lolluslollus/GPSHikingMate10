using System;
using System.Threading.Tasks;
using Utilz;
using Windows.Devices.Sensors;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media.Imaging;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace LolloGPS.Controlz
{
    public sealed partial class CompassControl : Utilz.Controlz.OpenableObservableControl
    {
        private const int MIN_MIN_COMPASS_REPORT_INTERVAL = 16;
        private const int MIN_MIN_INCLINOMETER_REPORT_INTERVAL = 16;
        private const int MIN_USEFUL_COMPASS_REPORT_INTERVAL = 100;
        private const int MIN_USEFUL_INCLINOMETER_REPORT_INTERVAL = 100;
        private readonly Compass _compass;
        private readonly Inclinometer _inclinometer;
        private readonly int _desiredCompassReportInterval;
        private readonly int _desiredInclinometerReportInterval;
        private DispatcherTimerPlus _dispatcherTimer4Compass;
        private DispatcherTimerPlus _dispatcherTimer4Inclinometer;

        private static readonly BitmapImage BadImage = new BitmapImage(new Uri("ms-appx:///Assets/compass-outer-bad-90.png"));
        private static readonly BitmapImage MediumImage = new BitmapImage(new Uri("ms-appx:///Assets/compass-outer-medium-90.png"));
        private static readonly BitmapImage GoodImage = new BitmapImage(new Uri("ms-appx:///Assets/compass-outer-good-90.png"));

        public CompassControl()
        {
            this.InitializeComponent();
            _compass = Compass.GetDefault();
            _inclinometer = Inclinometer.GetDefault();
            // Select a report interval that is both suitable for the purposes of the app and supported by the sensor.
            // This value will be used later to activate the sensor.
            int minCompassReportInterval = (int)_compass.MinimumReportInterval;
            _desiredCompassReportInterval = minCompassReportInterval > MIN_USEFUL_COMPASS_REPORT_INTERVAL ? minCompassReportInterval : MIN_USEFUL_COMPASS_REPORT_INTERVAL;
            int minInclinometerReportInterval = (int)_inclinometer.MinimumReportInterval;
            _desiredInclinometerReportInterval = minInclinometerReportInterval > MIN_USEFUL_INCLINOMETER_REPORT_INTERVAL ? minInclinometerReportInterval : MIN_USEFUL_INCLINOMETER_REPORT_INTERVAL;
        }

        protected override Task OpenMayOverrideAsync(object args = null)
        {
            if (_compass != null)
            {
                LayoutRoot.Visibility = Visibility.Visible;

                DisplayCurrentCompassReading();
                _dispatcherTimer4Compass?.Dispose();
                _dispatcherTimer4Compass = new DispatcherTimerPlus(DisplayCurrentCompassReading, _desiredCompassReportInterval);
                _dispatcherTimer4Compass.Start();

                _compass.ReadingChanged += OnCompass_ReadingChanged;
            }
            else
            {
                LayoutRoot.Visibility = Visibility.Collapsed;
            }

            if (_inclinometer != null)
            {
                DisplayCurrentInclinometerReading();
                _dispatcherTimer4Inclinometer?.Dispose();
                _dispatcherTimer4Inclinometer = new DispatcherTimerPlus(DisplayCurrentInclinometerReading, _desiredInclinometerReportInterval);
                _dispatcherTimer4Inclinometer.Start();

                _inclinometer.ReadingChanged += OnInclinometer_ReadingChanged;
            }
            else
            {
                InclinometerImage.Visibility = Visibility.Collapsed;
            }
            return Task.CompletedTask;
        }

        protected override Task CloseMayOverrideAsync(object args = null)
        {
            _dispatcherTimer4Compass?.Dispose();
            _dispatcherTimer4Compass = null;
            _dispatcherTimer4Inclinometer?.Dispose();
            _dispatcherTimer4Inclinometer = null;
            var compass = _compass; if (compass != null) compass.ReadingChanged -= OnCompass_ReadingChanged;
            var incl = _inclinometer; if (incl != null) incl.ReadingChanged -= OnInclinometer_ReadingChanged;
            return Task.CompletedTask;
        }

        private void OnCompass_ReadingChanged(Compass sender, CompassReadingChangedEventArgs args)
        {
            DisplayCurrentCompassReading();
        }
        private void OnInclinometer_ReadingChanged(Inclinometer sender, InclinometerReadingChangedEventArgs args)
        {
            DisplayCurrentInclinometerReading();
        }

        private void DisplayCurrentCompassReading()
        {
            var compass = _compass;
            if (compass == null) return;

            try
            {
                var reading = compass.GetCurrentReading();

                if (reading == null || reading.HeadingTrueNorth == null)
                    SetHeadingNoneAsync();
                else
                {
                    SetHeadingAsync((double)reading.HeadingTrueNorth, reading.HeadingAccuracy);
                }
            }
            catch { }
        }
        private void DisplayCurrentInclinometerReading()
        {
            var inclinometer = _inclinometer;
            if (inclinometer == null) return;

            try
            {
                // pitch is negative if the top of the screen is lower than the bottom
                // roll is negative if the left side of the screen is lower than the right
                // values are in degrees
                var reading = inclinometer.GetCurrentReading();
                if (reading == null) SetRollPitchNoneAsync();
                else SetRollPitchAsync(reading.PitchDegrees, reading.RollDegrees);
            }
            catch { }
        }

        private Task SetHeadingNoneAsync()
        {
            return RunInUiThreadAsync(() =>
            {
                Heading.Rotation = 0.0;
                LayoutRoot.Visibility = Visibility.Collapsed;
            });
        }
        private Task SetHeadingAsync(double newValue, MagnetometerAccuracy accuracy)
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
            return RunInUiThreadAsync(() =>
            {
                Heading.Rotation = -newValue;
                LayoutRoot.Visibility = Visibility.Visible;
                CompassImage.Source = source;
            });
        }

        private Task SetRollPitchNoneAsync()
        {
            return RunInUiThreadAsync(() =>
            {
                RollPitch.TranslateX = 0.0;
                RollPitch.TranslateY = 0.0;
                InclinometerImage.Visibility = Visibility.Collapsed;
            });
        }
        private Task SetRollPitchAsync(double pitchDegrees, double rollDegrees)
        {
            return RunInUiThreadAsync(() =>
            {
                try
                {
                    RollPitch.TranslateX = -rollDegrees / 180.0 * 30.0;
                    RollPitch.TranslateY = -pitchDegrees / 180.0 * 30.0;
                    InclinometerImage.Visibility = Visibility.Visible;
                }
                catch { }
            });
        }
    }
}
