using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Utilz;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.UI.Core;

namespace LolloGPS.Data
{
	[DataContract]
	public abstract class ObservableData : INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler PropertyChanged;
		//protected void RaisePropertyChanged([CallerMemberName] string propertyName = "")
		//{
		//	var listener = PropertyChanged;
		//	if (listener != null)
		//	{
		//		listener(this, new PropertyChangedEventArgs(propertyName));
		//	}
		//}
		protected async void RaisePropertyChanged_UI([CallerMemberName] string propertyName = "")
		{
			try
			{
				await RunInUiThreadAsync(delegate
				{
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
				}).ConfigureAwait(false);
			}
			catch (InvalidOperationException) // called from a background task: ignore
			{ }
			catch (Exception ex)
			{
				await Logger.AddAsync(ex.ToString(), Logger.PersistentDataLogFilename).ConfigureAwait(false);
			}
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
