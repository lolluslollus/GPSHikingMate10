using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Utilz;
using Utilz.Data;
using Windows.ApplicationModel.Resources;
using Windows.Networking.Connectivity;


namespace LolloGPS.Data.Runtime
{
	public sealed class RuntimeData : ObservableData
	{
		#region properties
		private static readonly SemaphoreSlimSafeRelease _settingsDbDataReadSemaphore = new SemaphoreSlimSafeRelease(1, 1);

		private volatile bool _isTrial = true;
		public bool IsTrial { get { return _isTrial; } set { _isTrial = value; RaisePropertyChanged_UI(); } }

		private volatile int _trialResidualDays = -1;
		public int TrialResidualDays { get { return _trialResidualDays; } set { _trialResidualDays = value; RaisePropertyChanged_UI(); } }

		private readonly bool _isHardwareButtonsAPIPresent = Windows.Foundation.Metadata.ApiInformation.IsTypePresent("Windows.Phone.UI.Input.HardwareButtons");
		public bool IsHardwareButtonsAPIPresent { get { return _isHardwareButtonsAPIPresent; } }

		private volatile bool _isAllowCentreOnCurrent = false;
		public bool IsAllowCentreOnCurrent { get { return _isAllowCentreOnCurrent; } set { if (_isAllowCentreOnCurrent != value) { _isAllowCentreOnCurrent = value; RaisePropertyChanged_UI(); } } }

		private volatile bool _isSettingsRead = false;
		public bool IsSettingsRead { get { return _isSettingsRead; } }
		private async Task Set_IsSettingsRead_Async(bool value)
		{
			try
			{
				await _settingsDbDataReadSemaphore.WaitAsync().ConfigureAwait(false);
				if (_isSettingsRead != value)
				{
					_isSettingsRead = value;
					RaisePropertyChanged_UI(nameof(IsSettingsRead));
					IsCommandsActive = _isSettingsRead && _isDbDataRead;
				}
			}
			finally
			{
				SemaphoreSlimSafeRelease.TryRelease(_settingsDbDataReadSemaphore);
			}
		}
		public static void SetIsSettingsRead_UI(bool isSettingsRead,
			[CallerMemberName] string memberName = "",
			[CallerFilePath] string sourceFilePath = "",
			[CallerLineNumber] int sourceLineNumber = 0)
		{
			Task set = GetInstance().Set_IsSettingsRead_Async(isSettingsRead);
		}

		private volatile bool _isDbDataRead = false;
		public bool IsDbDataRead { get { return _isDbDataRead; } }
		private async Task Set_IsDBDataRead_Async(bool value)
		{
			try
			{
				await _settingsDbDataReadSemaphore.WaitAsync().ConfigureAwait(false);
				if (_isDbDataRead != value)
				{
					_isDbDataRead = value;
					RaisePropertyChanged_UI(nameof(IsDbDataRead));
					IsCommandsActive = _isSettingsRead && _isDbDataRead;
				}
			}
			finally
			{
				SemaphoreSlimSafeRelease.TryRelease(_settingsDbDataReadSemaphore);
			}
		}
		public static void SetIsDBDataRead_UI(bool isDbDataRead)
		{
			Task set = GetInstance().Set_IsDBDataRead_Async(isDbDataRead);
		}

		private volatile bool _isCommandsActive = false;
		public bool IsCommandsActive
		{
			get { return _isCommandsActive; }
			private set
			{
				if (_isCommandsActive != value)
				{
					_isCommandsActive = value;
					RaisePropertyChangedUrgent_UI(nameof(IsCommandsActive));
				}
			}
		}

		private double _downloadProgressValue = default(double);
		public double DownloadProgressValue { get { return _downloadProgressValue; } private set { if (_downloadProgressValue != value) { _downloadProgressValue = value; RaisePropertyChanged(); } } }
		public void SetDownloadProgressValue_UI(double newProgressValue)
		{
			Task upd = RunInUiThreadAsync(delegate
			{
				GetInstance().DownloadProgressValue = newProgressValue;
			});
		}

		private readonly object _isConnAvailLocker = new object();
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
		private void UpdateIsConnectionAvailable()
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
						if (_persistentData == null) _persistentData = PersistentData.GetInstance();
						if (
							_persistentData.IsAllowMeteredConnection
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
		#endregion properties


		#region lifecycle
		private static RuntimeData _instance;
		private static readonly object _instanceLock = new object();
		private volatile bool _isOpen = false;
		public static RuntimeData GetInstance()
		{
			lock (_instanceLock)
			{
				if (_instance == null)
				{
					_instance = new RuntimeData();
				}
				_instance.Open();
				return _instance;
			}
		}

		private static volatile PersistentData _persistentData = null;
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
			RemoveHandlers();
			_isOpen = false;
		}
		#endregion lifecycle


		#region event helpers
		private volatile bool _isHandlersActive = false;
		private void AddHandlers()
		{
			if (!_isHandlersActive)
			{
				_isHandlersActive = true;
				if (_persistentData == null) _persistentData = PersistentData.GetInstance();
				NetworkInformation.NetworkStatusChanged += OnNetworkStatusChanged;
				_persistentData.PropertyChanged += OnPersistentData_PropertyChanged;
			}
		}
		private void RemoveHandlers()
		{
			if (_persistentData == null) _persistentData = PersistentData.GetInstance();
			NetworkInformation.NetworkStatusChanged -= OnNetworkStatusChanged;
			_persistentData.PropertyChanged -= OnPersistentData_PropertyChanged;
			_isHandlersActive = false;
		}
		#endregion event helpers


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
