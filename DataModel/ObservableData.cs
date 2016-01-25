using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Utilz;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;

namespace LolloGPS.Data
{
	[DataContract]
	public abstract class ObservableData : INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler PropertyChanged;
		protected void RaisePropertyChanged([CallerMemberName] string propertyName = "")
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
		protected async void RaisePropertyChanged_UI([CallerMemberName] string propertyName = "")
		{
			await RunInUiThreadAsync(delegate
			{
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
			}).ConfigureAwait(false);
		}
		protected async void RaisePropertyChangedUrgent_UI([CallerMemberName] string propertyName = "")
		{
			try
			{
				await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, delegate
				{
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
				}).AsTask().ConfigureAwait(false);
			}
			catch (InvalidOperationException) // called from a background task: ignore
			{ }
			catch (Exception ex)
			{
				await Logger.AddAsync(ex.ToString(), Logger.PersistentDataLogFilename).ConfigureAwait(false);
			}
		}


		#region UIThread
		protected static async Task RunInUiThreadAsync(DispatchedHandler action)
		{
			try
			{
				if (CoreApplication.MainView.CoreWindow.Dispatcher.HasThreadAccess)
				{
					action();
				}
				else
				{
					await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, action).AsTask().ConfigureAwait(false);
				}
			}
			catch (InvalidOperationException) // called from a background task: ignore
			{ }
			catch (Exception ex)
			{
				await Logger.AddAsync(ex.ToString(), Logger.PersistentDataLogFilename).ConfigureAwait(false);
			}
		}
		#endregion UIThread
	}
}
