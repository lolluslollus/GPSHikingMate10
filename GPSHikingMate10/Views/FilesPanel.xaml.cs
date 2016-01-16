using LolloBaseUserControls;
using LolloGPS.Data;
using LolloGPS.Data.Constants;
using System;
using System.Threading.Tasks;
using Windows.UI.Popups;
using Windows.UI.Xaml;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace LolloGPS.Core
{
	public sealed partial class FilesPanel : ObservableControl
	{
		public Main_VM VM
		{
			get { return (Main_VM)GetValue(VMProperty); }
			set { SetValue(VMProperty, value); }
		}
		public static readonly DependencyProperty VMProperty =
			DependencyProperty.Register("VM", typeof(Main_VM), typeof(FilesPanel), new PropertyMetadata(null, OnVMChanged));
		private static void OnVMChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
		{
			if (args.NewValue != args.OldValue) (obj as FilesPanel).UpdateDataContext();
		}


		public FilesPanel()
		{
			InitializeComponent();
		}
		private void UpdateDataContext()
		{
			Task upd = RunInUiThreadAsync(delegate
			{
				LayoutRoot.DataContext = VM; // LOLLO NOTE never set DataContent on self in a UserControl
			});
		}

		private void OnCenterHistory_Click(object sender, RoutedEventArgs e)
		{
			Task ch = VM?.CentreOnHistoryAsync();
		}

		private async void OnClearHistory_Click(object sender, RoutedEventArgs e)
		{
			//raise confirmation popup
			var dialog = new Windows.UI.Popups.MessageDialog("This will delete all the data. Are you sure?", "Confirm deletion");
			UICommand yesCommand = new UICommand("Yes", (command) => { });
			UICommand noCommand = new UICommand("No", (command) => { });
			dialog.Commands.Add(yesCommand);
			dialog.Commands.Add(noCommand);
			// Set the command that will be invoked by default
			dialog.DefaultCommandIndex = 1;
			// Show the message dialog
			IUICommand reply = await dialog.ShowAsync().AsTask();
			if (reply == yesCommand) { Task res = VM?.MyPersistentData?.ResetHistoryAsync(); }
		}

		private void OnSaveTrackingHistory_Click(object sender, RoutedEventArgs e)
		{
			Task sth = VM?.PickSaveSeriesToFileAsync(PersistentData.Tables.History, "_" + ConstantData.APPNAME_ALL_IN_ONE + "_Route");
		}

		private void OnCenterRoute_Click(object sender, RoutedEventArgs e)
		{
			Task cr = VM?.CentreOnRoute0Async();
		}

		private void OnClearRoute0_Click(object sender, RoutedEventArgs e)
		{
			Task rr = VM?.MyPersistentData?.ResetRoute0Async();
		}

		private void OnLoadRoute0_Click(object sender, RoutedEventArgs e)
		{
			Task lr = VM?.PickLoadSeriesFromFileAsync(PersistentData.Tables.Route0);
		}

		private void OnSaveRoute0_Click(object sender, RoutedEventArgs e)
		{
			Task sr = VM?.PickSaveSeriesToFileAsync(PersistentData.Tables.Route0, "_" + ConstantData.APPNAME_ALL_IN_ONE + "_Route");
		}

		private void OnCenterLandmarks_Click(object sender, RoutedEventArgs e)
		{
			Task cl = VM?.CentreOnLandmarksAsync();
		}

		private void OnClearLandmarks_Click(object sender, RoutedEventArgs e)
		{
			Task cll = VM?.MyPersistentData?.ResetLandmarksAsync();
		}

		private void OnLoadLandmarks_Click(object sender, RoutedEventArgs e)
		{
			Task ll = VM?.PickLoadSeriesFromFileAsync(PersistentData.Tables.Landmarks);
		}

		private void OnSaveLandmarks_Click(object sender, RoutedEventArgs e)
		{
			Task sl = VM?.PickSaveSeriesToFileAsync(PersistentData.Tables.Landmarks, "_" + ConstantData.APPNAME_ALL_IN_ONE + "_Landmarks");
		}
	}
}