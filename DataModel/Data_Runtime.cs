using System;
using System.Threading.Tasks;
using Utilz;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Networking.Connectivity;
using Windows.UI.Core;

namespace LolloGPS.Data.Runtime
{
    public sealed class RuntimeData : ObservableData, IDisposable
    {
        private static SemaphoreSlimSafeRelease SettingsDbDataReadSemaphore = new SemaphoreSlimSafeRelease(1, 1);

        #region properties
        private bool _isTrial = true;
        public bool IsTrial { get { return _isTrial; } set { _isTrial = value; RaisePropertyChanged_UI(); } }

        private int _trialResidualDays = -1;
        public int TrialResidualDays { get { return _trialResidualDays; } set { _trialResidualDays = value; RaisePropertyChanged_UI(); } }

        private bool _isHardwareButtonsAPIPresent = Windows.Foundation.Metadata.ApiInformation.IsTypePresent("Windows.Phone.UI.Input.HardwareButtons");
        public bool IsHardwareButtonsAPIPresent { get { return _isHardwareButtonsAPIPresent; } }

        private Boolean _isSettingsRead = false;
        public Boolean IsSettingsRead { get { return _isSettingsRead; } }
        private async Task Set_IsSettingsRead_Async(bool value)
        {
            try
            {
                await SettingsDbDataReadSemaphore.WaitAsync().ConfigureAwait(false);
                if (_isSettingsRead != value)
                {
                    _isSettingsRead = value;
                    RaisePropertyChanged_UI(nameof(RuntimeData.IsSettingsRead));
                    IsCommandsActive = _isSettingsRead && _isDBDataRead;
                }
            }
            finally
            {
                SemaphoreSlimSafeRelease.TryRelease(SettingsDbDataReadSemaphore);
            }
        }
        public static void SetIsSettingsRead_UI(bool isSettingsRead)
        {
            Task set = RuntimeData.GetInstance().Set_IsSettingsRead_Async(isSettingsRead);
        }

        private Boolean _isDBDataRead = false;
        public Boolean IsDBDataRead { get { return _isDBDataRead; } }
        private async Task Set_IsDBDataRead_Async(bool value)
        {
            try
            {
                await SettingsDbDataReadSemaphore.WaitAsync().ConfigureAwait(false);
                if (_isDBDataRead != value)
                {
                    _isDBDataRead = value;
                    RaisePropertyChanged_UI(nameof(RuntimeData.IsDBDataRead));
                    IsCommandsActive = _isSettingsRead && _isDBDataRead;
                }
            }
            finally
            {
                SemaphoreSlimSafeRelease.TryRelease(SettingsDbDataReadSemaphore);
            }
        }
        public static void SetIsDBDataRead_UI(bool isDbDataRead)
        {
            Task set = GetInstance().Set_IsDBDataRead_Async(isDbDataRead);
        }

        private Boolean _isCommandsActive = false;
        public Boolean IsCommandsActive
        {
            get { return _isCommandsActive; }
            private set
            {
                if (_isCommandsActive != value)
                {
                    _isCommandsActive = value;
                    RaisePropertyChanged_UI(nameof(RuntimeData.IsCommandsActive));
                }
            }
        }

        //public async Task RunFunctionUnderSemaphore(Action func)
        //{
        //    try
        //    {
        //        await SettingsDbDataReadSemaphore.WaitAsync(); //.ConfigureAwait(false);
        //        func();
        //    }
        //    finally
        //    {
        //        SemaphoreSlimSafeRelease.TryRelease(SettingsDbDataReadSemaphore);
        //    }
        //}
        public async Task RunFunctionUnderSemaphoreT(Func<Task> func)
        {
            try
            {
                await SettingsDbDataReadSemaphore.WaitAsync(); //.ConfigureAwait(false);
                await func().ConfigureAwait(false);
            }
            finally
            {
                SemaphoreSlimSafeRelease.TryRelease(SettingsDbDataReadSemaphore);
            }
        }
        private double _downloadProgressValue = default(double);
        public double DownloadProgressValue { get { return _downloadProgressValue; } private set { if (_downloadProgressValue != value) { _downloadProgressValue = value; RaisePropertyChanged(); } } }
        public static void SetDownloadProgressValue_UI(double newProgressValue)
        {
            IAsyncAction set = CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, delegate
            {
                GetInstance().DownloadProgressValue = newProgressValue;
            });
        }

        private volatile bool _isConnectionAvailable = false;
        public bool IsConnectionAvailable
        {
            get { return _isConnectionAvailable; }
            private set
            {
                if (_isConnectionAvailable != value)
                {
                    _isConnectionAvailable = value;
                    RaisePropertyChanged_UI(nameof(RuntimeData.IsConnectionAvailable));
                }
            }
        }
        private void UpdateIsConnectionAvailable()
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
                    if (_persistentData == null) _persistentData = PersistentData.GetInstance();
                    if (
                        (_persistentData != null && _persistentData.IsAllowMeteredConnection)
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
        #endregion properties

        #region construct and dispose
        private static RuntimeData _instance;
        private static readonly object _instanceLock = new object();
        public static RuntimeData GetInstance()
        {
            lock (_instanceLock)
            {
                if (_instance == null)
                {
                    _instance = new RuntimeData();
                }
                return _instance;
            }
        }

        private static PersistentData _persistentData = null;
        private RuntimeData()
        {
            Activate();
        }
        public void Activate()
        {
            UpdateIsConnectionAvailable();
            AddHandlers();
        }
        public void Dispose()
        {
            RemoveHandlers();
        }
        private bool _isHandlersActive = false;
        private void AddHandlers()
        {
            if (!_isHandlersActive)
            {
                if (_persistentData == null) _persistentData = PersistentData.GetInstance();
                NetworkInformation.NetworkStatusChanged += OnNetworkStatusChanged;
                if (_persistentData != null) _persistentData.PropertyChanged += OnPersistentData_PropertyChanged;
                _isHandlersActive = true;
            }
        }
        private void RemoveHandlers()
        {
            if (_persistentData == null) _persistentData = PersistentData.GetInstance();
            NetworkInformation.NetworkStatusChanged -= OnNetworkStatusChanged;
            if (_persistentData != null) _persistentData.PropertyChanged -= OnPersistentData_PropertyChanged;
            _isHandlersActive = false;
        }
        #endregion construct and dispose

        #region event handlers
        private void OnNetworkStatusChanged(object sender)
        {
            UpdateIsConnectionAvailable();
        }
        private void OnPersistentData_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PersistentData.IsAllowMeteredConnection)) UpdateIsConnectionAvailable();
        }
        #endregion event handlers
    }
}
