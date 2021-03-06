﻿using LolloGPS.Data;
using LolloGPS.Data.Runtime;
using LolloGPS.ViewModels;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace LolloGPS.Core
{
    public sealed partial class PointInfoPanel : Utilz.Controlz.BackOrientOpenObservControl
    {
        public event EventHandler PointChanged;

        #region properties
        public PersistentData PersistentData => App.PersistentData;
        // 		public PersistentData PersistentData { get { return App.PersistentData; } } // LOLLO NOTE replaced with an expression body
        public MainVM MainVM
        {
            get { return (MainVM)GetValue(MainVMProperty); }
            set { SetValue(MainVMProperty, value); }
        }
        public static readonly DependencyProperty MainVMProperty =
            DependencyProperty.Register("MainVM", typeof(MainVM), typeof(PointInfoPanel), new PropertyMetadata(null, OnMainVM_Changed));
        private static void OnMainVM_Changed(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            if (args.NewValue != args.OldValue)
            {
                (obj as Utilz.Controlz.BackOrientOpenObservControl).BackPressedRaiser = args.NewValue as Utilz.Controlz.IBackPressedRaiser;
            }
        }


        private volatile bool _isGotoPreviousEnabled = false;
        public bool IsGotoPreviousEnabled { get { return _isGotoPreviousEnabled; } private set { _isGotoPreviousEnabled = value; RaisePropertyChanged_UI(); } }
        private volatile bool _isGotoNextEnabled = false;
        public bool IsGotoNextEnabled { get { return _isGotoNextEnabled; } private set { _isGotoNextEnabled = value; RaisePropertyChanged_UI(); } }
        private double _distanceMFromPrevious = default(double);
        public double DistanceMFromPrevious { get { return _distanceMFromPrevious; } private set { _distanceMFromPrevious = value; RaisePropertyChanged_UI(); } }
        private double _distanceMFromFirst = default(double);
        public double DistanceMFromFirst { get { return _distanceMFromFirst; } private set { _distanceMFromFirst = value; RaisePropertyChanged_UI(); } }
        private double _distanceMToLast = default(double);
        public double DistanceMToLast { get { return _distanceMToLast; } private set { _distanceMToLast = value; RaisePropertyChanged_UI(); } }
        private volatile bool _isTotalDistancesCalculated = false;
        public bool IsTotalDistancesCalculated { get { return _isTotalDistancesCalculated; } private set { _isTotalDistancesCalculated = value; RaisePropertyChanged_UI(); } }
        private volatile bool _isSeriesChoicePresented = false;
        public bool IsSeriesChoicePresented { get { return _isSeriesChoicePresented; } private set { _isSeriesChoicePresented = value; RaisePropertyChanged_UI(); } }

        private List<PointRecord> _mySelectedRecords;
        private List<PersistentData.Tables> _mySelectedSeriess;
        #endregion properties


        #region lifecycle
        public PointInfoPanel()
        {
            InitializeComponent();
        }
        protected override Task OnLoadedMayOverrideAsync()
        {
            return OpenAsync();
        }
        protected override Task OnUnloadedMayOverrideAsync()
        {
            return CloseAsync();
        }
        protected override Task OpenMayOverrideAsync(object args = null)
        {
            PersistentData.IsShowingPivot = false;
            PersistentData.IsBackButtonEnabled = true;
            RuntimeData.GetInstance().IsAllowCentreOnCurrent = false;
            UpdateWidth();
            UpdateHeight();

            return base.OpenMayOverrideAsync();
        }
        protected override Task CloseMayOverrideAsync(object args = null)
        {
            var timer = _holdingTimer;
            timer?.Dispose();
            _holdingTimer = null;

            PersistentData.IsBackButtonEnabled = false;
            RuntimeData.GetInstance().IsAllowCentreOnCurrent = true;

            return base.CloseMayOverrideAsync();
        }
        #endregion lifecycle


        #region user event handlers
        private async void OnDeletePoint_Click(object sender, RoutedEventArgs e)
        {
            bool isDeleted = await PersistentData.DeleteSelectedPointFromSeriesAsync(CancToken);
            if (!isDeleted) PersistentData.LastMessage = "Error updating data";
            SetPointProperties();
        }
        private void OnSymbolChanged(object sender, string newSymbol)
        {
            var pd = PersistentData;
            if (pd == null) return;

            var whichSeries = pd.SelectedSeries;
            if (whichSeries == PersistentData.Tables.Nil) return;
            pd.Selected?.UpdateDatabase(whichSeries);
            pd.RefreshSeries(whichSeries);
        }

        private void OnHumanDescription_TextChanged(object sender, TextChangedEventArgs e)
        {
            string currentText = (sender as TextBox)?.Text;
            if (currentText == null) return;
            Task upd = PersistentData?.Selected?.UpdateHumanDescriptionAsync(currentText, PersistentData.SelectedSeries);
        }
        private void OnHumanDescriptionTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            //string currentText = (sender as TextBox)?.Text;
            //if (currentText == null) return;
            //Task upd = PersistentData?.Selected?.UpdateHumanDescriptionAsync(currentText, PersistentData.SelectedSeries);
        }

        // horrid BODGE because TextBox with IsTabStop=False won't acquire focus (and won't show the keyboard, making it as dumb as a TextBlock)
        //private void OnTextBox_PointerEntered(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        //{
        //    (sender as TextBox).IsTabStop = true;
        //    bool isOK = (sender as TextBox).Focus(FocusState.Pointer);
        //    (sender as TextBox).IsTabStop = false;
        //}

        private void OnHyperlink_TextChanged(object sender, TextChangedEventArgs e)
        {
            string currentText = (sender as TextBox)?.Text;
            if (currentText == null) return;
            Task upd = PersistentData?.Selected?.UpdateHyperlinkAsync(currentText, PersistentData.SelectedSeries);
        }
        private void OnHyperlinkTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            //string currentText = (sender as TextBox)?.Text;
            //if (currentText == null) return;
            //Task upd = PersistentData?.Selected?.UpdateHyperlinkAsync(currentText, PersistentData.SelectedSeries);
        }

        private void OnHyperlinkTextTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            string currentText = (sender as TextBox)?.Text;
            if (currentText == null) return;
            Task upd = PersistentData?.Selected?.UpdateHyperlinkTextAsync(currentText, PersistentData.SelectedSeries);
        }

        private void OnHyperlink_Click(object sender, RoutedEventArgs e)
        {
            MainVM?.NavigateToUri(PersistentData?.Selected?.HyperLink);
        }

        private void OnGotoPrevious_Click(object sender, RoutedEventArgs e)
        {
            SkipToRecord(-1);
        }
        private void OnGotoNext_Click(object sender, RoutedEventArgs e)
        {
            SkipToRecord(1);
        }

        private void OnGoto100Previous_Click(object sender, RoutedEventArgs e)
        {
            SkipToRecord(-100);
        }
        private void OnGoto100Next_Click(object sender, RoutedEventArgs e)
        {
            SkipToRecord(100);
        }
        private void OnGotoPreviousButton_Holding(object sender, Windows.UI.Xaml.Input.HoldingRoutedEventArgs e)
        {
            SkipWhenHolding(-100, e);
        }
        private void OnGotoNextButton_Holding(object sender, Windows.UI.Xaml.Input.HoldingRoutedEventArgs e)
        {
            SkipWhenHolding(100, e);
        }

        private sealed class HoldingTimer : IDisposable
        {
            private DispatcherTimer _holdingTimer = null;
            private int _step = 0;
            private Action<int> _action = null;
            private readonly TimeSpan _interval = new TimeSpan(1000000);

            public void Dispose()
            {
                Stop();
            }

            public void Start(int step, Action<int> action)
            {
                Stop();

                _holdingTimer = new DispatcherTimer() { Interval = _interval };
                _step = step;
                _action = action;
                AddHandlers_HoldingTimer();
                _holdingTimer.Start();
            }
            public void Stop()
            {
                RemoveHandlers_HoldingTimer();
                _holdingTimer?.Stop();
                _holdingTimer = null;
            }

            private bool _isHoldingTimerHandlersActive = false;
            private void AddHandlers_HoldingTimer()
            {
                if (!_isHoldingTimerHandlersActive && _holdingTimer != null)
                {
                    _holdingTimer.Tick += OnHoldingTimer_Tick;
                    _isHoldingTimerHandlersActive = true;
                }
            }
            private void RemoveHandlers_HoldingTimer()
            {
                if (_holdingTimer != null)
                {
                    _holdingTimer.Tick -= OnHoldingTimer_Tick;
                    _isHoldingTimerHandlersActive = false;
                }
            }
            private void OnHoldingTimer_Tick(object sender, object e)
            {
                _action?.Invoke(_step);
            }
        }

        private HoldingTimer _holdingTimer = null;
        private void SkipWhenHolding(int step, Windows.UI.Xaml.Input.HoldingRoutedEventArgs e)
        {
            //Debug.WriteLine("HoldingState is " + e.HoldingState.ToString());
            if (e.HoldingState == Windows.UI.Input.HoldingState.Started)
            {
                if (_holdingTimer == null) _holdingTimer = new HoldingTimer();
                _holdingTimer.Start(step, SkipToRecord);
            }
            else
            {
                _holdingTimer?.Stop();
            }
        }
        private void SkipToRecord(int step)
        {
            PersistentData?.SelectNeighbourRecordFromAnySeries(step);
            SetPointProperties();
        }

        private void OnCalcTotalDistance_Click(object sender, RoutedEventArgs e)
        {
            SetTotalDistances();
        }
        #endregion user event handlers


        #region ui event handlers
        protected override void OnVisibleBoundsChangedMayOverride(ApplicationView sender, object args)
        {
            UpdateWidth();
            UpdateHeight();
        }
        #endregion ui event handlers


        #region updaters
        private void UpdateWidth()
        {
            //    ChooseSeriesGrid.Width = InfoGrid.Width = PopupContainer.ActualWidth;
            double availableWidth = default(double);

            if (Parent is Popup) availableWidth = (Parent as FrameworkElement).ActualWidth;
            else if (AppView?.VisibleBounds != null) availableWidth = AppView.VisibleBounds.Width;

            ChooseSeriesGrid.Width = InfoGrid.Width = availableWidth;
        }
        private void UpdateHeight()
        {
            //    ChooseSeriesGrid.Height = InfoGrid.Height = PopupContainer.ActualHeight;
            double availableHeight = default(double);

            if (Parent is Popup) availableHeight = (Parent as FrameworkElement).ActualHeight;
            else if (AppView?.VisibleBounds != null) availableHeight = AppView.VisibleBounds.Height;

            ChooseSeriesGrid.Height = InfoGrid.Height = availableHeight;
        }
        #endregion updaters


        #region services
        public void SetDetails(PointRecord selectedRecord, PersistentData.Tables selectedSeries)
        {
            List<PointRecord> selectedRecords = new List<PointRecord> { selectedRecord };
            List<PersistentData.Tables> selectedSerieses = new List<PersistentData.Tables> { selectedSeries };
            SetDetails(selectedRecords, selectedSerieses);
        }
        /// <summary>
        /// This method expects max 1 record each series
        /// </summary>
        /// <param name="selectedRecords"></param>
        /// <param name="selectedSeriess"></param>
        public void SetDetails(List<PointRecord> selectedRecords, List<PersistentData.Tables> selectedSeriess)
        {
            IsSeriesChoicePresented = false;

            if (selectedRecords.Count == 1) SelectPoint(selectedRecords[0], selectedSeriess[0]);
            else if (selectedRecords.Count >= 1)
            {
                _mySelectedRecords = selectedRecords;
                _mySelectedSeriess = selectedSeriess;
                // open popup to choose the series
                ChooseDisplayHistoryButton.Visibility = selectedSeriess.Contains(PersistentData.Tables.History) ? Visibility.Visible : Visibility.Collapsed;
                ChooseDisplayRoute0Button.Visibility = selectedSeriess.Contains(PersistentData.Tables.Route0) ? Visibility.Visible : Visibility.Collapsed;
                ChooseDisplayCheckpointsButton.Visibility = selectedSeriess.Contains(PersistentData.Tables.Checkpoints) ? Visibility.Visible : Visibility.Collapsed;

                IsSeriesChoicePresented = true;
            }
        }
        private void OnDisplayHistory_Click(object sender, RoutedEventArgs e)
        {
            IsSeriesChoicePresented = false;
            try
            {
                int index = _mySelectedSeriess.IndexOf(PersistentData.Tables.History);
                SelectPoint(_mySelectedRecords[index], _mySelectedSeriess[index]);
            }
            catch (Exception) { }
        }

        private void OnDisplayRoute0_Click(object sender, RoutedEventArgs e)
        {
            IsSeriesChoicePresented = false;
            try
            {
                int index = _mySelectedSeriess.IndexOf(PersistentData.Tables.Route0);
                SelectPoint(_mySelectedRecords[index], _mySelectedSeriess[index]);
            }
            catch (Exception) { }
        }

        private void OnDisplayCheckpoints_Click(object sender, RoutedEventArgs e)
        {
            IsSeriesChoicePresented = false;
            try
            {
                int index = _mySelectedSeriess.IndexOf(PersistentData.Tables.Checkpoints);
                SelectPoint(_mySelectedRecords[index], _mySelectedSeriess[index]);
            }
            catch (Exception) { }
        }

        private void SelectPoint(PointRecord dataRecord, PersistentData.Tables whichTable)
        {
            PersistentData?.SelectRecordFromSeries(dataRecord, whichTable);
            SetPointProperties();
        }

        private void SetPointProperties()
        {
            if (PersistentData?.IsSelectedSeriesNonNullAndNonEmpty() != true) PointChanged?.Invoke(this, EventArgs.Empty);

            IsGotoPreviousEnabled = PersistentData?.IsSelectedRecordFromAnySeriesFirst() == false;
            IsGotoNextEnabled = PersistentData.IsSelectedRecordFromAnySeriesLast() == false;

            PointRecord prev = null;
            PointRecord curr = null;
            if (IsGotoPreviousEnabled)
            {
                prev = PersistentData?.GetRecordBeforeSelectedFromAnySeries();
                curr = PersistentData?.Selected;
            }

            if (prev != null && curr != null) DistanceMFromPrevious = DistanceMBetweenLocations.Calc(prev.Latitude, prev.Longitude, curr.Latitude, curr.Longitude);
            else DistanceMFromPrevious = 0.0;

            ResetTotalDistances(); // expensive calculation, we only do it on click

            PointChanged?.Invoke(this, EventArgs.Empty);
        }

        private void ResetTotalDistances()
        {
            DistanceMFromFirst = 0.0;
            DistanceMToLast = 0.0;
            IsTotalDistancesCalculated = false;
        }
        private void SetTotalDistances()
        {
            if (PersistentData?.IsSelectedSeriesNonNullAndNonEmpty() != true)
            {
                DistanceMFromFirst = 0.0;
                DistanceMToLast = 0.0;
            }
            else
            {

                var selectedSeries = PersistentData?.GetSelectedSeries();
                var selectedIndex = PersistentData?.SelectedIndex_Base1 - 1 ?? 0;
                var selectedSeriesCount = selectedSeries.Count;

                if (!_isGotoPreviousEnabled) DistanceMFromFirst = 0.0;
                else
                {
                    double distanceMFromFirst = 0.0;
                    for (int i = 1; i <= selectedIndex; i++)
                    {
                        distanceMFromFirst += DistanceMBetweenLocations.Calc(selectedSeries[i - 1].Latitude, selectedSeries[i - 1].Longitude, selectedSeries[i].Latitude, selectedSeries[i].Longitude);
                    }
                    DistanceMFromFirst = distanceMFromFirst;
                }

                double distanceMToLast = 0.0;
                for (int i = selectedIndex + 1; i < selectedSeriesCount; i++)
                {
                    distanceMToLast += DistanceMBetweenLocations.Calc(selectedSeries[i - 1].Latitude, selectedSeries[i - 1].Longitude, selectedSeries[i].Latitude, selectedSeries[i].Longitude);
                }
                DistanceMToLast = distanceMToLast;
            }
            IsTotalDistancesCalculated = true;
        }
        #endregion services
    }

    public sealed class DistanceMBetweenLocations
    {
        public const double EarthRadiusKm = 6376.5;
        public static double Calc(double lat1, double lon1, double lat2, double lon2)
        {
            /*
                The Haversine formula according to Dr. Math.
                http://mathforum.org/library/drmath/view/51879.html

                dlon = lon2 - lon1
                dlat = lat2 - lat1
                a = (sin(dlat/2))^2 + cos(lat1) * cos(lat2) * (sin(dlon/2))^2
                c = 2 * atan2(sqrt(a), sqrt(1-a)) 
                d = R * c

                Where
                    * dlon is the change in longitude
                    * dlat is the change in latitude
                    * c is the great circle distance in Radians.
                    * R is the radius of a spherical Earth.
                    * The locations of the two points in 
                        spherical coordinates (longitude and 
                        latitude) are lon1,lat1 and lon2, lat2.
            */
            double dDistance = double.MinValue;
            double dLat1InRad = lat1 * (ConstantData.DEG_TO_RAD);
            double dLong1InRad = lon1 * (ConstantData.DEG_TO_RAD);
            double dLat2InRad = lat2 * (ConstantData.DEG_TO_RAD);
            double dLong2InRad = lon2 * (ConstantData.DEG_TO_RAD);

            double dLongitude = dLong2InRad - dLong1InRad;
            double dLatitude = dLat2InRad - dLat1InRad;

            // Intermediate result a.
            double a = Math.Pow(Math.Sin(dLatitude / 2.0), 2.0) +
                       Math.Cos(dLat1InRad) * Math.Cos(dLat2InRad) *
                       Math.Pow(Math.Sin(dLongitude / 2.0), 2.0);

            // Intermediate result c (great circle distance in Radians).
            double c = 2.0 * Math.Asin(Math.Sqrt(a));

            // Distance.
            // const double kEarthRadiusMiles = 3956.0;

            dDistance = EarthRadiusKm * c * 1000.0;

            return dDistance;
        }
    }

    public interface IInfoPanelEventReceiver
    {
        void OnInfoPanelPointChanged(object sender, EventArgs e);
        void OnInfoPanelClosed(object sender, object e);
    }
}