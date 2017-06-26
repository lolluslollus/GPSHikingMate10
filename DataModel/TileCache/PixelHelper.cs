using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Utilz;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace LolloGPS.Data.TileCache
{
    internal static class PixelHelper
    {
        private static readonly BitmapTransform _bitmapTransform = new BitmapTransform() { InterpolationMode = BitmapInterpolationMode.Linear };

        internal static async Task<RandomAccessStreamReference> GetPixelStreamRefFromFile(StorageFolder folder, string fileName, CancellationToken cancToken)
        {
            try
            {
                if (cancToken.IsCancellationRequested) return null;
                byte[] pixels = null;
                using (var readStream = await folder.OpenStreamForReadAsync(fileName).ConfigureAwait(false))
                {
                    if (cancToken.IsCancellationRequested) return null;
                    //pixels = await GetPixelArrayFromByteStream(readStream.AsRandomAccessStream()).ConfigureAwait(false);
                    pixels = await GetPixelArrayFromRandomAccessStream(readStream.AsRandomAccessStream()).ConfigureAwait(false);
                    if (cancToken.IsCancellationRequested) return null;
                }
                return await GetStreamRefFromArray(pixels).ConfigureAwait(false);
            }
            catch (Exception exc)
            {
                //Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
                return null;
            }
        }
        /*
        internal static async Task<RandomAccessStreamReference> GetPixelStreamRefFromByteArray(byte[] imgBytes)
        {
            try
            {
                byte[] pixels = await GetPixelArrayFromByteArray(imgBytes).ConfigureAwait(false);
                return await GetStreamRefFromArray(pixels).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Add_TPL(ex.ToString(), Logger.PersistentDataLogFilename);
                return null;
            }
        }
        */
        /*
        private static async Task<byte[]> GetPixelArrayFromByteArray(byte[] bytes)
        {
            try
            {
                using (InMemoryRandomAccessStream imraStream = new InMemoryRandomAccessStream())
                {
                    using (IOutputStream imraOutputStream = imraStream.GetOutputStreamAt(0)) // this seems to make it a little more stable
                    {
                        using (DataWriter StreamWriter = new DataWriter(imraOutputStream))
                        {
                            StreamWriter.WriteBytes(bytes);
                            await StreamWriter.StoreAsync().AsTask().ConfigureAwait(false);
                            await StreamWriter.FlushAsync().AsTask().ConfigureAwait(false);
                            StreamWriter.DetachStream(); // otherwise Dispose() will murder the stream
                        }

                        return await GetPixelArrayFromRandomAccessStream(imraStream).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Add_TPL(ex.ToString(), Logger.PersistentDataLogFilename);
                return null;
            }
        }
        */
        private static async Task<byte[]> GetPixelArrayFromRandomAccessStream(IRandomAccessStream source)
        {
#if DEBUG
            var sw = new Stopwatch(); sw.Start();
#endif
            try
            {
                var decoder = await BitmapDecoder.CreateAsync(source).AsTask().ConfigureAwait(false);
                //var decoder = await BitmapDecoder.CreateAsync(BitmapDecoder.PngDecoderId, source).AsTask().ConfigureAwait(false);
                //var decoder = await BitmapDecoder.CreateAsync(jpegDecoder.CodecId, dbStream).AsTask().ConfigureAwait(false);
                // LOLLO TODO the image can easily be 250K when the source only takes 10K. We need some compression! I am trying PNG decoder right now.
                // I can also try with the settings below - it actually seems not! I think the freaking output is always 262144 bytes coz it's really all the pixels.

                var pixelProvider = await decoder.GetPixelDataAsync(
                    BitmapPixelFormat.Rgba8,
                    //BitmapAlphaMode.Straight,
                    BitmapAlphaMode.Ignore, // faster
                    _bitmapTransform,
                    ExifOrientationMode.RespectExifOrientation,
                //ColorManagementMode.ColorManageToSRgb).AsTask().ConfigureAwait(false);
                ColorManagementMode.DoNotColorManage).AsTask().ConfigureAwait(false);

                return pixelProvider.DetachPixelData();
            }
            catch (Exception ex)
            {
                Logger.Add_TPL(ex.ToString(), Logger.PersistentDataLogFilename);
                return null;
            }
#if DEBUG
            finally
            {
                sw.Stop();
                Debug.WriteLine("GetPixelArrayFromRandomAccessStream has taken " + sw.ElapsedTicks + " ticks");
            }
#endif
        }
        private static async Task<RandomAccessStreamReference> GetStreamRefFromArray(byte[] array)
        {
            if (array == null || array.Length == 0) return null;

            // write pixels into a stream and return a reference to it
            // no Dispose() in the following!
            InMemoryRandomAccessStream inMemoryRandomAccessStream = new InMemoryRandomAccessStream();
            using (IOutputStream outputStream = inMemoryRandomAccessStream.GetOutputStreamAt(0)) // this seems to make it a little more stable
            {
                using (DataWriter writer = new DataWriter(outputStream))
                {
                    writer.WriteBytes(array);
                    await writer.StoreAsync().AsTask().ConfigureAwait(false);
                    await writer.FlushAsync().AsTask().ConfigureAwait(false);
                    writer.DetachStream(); // otherwise Dispose() will murder the stream
                }
                return RandomAccessStreamReference.CreateFromStream(inMemoryRandomAccessStream);
            }
        }
        /*
        public static async Task<IRandomAccessStreamReference> GetRedTileStreamRefAsync()
        { // this is sample code from MS
            int pixelHeight = 256;
            int pixelWidth = 256;
            int bpp = 4;

            byte[] bytes = new byte[pixelHeight * pixelWidth * bpp];

            for (int yy = 0; yy < pixelHeight; yy++)
            {
                for (int xx = 0; xx < pixelWidth; xx++)
                {
                    int pixelIndex = yy * pixelWidth + xx;
                    int byteIndex = pixelIndex * bpp;

                    // Set the current pixel bytes.
                    bytes[byteIndex] = 0xff;        // Red
                    bytes[byteIndex + 1] = 0x00;    // Green
                    bytes[byteIndex + 2] = 0x00;    // Blue
                    bytes[byteIndex + 3] = 0x80;    // Alpha (0xff = fully opaque)
                }
            }

            // Create RandomAccessStream from byte array.
            InMemoryRandomAccessStream randomAccessStream = new InMemoryRandomAccessStream();
            IOutputStream outputStream = randomAccessStream.GetOutputStreamAt(0);
            DataWriter writer = new DataWriter(outputStream);
            writer.WriteBytes(bytes);
            await writer.StoreAsync();
            await writer.FlushAsync();
            return RandomAccessStreamReference.CreateFromStream(randomAccessStream);
        }
        */
    }
}
