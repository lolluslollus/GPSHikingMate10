using GPSHikingMate10.ViewModels;
using LolloGPS.Data;
using LolloGPS.Data.Runtime;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Utilz;
using Utilz.Controlz;
using Windows.ApplicationModel.Activation;
using Windows.Phone.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

// The Pivot Application template is documented at http://go.microsoft.com/fwlink/?LinkID=391641
namespace LolloGPS.Core
{
    public sealed partial class Main : OpenableObservablePage, IInfoPanelEventReceiver
    {
        #region properties
        public PersistentData PersistentData { get { return App.PersistentData; } }
        public RuntimeData RuntimeData { get { return App.RuntimeData; } }
        
        private MainVM _mainVM = null;
        public MainVM MainVM { get { return _mainVM; } }
        private MapsVM _mapsVM = null;
        public MapsVM MapsVM { get { return _mapsVM; } }

        public bool IsWideEnough
        {
            get { return (bool)GetValue(IsWideEnoughProperty); }
            set { SetValue(IsWideEnoughProperty, value); }
        }
        public static readonly DependencyProperty IsWideEnoughProperty =
            DependencyProperty.Register("IsWideEnough", typeof(bool), typeof(Main), new PropertyMetadata(false, OnIsWideEnoughChanged));
        private static void OnIsWideEnoughChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            var vm = (obj as Main)?.MainVM;
            if (vm != null) vm.IsWideEnough = (bool)(args.NewValue);
            Task alt0 = (obj as Main)?.UpdateIsExtraButtonsEnabledAsync();
            Task alt1 = (obj as Main)?.UpdateAltitudeColumnWidthAsync();
        }

        private bool _isExtraButtonsEnabled = false;
        public bool IsExtraButtonsEnabled { get { return _isExtraButtonsEnabled; } private set { if (_isExtraButtonsEnabled != value) { _isExtraButtonsEnabled = value; RaisePropertyChanged_UI(); } } }
        #endregion properties


        #region lifecycle
        public Main()
        {
            InitializeComponent();
            NavigationCacheMode = NavigationCacheMode.Disabled;
#if !NOSTORE
			MyPivot.Items.Remove(LogsButton);
#endif
        }

