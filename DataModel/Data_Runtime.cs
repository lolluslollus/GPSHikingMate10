using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Utilz;
using Utilz.Data;
using Windows.ApplicationModel.Resources;
using Windows.Devices.Input;
using Windows.Networking.Connectivity;
using Windows.Phone.Devices.Notification;

namespace LolloGPS.Data.Runtime
{
    /// <summary>
    /// This should be the first entity to be instantiated in the application lifecycle,
    /// and the last to be closed. For example, it should not hold references to PersistentData,
    /// which is not necessarily available 
    /// (except for its default instance, which is always available since it's a singleton).
    /// </summary>
    public sealed class RuntimeData : ObservableData
    {
        #region properties
        private static readonly SemaphoreSlimSafeRelease _settingsDbDataReadSemaphore = new SemaphoreSlimSafeRelease(1, 1);

        private volatile bool _isWideEnough = false;
        public bool IsWideEnough { get { return _isWideEnough; } set { _isWideEnough = value; RaisePropertyChanged_UI(); } }
        private volatile bool _isTrial = true;
        public bool IsTrial { get { return _isTrial; } set { _isTrial = value; RaisePropertyChanged_UI(); } }

        private volatile int _trialResidualDays = -1;
        public int TrialResidualDays { get { return _trialResidualDays; } set { _trialResidualDays = value; RaisePropertyChanged_UI(); } }

        private volatile bool _isAllowCentreOnCurrent = false;
        public bool IsAllowCentreOnCurrent { get { return _isAllowCentreOnCurrent; } set { if (_isAllowCentreOnCurrent != value) { _isAllowCentreOnCurrent = value; RaisePropertyChanged_UI(); } } }

        #region hardware
        private static readonly bool _isVibrationDevicePresent = Windows.Foundation.Metadata.ApiInformation.IsTypePresent("Windows.Phone.Devices.Notification.VibrationDevice");
        // if you only check the touch device, a little trackpad will return a positive. We really want to know if we have a proper touch screen,
        // so we check that the device can vibrate, too; essentially, if it is a phone or similar.
        private static readonly bool _isProperTouchDevice = new TouchCapabilities().TouchPresent == 1 && Windows.Foundation.Metadata.ApiInformation.IsTypePresent("Windows.Phone.Devices.Notification.VibrationDevice");
        public static bool IsProperTouchDevice { get { return _isProperTouchDevice; } }

        private readonly bool _isHardwareButtonsAPIPresent = Windows.Foundation.Metadata.ApiInformation.IsTypePresent("Windows.Phone.UI.Input.HardwareButtons");
        public bool IsHardwareButtonsAPIPresent { get { return _isHardwareButtonsAPIPresent; } }
        #endregion hardware       

        #region download and save tiles
        private double _downloadProgressValue = default(double);
        public double DownloadProgressValue { get { return _downloadProgressValue; } private set { if (_downloadProgressValue != value) { _downloadProgressValue = value; RaisePropertyChanged(); } } }
        public void SetDownloadProgressValue_UI(double newProgressValue)
        {
            Task upd = RunInUiThreadAsync(delegate
            {
                GetInstance().DownloadProgressValue = newProgressValue;
            });
        }
        private double _saveProgressValue = default(double);
        public double SaveProgressValue { get { return _saveProgressValue; } private set { if (_saveProgressValue != value) { _saveProgressValue = value; RaisePropertyChanged(); } } }
        public void SetSaveProgressValue_UI(double newProgressValue)
        {
            Task upd = RunInUiThreadAsync(delegate
            {
                GetInstance().SaveProgressValue = newProgressValue;
            });
        }
        #endregion download and save tiles

        #region connection
        private static readonly object _isConnAvailLocker = new object();
        private bool _isConnectionAvailable = false; // no volatile here: I have the locker already, so I use it. volatile is very fast, but the locker is way faster.
        public bool IsConnectionAvailable
        {
            get
            {
                lock (_isConnAvailLocker)
                {
                    return _isConnectionAvailable;
                }
            }
            private set
            {
                if (_isConnectionAvailable != value)
                {
                    _isConnectionAvailable = value;
                    RaisePropertyChanged_UI(nameof(IsConnectionAvailable));
                }
            }
        }
        public void UpdateIsConnectionAvailable()
        {
            lock (_isConnAvailLocker)
            {
                var profile = NetworkInformation.GetInternetConnectionProfile();
                if (profile == null)
                {
                    IsConnectionAvailable = false;
                }
                else
                {
                    var level = profile.GetNetworkConnectivityLevel();
                    if (level == NetworkConnectivityLevel.InternetAccess || level == NetworkConnectivityLevel.LocalAccess)
                    {
                        if (
                            PersistentData.GetInstance().IsAllowMeteredConnection
                            ||
                            NetworkInformation.GetInternetConnectionProfile()?.GetConnectionCost()?.NetworkCostType == NetworkCostType.Unrestricted
                            )
                        {
                            IsConnectionAvailable = true;
                        }
                        else
                        {
                            IsConnectionAvailable = false;
                        }
                    }
                    else
                    {
                        IsConnectionAvailable = false;
                    }
                }
            }
        }
        #endregion connection

        #region globalisation
        private static readonly ResourceLoader _resourceLoader = new ResourceLoader();
        /// <summary>
        /// Gets a text from the resources, but not in the complex form such as "Resources/NewFieldValue/Text"
        /// For that, you need Windows.ApplicationModel.Resources.Core.ResourceManager.Current.MainResourceMap.GetValue("Resources/NewFieldValue/Text", ResourceContext.GetForCurrentView()).ValueAsString;
        /// However, that must be called from a view, and this class is not.
        /// </summary>
        /// <param name="resourceName"></param>
        /// <returns></returns>
        public static string GetText(string resourceName)
        {
            // localization localisation globalization globalisation
            string name = _resourceLoader.GetString(resourceName);
            return name ?? string.Empty;
        }
        #endregion globalisation
        #endregion properties

        #region lifecycle
        private static RuntimeData _instance;
        private static readonly object _instanceLocker = new object();
        private bool _isOpen = false;
        public static RuntimeData GetInstance()
        {
            lock (_instanceLocker)
            {
                if (_instance == null)
                {
                    _instance = new RuntimeData();
                }
                _instance.Open();
                return _instance;
            }
        }

        private RuntimeData()
        {
            Open();
        }
        private void Open()
        {
            if (_isOpen) return;
            _isOpen = true;

            AddHandlers();
            UpdateIsConnectionAvailable();
        }
        public void Close()
        {
            lock (_instanceLocker)
            {
                RemoveHandlers();
                _isOpen = false;
            }
        }
        #endregion lifecycle

        #region event helpers
        private bool _isHandlersActive = false;
        private void AddHandlers()
        {
            if (!_isHandlersActive)
            {
                _isHandlersActive = true;
                NetworkInformation.NetworkStatusChanged += OnNetworkStatusChanged;
            }
        }
        private void RemoveHandlers()
        {
            NetworkInformation.NetworkStatusChanged -= OnNetworkStatusChanged;
            _isHandlersActive = false;
        }
        #endregion event helpers

        #region event handlers
        private void OnNetworkStatusChanged(object sender)
        {
            UpdateIsConnectionAvailable();
        }
        #endregion event handlers

        #region services
        public static void ShortVibration()
        {
            if (_isVibrationDevicePresent)
            {
                VibrationDevice myDevice = VibrationDevice.GetDefault();
                myDevice.Vibrate(TimeSpan.FromSeconds(.12));
            }
        }
        #endregion services
    }
}
