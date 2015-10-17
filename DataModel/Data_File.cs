using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace LolloGPS.Data.Files
{
    /// <summary>
    /// this works, use it for testing or backing up and restoring data
    /// </summary>
    public sealed class FileData
    {
        private static async Task<IReadOnlyList<StorageFile>> GetFilesInFolderAsync(StorageFolder folder)
        {
            List<StorageFile> output = new List<StorageFile>();
            var files = await folder.GetFilesAsync().AsTask().ConfigureAwait(false);
            output.AddRange(files);
            var folders = await folder.GetFoldersAsync().AsTask().ConfigureAwait(false);
            foreach (var item in folders)
            {
                output.AddRange(await GetFilesInFolderAsync(item).ConfigureAwait(false));
            }
            return output;
        }
        public static async Task<string> GetAllFilesInLocalFolderAsync()
        {
            string output = string.Empty;
            // Debug.WriteLine("start reading local folder contents");
            var filez = await GetFilesInFolderAsync(ApplicationData.Current.LocalFolder).ConfigureAwait(false);
            foreach (var item in filez)
            {
                // Debug.WriteLine(item.Path, item.Name);
                output += (item.Path + item.Name + Environment.NewLine);
            }
            // Debug.WriteLine("end reading local folder contents");
            return output;
        }
    }
}
