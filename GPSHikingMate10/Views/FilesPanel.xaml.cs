using LolloGPS.Data;
using System;
using System.Threading.Tasks;
using Windows.UI.Popups;
using Windows.UI.Xaml;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace LolloGPS.Core
{
    public sealed partial class FilesPanel : Utilz.Controlz.ObservableControl
    {
        #region properties
        public MainVM MainVM
        {
            get { return (MainVM)GetValue(MainVMProperty); }
            set { SetValue(MainVMProperty, value); }
        }
        public static readonly DependencyProperty MainVMProperty =
            DependencyProperty.Register("MainVM", typeof(MainVM), typeof(FilesPanel), new PropertyMetadata(null));
        #endregion properties


        #region lifecycle
        public FilesPanel()
        {
            InitializeComponent();
        }
        #endregion lifecycle


        #region event handlers
        private void OnCenterHistory_Click(object sender, RoutedEventArgs e)
        {
            Task ch = MainVM?.CentreOnHistoryAsync();
        }

        private async void OnClearHistory_Click(object sender, RoutedEventArgs e)
        {
            //raise confirmation popup
            var dialog = new MessageDialog("This will delete all the data. Are you sure?", "Confirm deletion");
            UICommand yesCommand = new UICommand("Yes", command => { });
            UICommand noCommand = new UICommand("No", command => { });
            dialog.Commands.Add(yesCommand);
            dialog.Commands.Add(noCommand);
            // Set the command that will be invoked by default
            dialog.DefaultCommandIndex = 1;
            // Show the message dialog
            IUICommand reply = await dialog.ShowAsync().AsTask();
            if (reply == yesCommand) { Task res = MainVM?.PersistentData?.ResetHistoryAsync(); }
        }

        private void OnSaveTrackingHistory_Click(object sender, RoutedEventArgs e)
        {
            Task sth = MainVM?.PickSaveSeriesToFileAsync(PersistentData.Tables.History, "_" + ConstantData.APPNAME_ALL_IN_ONE + "_Route");
        }

        private void OnCenterRoute_Click(object sender, RoutedEventArgs e)
        {
            Task cr = MainVM?.CentreOnRoute0Async();
        }

        private void OnClearRoute0_Click(object sender, RoutedEventArgs e)
        {
            Task rr = MainVM?.PersistentData?.ResetRoute0Async();
        }

        private void OnLoadRoute0_Click(object sender, RoutedEventArgs e)
        {
            Task lr = MainVM?.PickLoadSeriesFromFileAsync(PersistentData.Tables.Route0);
        }

        private void OnSaveRoute0_Click(object sender, RoutedEventArgs e)
        {
            Task sr = MainVM?.PickSaveSeriesToFileAsync(PersistentData.Tables.Route0, "_" + ConstantData.APPNAME_ALL_IN_ONE + "_Route");
        }

        private void OnCenterCheckpoints_Click(object sender, RoutedEventArgs e)
        {
            Task cl = MainVM?.CentreOnCheckpointsAsync();
        }

        private void OnClearCheckpoints_Click(object sender, RoutedEventArgs e)
        {
            Task cll = MainVM?.PersistentData?.ResetCheckpointsAsync();
        }

        private void OnLoadCheckpoints_Click(object sender, RoutedEventArgs e)
        {
            Task ll = MainVM?.PickLoadSeriesFromFileAsync(PersistentData.Tables.Checkpoints);
        }

        private void OnSaveCheckpoints_Click(object sender, RoutedEventArgs e)
        {
            Task sl = MainVM?.PickSaveSeriesToFileAsync(PersistentData.Tables.Checkpoints, "_" + ConstantData.APPNAME_ALL_IN_ONE + "_Checkpoints");
        }
        #endregion event handlers
    }
}