        protected override async Task OpenMayOverrideAsync(object args = null)
        {
            Logger.Add_TPL("Main.OpenMayOverrideAsync just started", Logger.AppEventsLogFilename, Logger.Severity.Info, false);

            _mainVM = new MainVM(IsWideEnough, MyLolloMap, MyAltitudeProfiles, this);
            await _mainVM.OpenAsync(args);
            RaisePropertyChanged_UI(nameof(MainVM));
            //await Task.Delay(1); // just in case

            await UpdateAltitudeColumnWidthAsync();
            await UpdateAltitudeColumnMaxWidthAsync();
            await UpdateIsExtraButtonsEnabledAsync();

            await MyLolloMap.OpenAsync(args);
            await MyAltitudeProfiles.OpenAsync(args);

            _mapsVM = new MapsVM(MyLolloMap.LolloMapVM, _mainVM);
            await _mapsVM.OpenAsync(args);
            RaisePropertyChanged_UI(nameof(MapsVM));

            MyMapsPanel.LolloMapVM = MyLolloMap.LolloMapVM;
            MyMapsPanel.MainVM = _mainVM;
            MyMapsPanel.MapsVM = _mapsVM;
            await MyMapsPanel.OpenAsync(args);

            MyCustomMapsPanel.MainVM = _mainVM;
            MyCustomMapsPanel.MapsVM = _mapsVM;
            await MyCustomMapsPanel.OpenAsync(args);

            AddHandlers();
            Logger.Add_TPL("Main.OpenMayOverrideAsync ended", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
        }

        protected override async Task CloseMayOverrideAsync(object args = null)
        {
            Debug.WriteLine("Main.CloseMayOverrideAsync() started");
            try
            {
                RemoveHandlers();

                EndAllAnimations();

                var mainVM = _mainVM;
                if (mainVM != null)
                {
                    await mainVM.CloseAsync(args);
                }
                var mapsVM = _mapsVM;
                if (mapsVM != null)
                {
                    await mapsVM.CloseAsync(args);
                }
                Debug.WriteLine("Main.CloseMayOverrideAsync() closed its VMs");

                await MyPointInfoPanel.CloseAsync(args);
                await MyLolloMap.CloseAsync(args);
                await MyAltitudeProfiles.CloseAsync(args);
                Debug.WriteLine("Main.CloseMayOverrideAsync() closed the altitude");
            }
            catch (Exception ex)
            {
                await Logger.AddAsync(ex.ToString(), Logger.ForegroundLogFilename);
            }
        }
        #endregion lifecycle


        #region event handling
        private volatile bool _isDataChangedHandlerActive = false;
        private void AddHandlers()
        {
            if (_isDataChangedHandlerActive) return;

            _isDataChangedHandlerActive = true;
            if (PersistentData != null) PersistentData.PropertyChanged += OnPersistentData_PropertyChanged;
        }

        private void RemoveHandlers()
        {
            if (PersistentData != null) PersistentData.PropertyChanged -= OnPersistentData_PropertyChanged;
            _isDataChangedHandlerActive = false;
        }

        private void OnBack_Click(object sender, RoutedEventArgs e)
        {
            _mainVM?.GoBackMyButtonSoft();
        }

        protected override void OnHardwareButtons_BackPressed(object sender, BackPressedEventArgs e)
        {
            _mainVM?.GoBackHard(sender, e);
        }

        protected override void OnTabletSoftwareButton_BackPressed(object sender, Windows.UI.Core.BackRequestedEventArgs e)
        {
            _mainVM?.GoBackTabletSoft(sender, e);
        }

        private void OnPersistentData_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PersistentData.LastMessage))
            {
                if (!string.IsNullOrWhiteSpace(PersistentData.LastMessage))
                {
                    Task gt = RunInUiThreadAsync(delegate
                    {
                        EndAllAnimations();
                        ShowHideLastMessageFlashyStoryboard.Begin();
                    });
                }
            }
            else if (e.PropertyName == nameof(PersistentData.IsShowingAltitudeProfiles))
            {
                Task alt = UpdateAltitudeColumnMaxWidthAsync();
                Task butt = UpdateIsExtraButtonsEnabledAsync();
            }
        }

        private bool _isLastMessageVisible = false;

        private void EndAllAnimations()
        {
            ShowHideLastMessageFlashyStoryboard.SkipToFill();
            ShowHideLastMessageDiscreetStoryboard.SkipToFill();
        }
        private void OnLastMessage_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            if (!_isLastMessageVisible && !string.IsNullOrWhiteSpace(_mainVM.PersistentData.LastMessage))
            {
                _isLastMessageVisible = true;
                EndAllAnimations();
                ShowHideLastMessageDiscreetStoryboard.Begin();
            }
            else if (_isLastMessageVisible)
            {
                _isLastMessageVisible = false;
                EndAllAnimations();
            }
        }
        private void OnGetAFixNow_Click(object sender, RoutedEventArgs e)
        {
            Task vibrate = Task.Run(() => RuntimeData.ShortVibration());
            _mainVM.GetAFix();
        }

        private void OnGotoLast_Click(object sender, RoutedEventArgs e)
        {
            Task vibrate = Task.Run(() => RuntimeData.ShortVibration());
            Task go = _mainVM.CentreOnCurrentAsync();
        }

        private void OnCancelDownload_Click(object sender, RoutedEventArgs e)
        {
            _mainVM.SetLastMessage_UI("Cancelling download");
            MyLolloMap?.LolloMapVM?.CancelDownloadByUser();
        }

        private async void OnTestFiles_Click(object sender, RoutedEventArgs e)
        {
            string txt = await FileDirectoryExtensions.GetAllFilesInLocalCacheFolderAsync(ConstantData.TILE_SOURCES_DIR_NAME).ConfigureAwait(false);

            await RunInUiThreadAsync(delegate
            {
                _mainVM.LogText = txt;
            }).ConfigureAwait(false);
        }

        private async void OnLogButton_Click(object sender, RoutedEventArgs e)
        {
            string cnt = (sender as Button)?.Content?.ToString();
            if (cnt == "FileError")
            {
                _mainVM.LogText = await Logger.ReadAsync(Logger.FileErrorLogFilename);
            }
            else if (cnt == "PersistentData")
            {
                _mainVM.LogText = await Logger.ReadAsync(Logger.PersistentDataLogFilename);
            }
            else if (cnt == "Fgr")
            {
                _mainVM.LogText = await Logger.ReadAsync(Logger.ForegroundLogFilename);
            }
            else if (cnt == "Bgr")
            {
                _mainVM.LogText = await Logger.ReadAsync(Logger.BackgroundLogFilename);
            }
            else if (cnt == "BgrCanc")
            {
                _mainVM.LogText = await Logger.ReadAsync(Logger.BackgroundCancelledLogFilename);
            }
            else if (cnt == "AppExc")
            {
                _mainVM.LogText = await Logger.ReadAsync(Logger.AppExceptionLogFilename);
            }
            else if (cnt == "AppEvents")
            {
                _mainVM.LogText = await Logger.ReadAsync(Logger.AppEventsLogFilename);
            }
            else if (cnt == "Clear")
            {
                Logger.ClearAll();
            }
        }
        private void OnLogText_Unloaded(object sender, RoutedEventArgs e)
        {
            _mainVM.LogText = string.Empty;
        }

        private void OnAltitude_Click(object sender, RoutedEventArgs e)
        {
            PersistentData.IsShowingPivot = false;
        }

        private void OnMapStyleButton_Click(object sender, RoutedEventArgs e)
        {
            PersistentData.CycleMapStyle();
        }
        #endregion event handling


        #region services
        private Task UpdateAltitudeColumnMaxWidthAsync()
        {
            return RunInUiThreadAsync(delegate
            {
                AltitudeColumn.MaxWidth = !PersistentData.IsShowingAltitudeProfiles ? 0 : double.PositiveInfinity;
            });
        }
        private Task UpdateIsExtraButtonsEnabledAsync()
        {
            return RunInUiThreadAsync(delegate
            {
                IsExtraButtonsEnabled = IsWideEnough || !PersistentData.IsShowingAltitudeProfiles;
            });
        }
        private Task UpdateAltitudeColumnWidthAsync()
        {
            return RunInUiThreadAsync(delegate
            {
                AltitudeColumn.Width = IsWideEnough ? new GridLength(1.0, GridUnitType.Star) : new GridLength(0.0);
                Grid.SetColumn(MyAltitudeProfiles, IsWideEnough ? 1 : 0);
            });
        }
        #endregion services


        #region point info panel
        public void OnInfoPanelPointChanged(object sender, EventArgs e)
        {
            MyAltitudeProfiles.OnInfoPanelPointChanged(sender, e);
            MyLolloMap.OnInfoPanelPointChanged(sender, e);

            try
            {
                if (PersistentData?.IsSelectedSeriesNonNullAndNonEmpty() == false) SelectedPointPopup.IsOpen = false;
            }
            catch (Exception ex)
            {
                Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
            }
        }

        public void OnInfoPanelClosed(object sender, object e)
        {
            if (PersistentData.IsShowingAltitudeProfiles) MyAltitudeProfiles.OnInfoPanelClosed(sender, e);
            MyLolloMap.OnInfoPanelClosed(sender, e);
        }

        private void OnShowOnePointDetailsRequested(object sender, AltitudeProfiles.ShowOnePointDetailsRequestedArgs e)
        {
            if (IsWideEnough || !PersistentData.IsShowingAltitudeProfiles)
            {
                if (e.SelectedSeries == PersistentData.Tables.History) MyLolloMap.CentreOnHistoryAsync();
                if (e.SelectedSeries == PersistentData.Tables.Route0) MyLolloMap.CentreOnRoute0Async();
                if (e.SelectedSeries == PersistentData.Tables.Checkpoints) MyLolloMap.CentreOnCheckpointsAsync();
            }
            MyPointInfoPanel.SetDetails(e.SelectedRecord, e.SelectedSeries);
            SelectedPointPopup.IsOpen = true;
        }

        private void OnShowManyPointDetailsRequested(object sender, LolloMap.ShowManyPointDetailsRequestedArgs e)
        {
            MyPointInfoPanel.SetDetails(e.SelectedRecords, e.SelectedSeriess);
            SelectedPointPopup.IsOpen = true;
        }
        #endregion point info panel

        #region file activated
        public async Task FileActivateAsync(FileActivatedEventArgs args)
        {
            if (!IsOnMe) return;
            // wait for the mainVM to be available and open, a bit crude but it beats opening it concurrently from here, 
            // while I am already trying to open it from somewhere else.
            int cnt = 0;
            while ((_mainVM == null || !_mainVM.IsOpen) && IsOnMe)
            {
                cnt++; if (cnt > 200) return;
                await Task.Delay(SuspenderResumerExtensions.MSecToWaitToConfirm).ConfigureAwait(false);
            }
            if (!IsOnMe) return;
            await _mainVM.LoadFileAsync(args).ConfigureAwait(false);
        }
        #endregion file activated
    }
}