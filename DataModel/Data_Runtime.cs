using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Utilz;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.Resources;
using Windows.Foundation;
using Windows.Networking.Connectivity;
using Windows.UI.Core;

namespace LolloGPS.Data.Runtime
{
	public sealed class RuntimeData : ObservableData, IDisposable
	{
		private static SemaphoreSlimSafeRelease _settingsDbDataReadSemaphore = new SemaphoreSlimSafeRelease(1, 1);

		#region properties
		private bool _isTrial = true;
		public bool IsTrial { get { return _isTrial; } set { _isTrial = value; RaisePropertyChanged_UI(); } }

		private int _trialResidualDays = -1;
		public int TrialResidualDays { get { return _trialResidualDays; } set { _trialResidualDays = value; RaisePropertyChanged_UI(); } }

		private bool _isHardwareButtonsAPIPresent = Windows.Foundation.Metadata.ApiInformation.IsTypePresent("Windows.Phone.UI.Input.HardwareButtons");
		public bool IsHardwareButtonsAPIPresent { get { return _isHardwareButtonsAPIPresent; } }

		private bool _isSettingsRead = false;
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
					IsCommandsActive = _isSettingsRead && _isDBDataRead;
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
			Logger.Add_TPL(sourceFilePath + " line " + sourceLineNumber + memberName + " set isSettingsRead to " + isSettingsRead, Logger.ForegroundLogFilename, Logger.Severity.Info);
			Task set = GetInstance().Set_IsSettingsRead_Async(isSettingsRead);
		}

		private bool _isDBDataRead = false;
		public bool IsDBDataRead { get { return _isDBDataRead; } }
		private async Task Set_IsDBDataRead_Async(bool value)
		{
			try
			{
				await _settingsDbDataReadSemaphore.WaitAsync().ConfigureAwait(false);
				if (_isDBDataRead != value)
				{
					_isDBDataRead = value;
					RaisePropertyChanged_UI(nameof(IsDBDataRead));
					IsCommandsActive = _isSettingsRead && _isDBDataRead;
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

		private bool _isCommandsActive = false;
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
				await _settingsDbDataReadSemaphore.WaitAsync(); //.ConfigureAwait(false);
				await func().ConfigureAwait(false);
			}
			finally
			{
				SemaphoreSlimSafeRelease.TryRelease(_settingsDbDataReadSemaphore);
			}
		}
		private double _downloadProgressValue = default(double);
		public double DownloadProgressValue { get { return _downloadProgressValue; } private set { if (_downloadProgressValue != value) { _downloadProgressValue = value; RaisePropertyChanged(); } } }
		public static void SetDownloadProgressValue_UI(double newProgressValue)
		{
			Task upd = RunInUiThreadAsync(delegate
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
					RaisePropertyChanged_UI(nameof(IsConnectionAvailable));
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
		private static ResourceLoader _resourceLoader = new ResourceLoader();
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
