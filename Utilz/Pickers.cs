using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace Utilz
{
	public class Pickers
	{
		private const string PICKED_FOLDER_TOKEN = "PickedFolderToken";
		private const string PICKED_SAVE_FILE_TOKEN = "PickedSaveFileToken";
		private const string PICKED_OPEN_FILE_TOKEN = "PickedOpenFileToken";

		public static async Task<StorageFolder> PickDirectoryAsync(string[] extensions)
		{
			//bool unsnapped = ((ApplicationView.Value != ApplicationViewState.Snapped) || ApplicationView.TryUnsnap());
			//if (unsnapped)
			//{

			var openPicker = new FolderPicker();
			openPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
			//openPicker.CommitButtonText=
			//openPicker.ViewMode = PickerViewMode.List;
			foreach (var ext in extensions)
			{
				openPicker.FileTypeFilter.Add(ext);
			}
			var folder = await openPicker.PickSingleFolderAsync();
			if (folder != null)
			{
				// Application now has read/write access to all contents in the picked folder
				// (including other sub-folder contents)
				// LOLLO TODO check https://msdn.microsoft.com/en-us/library/windows/apps/mt186452.aspx
				// and https://blogs.windows.com/buildingapps/2014/06/19/common-questions-and-answers-about-files-and-app-data-part-1-app-data/
				// and use this, to make your life easier!
				Windows.Storage.AccessCache.StorageApplicationPermissions.FutureAccessList.AddOrReplace("PickedFolderToken", folder);
			}
			return folder;

			//}
			//return false;
		}

		public static async Task<StorageFile> PickOpenFileAsync(string[] extensions)
		{
			// test for phone: bring it to the UI thread
			StorageFile file = null;
			try
			{
				Task<StorageFile> fileTask = null;
				await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, delegate
				{
					var openPicker = new FileOpenPicker();
					
					openPicker.ViewMode = PickerViewMode.List;
					//openPicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
					openPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
					//openPicker.CommitButtonText = "Pick a file"; // LOLLO localise this if you use it
					foreach (var ext in extensions)
					{
						openPicker.FileTypeFilter.Add(ext);
					}
					fileTask = openPicker.PickSingleFileAsync().AsTask();
				});
				file = await fileTask;
				if (file != null)
				{
					Windows.Storage.AccessCache.StorageApplicationPermissions.FutureAccessList.AddOrReplace(PICKED_OPEN_FILE_TOKEN, file);
				}
			}
			catch (Exception ex)
			{
				await Logger.AddAsync(ex.ToString(), Logger.FileErrorLogFilename).ConfigureAwait(false);
			}
			return file;
		}

		public static async Task<StorageFile> PickSaveFileAsync(string[] extensions, string suggestedFileName)
		{
			var picker = new FileSavePicker();
			picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
			picker.SuggestedFileName = suggestedFileName;
			//openPicker.CommitButtonText=
			//openPicker.ViewMode = PickerViewMode.List;
			foreach (var ext in extensions)
			{
				var exts = new List<string>(); exts.Add(ext);
				picker.FileTypeChoices.Add(ext + " file", exts);
			}

			var file = await picker.PickSaveFileAsync();
			if (file != null)
			{
				Windows.Storage.AccessCache.StorageApplicationPermissions.FutureAccessList.AddOrReplace(PICKED_SAVE_FILE_TOKEN, file);
			}

			return file;
		}

		// LOLLO TODO these tokens are eternal and the list can contain 1000 entries max. Delete them after using!
		public static async Task<StorageFolder> GetLastPickedFolderJustOnceAsync()
		{
			StorageFolder result = null;
			try
			{
				result = await Windows.Storage.AccessCache.StorageApplicationPermissions.FutureAccessList.GetFolderAsync(PICKED_FOLDER_TOKEN).AsTask().ConfigureAwait(false);
			}
			catch
			{
				result = null;
			}
			return result;
		}

		public static async Task<StorageFile> GetLastPickedOpenFileJustOnceAsync()
		{
			StorageFile result = null;
			try
			{
				result = await Windows.Storage.AccessCache.StorageApplicationPermissions.FutureAccessList.GetFileAsync(PICKED_OPEN_FILE_TOKEN).AsTask().ConfigureAwait(false);
			}
			catch
			{
				result = null;
			}
			return result;
		}

		public static async Task<StorageFile> GetLastPickedSaveFileJustOnceAsync()
		{
			StorageFile result = null;
			try
			{
				result = await Windows.Storage.AccessCache.StorageApplicationPermissions.FutureAccessList.GetFileAsync(PICKED_SAVE_FILE_TOKEN).AsTask().ConfigureAwait(false);
			}
			catch
			{
				result = null;
			}
			return result;
		}
	}
}
