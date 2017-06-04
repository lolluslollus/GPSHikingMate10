using LolloGPS.Data;
using LolloGPS.Data.Runtime;
using System;
using System.Diagnostics;
using Utilz;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace LolloGPS.Core
{
	public sealed partial class AboutPanel : UserControl
	{
		public string AppName => ConstantData.AppName; // LOLLO NOTE these are "expression bodies", apparently equivalent to a getter.
		public string AppVersion => ConstantData.Version;
		public RuntimeData RuntimeData => App.RuntimeData;


		public AboutPanel()
		{
			InitializeComponent();
		}
		private async void OnBuy_Click(object sender, RoutedEventArgs e)
		{
			bool isAlreadyBought = await Licenser.GetInstance().BuyAsync();
			if (!isAlreadyBought) (App.Current as App).Quit();
		}

		private async void OnRate_Click(object sender, RoutedEventArgs e)
		{
			await Licenser.GetInstance().RateAsync();
		}
		private async void OnGotoPrivacyPolicy_Click(object sender, RoutedEventArgs e)
		{
			await Launcher.LaunchUriAsync(new Uri(ConstantData.PRIVACY_POLICY_URL));
		}

		private async void OnSendMail_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				string uri = "mailto:" + ConstantData.MYMAIL + "?subject=" + ConstantData.APPNAME + " feedback";
				await Launcher.LaunchUriAsync(new Uri(uri, UriKind.Absolute));
			}
			catch (Exception ex)
			{
				Debug.WriteLine("ERROR: OnSendMail_Click caused an exception: " + ex.ToString());
			}
		}

		private async void OnSendMailWithLog_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				await Logger.SendEmailWithLogsAsync(ConstantData.MYMAIL, ConstantData.APPNAME).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				Debug.WriteLine("ERROR: OnSendMailWithLog_Click caused an exception: " + ex.ToString());
			}
		}
	}
}
