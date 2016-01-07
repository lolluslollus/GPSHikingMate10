using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Utilz;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;

namespace LolloBaseUserControls
{
	public abstract class ObservableControl : UserControl, INotifyPropertyChanged
	{
		#region INotifyPropertyChanged
		public event PropertyChangedEventHandler PropertyChanged;
		protected void ClearListeners() // we can use this inside a Dispose
		{
			PropertyChanged = null;
		}
		protected void RaisePropertyChanged([CallerMemberName] string propertyName = "")
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
		protected void RaisePropertyChanged_UI([CallerMemberName] string propertyName = "")
		{
			try
			{
				Task raise = RunInUiThreadAsync(delegate { RaisePropertyChanged(propertyName); });
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.PersistentDataLogFilename);
			}
		}
		#endregion INotifyPropertyChanged


		#region construct dispose
		public ObservableControl() { }
		#endregion construct dispose


		#region UIThread
		public async Task RunInUiThreadAsync(DispatchedHandler action)
		{
			if (Dispatcher.HasThreadAccess)
			{
				action();
			}
			else
			{
				await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, action).AsTask().ConfigureAwait(false);
			}
		}
		#endregion UIThread
	}
}
