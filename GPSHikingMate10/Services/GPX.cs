using LolloGPS.Core;
using LolloGPS.Data;
using LolloGPS.Data.Constants;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Utilz;
using Windows.Data.Xml.Dom;
using Windows.Storage;
using Windows.Storage.Provider;
using Windows.Storage.Streams;
using Windows.UI.Xaml;

namespace GPX
{
	// date-time formats: http://www.geekzilla.co.uk/View00FF7904-B510-468C-A2C8-F859AA20581F.htm
	public sealed class ReaderWriter
	{
		#region load route
		private const int LOAD_WAIT_INTERVAL_MSEC = 50;
		private const int LOAD_MAX_WAIT_INTERVALS = 200;
		/// <summary>
		/// Read a file and put it into the DB - not into its PersistentData.Collection.
		/// This method returns a task that is completed after reading the data and queues ui and db operations on the ui thread and on the threadpool.
		/// This method should be run on the threadpool.
		/// </summary>
		/// <param name="gpxFile"></param>
		/// <param name="whichTable"></param>
		/// <param name="token"></param>
		/// <returns></returns>
		public static async Task<Tuple<bool, string>> LoadSeriesFromFileIntoDbAsync(StorageFile gpxFile, PersistentData.Tables whichTable, CancellationToken token)
		{
			if (whichTable == PersistentData.Tables.Route0) return await LoadRoute0Async(gpxFile, token).ConfigureAwait(false);
			else if (whichTable == PersistentData.Tables.Landmarks) return await LoadLandmarksAsync(gpxFile, token).ConfigureAwait(false);
			else return null;
		}
		//public static async Task<Tuple<bool, string>> LoadSeriesFromFileIntoDbWaitingForDataOpenAsync(StorageFile gpxFile, PersistentData.Tables whichTable, CancellationToken token)
		//{
		//	int waitIntervals = 0;
		//	while (!((App)Application.Current).IsDataOpen)
		//	{
		//		await Task.Delay(LOAD_WAIT_INTERVAL_MSEC).ConfigureAwait(false);
		//		waitIntervals++;
		//		if (waitIntervals > LOAD_MAX_WAIT_INTERVALS) return null;
		//	}

		//	if (whichTable == PersistentData.Tables.Route0) return await LoadRoute0Async(gpxFile, token).ConfigureAwait(false);
		//	else if (whichTable == PersistentData.Tables.Landmarks) return await LoadLandmarksAsync(gpxFile, token).ConfigureAwait(false);
		//	else return null;
		//}
		private async static Task<Tuple<bool, string>> LoadRoute0Async(StorageFile gpxFile, CancellationToken token)
		{
			string outMessage = string.Empty;
			bool outIsOk = false;

			Logger.Add_TPL("Start reading Route0", Logger.ForegroundLogFilename, Logger.Severity.Info);
			if (gpxFile != null) // I could use class MemoryFailPoint in the following, to catch OutOfMemoryExceptions before they happen but it's old.
			{
				try
				{
					List<PointRecord> newDataRecords = await LoadDataRecordsAsync(gpxFile, PersistentData.Tables.Route0, token).ConfigureAwait(false);

					token.ThrowIfCancellationRequested();
					if (newDataRecords == null || newDataRecords.Count < 1)
					{
						outMessage = "invalid route";
						Logger.Add_TPL("New route is empty, Route0 has not changed", Logger.ForegroundLogFilename, Logger.Severity.Info);
					}
					else
					{
						await PersistentData.SetRoute0InDBAsync(newDataRecords).ConfigureAwait(false);
						Logger.Add_TPL("Route0 has been set in DB", Logger.ForegroundLogFilename, Logger.Severity.Info);
						outIsOk = true;
						outMessage = newDataRecords.Count + " route points loaded";
					}
				}
				catch (Exception exc) // OutOfMemoryException
				{
					var howMuchMemoryLeft0 = GC.GetTotalMemory(true);
					outMessage = "error reading GPX: " + exc.Message;
					//NotifyReadingException_UI(exc);
					Logger.Add_TPL("Error reading GPX: " + exc.ToString(), Logger.ForegroundLogFilename, Logger.Severity.Info);
				}
				GC.GetTotalMemory(true);
			}
			else // gpxFile is null
			{
				outMessage = "could not read GPX file";
				Logger.Add_TPL("GPX file null", Logger.ForegroundLogFilename, Logger.Severity.Info);
			}

			return Tuple.Create<bool, string>(outIsOk, outMessage);
		}
		private async static Task<Tuple<bool, string>> LoadLandmarksAsync(StorageFile gpxFile, CancellationToken token)
		{
			string outMessage = string.Empty;
			bool outIsOk = false;

			Logger.Add_TPL("Start reading Landmarks", Logger.ForegroundLogFilename, Logger.Severity.Info);
			if (gpxFile != null) // I could use class MemoryFailPoint in the following, to catch OutOfMemoryExceptions before they happen but it's old.
			{
				try
				{
					List<PointRecord> newDataRecords = await LoadDataRecordsAsync(gpxFile, PersistentData.Tables.Landmarks, token).ConfigureAwait(false);

					token.ThrowIfCancellationRequested();
					if (newDataRecords == null || newDataRecords.Count < 1)
					{
						outMessage = "invalid landmarks";
						Logger.Add_TPL("New landmarks are empty, landmarks have not changed", Logger.ForegroundLogFilename, Logger.Severity.Info);
					}
					else
					{
						await PersistentData.SetLandmarksInDBAsync(newDataRecords).ConfigureAwait(false);
						Logger.Add_TPL("Landmarks have been set in DB", Logger.ForegroundLogFilename, Logger.Severity.Info);
						outIsOk = true;
						outMessage = newDataRecords.Count + " landmarks loaded";
					}
				}
				catch (Exception exc) // OutOfMemoryException
				{
					var howMuchMemoryLeft0 = GC.GetTotalMemory(true);
					outMessage = "error reading GPX: " + exc.Message;
					Logger.Add_TPL("Error reading GPX: " + exc.ToString(), Logger.ForegroundLogFilename, Logger.Severity.Info);
				}
				GC.GetTotalMemory(true);
			}
			else // gpxFile is null
			{
				outMessage = "could not read GPX file";
				Logger.Add_TPL("GPX file null", Logger.ForegroundLogFilename, Logger.Severity.Info);
			}

			return Tuple.Create<bool, string>(outIsOk, outMessage);
		}
		private static async Task<List<PointRecord>> LoadDataRecordsAsync(StorageFile gpxFile, PersistentData.Tables whichTable, CancellationToken token)
		{
			List<PointRecord> newDataRecords = new List<PointRecord>();
			if (gpxFile == null) return newDataRecords;

			var fileProperties = await gpxFile.GetBasicPropertiesAsync();
			if (fileProperties.Size > ConstantData.MaxFileSize) return newDataRecords;

			try
			{
				using (IInputStream inStream2 = await gpxFile.OpenSequentialReadAsync().AsTask<IInputStream>().ConfigureAwait(false)) //OpenReadAsync() also works
				{
					XElement xmlData = XElement.Load(inStream2.AsStreamForRead());
					token.ThrowIfCancellationRequested();

					if (xmlData != null)
					{
						XNamespace xn = xmlData.GetDefaultNamespace();
						List<XElement> mapPoints = null;
						if (whichTable == PersistentData.Tables.Landmarks)
							mapPoints = GetWpts_Landmarks(xmlData, xn);
						else
							mapPoints = GetWpts_Route0(xmlData, xn);
						token.ThrowIfCancellationRequested();

						foreach (XElement xe in mapPoints)
						{
							double latitude = default(double);
							var lat = xe.Attribute("lat"); // no xn + with attributes
							if (lat != null) double.TryParse(lat.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out latitude);
							else continue; //do not add this point if lat is missing

							double longitude = default(double);
							var lon = xe.Attribute("lon"); // no xn + with attributes
							if (lon != null) double.TryParse(lon.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out longitude);
							else continue; //do not add this point if lon is missing

							double altitude = default(double);
							var ele = xe.Descendants(xn + "ele").FirstOrDefault();
							if (ele != null) double.TryParse(ele.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out altitude);

							string positionSource = string.Empty;
							var src = xe.Descendants(xn + "src").FirstOrDefault();
							if (src != null) positionSource = src.Value;

							DateTime timePoint = default(DateTime);                   // Date and time in are in Univeral Coordinated Time (UTC), not local time! Conforms to ISO 8601 specification for date/time representation. 
							var time = xe.Descendants(xn + "time").FirstOrDefault(); // Creation/modification timestamp for element. 
							if (time != null) DateTime.TryParse(time.Value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out timePoint);                     //Fractional seconds are allowed for millisecond timing in tracklogs. 

							UInt32 howManySatellites = default(UInt32);
							var sat = xe.Descendants(xn + "sat").FirstOrDefault();
							if (sat != null) UInt32.TryParse(sat.Value, out howManySatellites);

							double speedInMetreSec = default(double);
							var speed = xe.Descendants(xn + "speed").FirstOrDefault();
							if (speed != null) double.TryParse(speed.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out speedInMetreSec);

							//string gpsName = string.Empty;
							//var name = xe.Descendants(xn + "name").FirstOrDefault();
							//if (name != null) gpsName = name.Value;

							//string gpsComment = string.Empty;
							//var cmt = xe.Descendants(xn + "cmt").FirstOrDefault();
							//if (cmt != null) gpsComment = cmt.Value;

							string humanDescription = string.Empty;
							var desc = xe.Descendants(xn + "desc").FirstOrDefault();
							if (desc != null) humanDescription = desc.Value;

							string hyperLink = null;
							string hyperLinkText = string.Empty;
							var link = xe.Descendants(xn + "link").FirstOrDefault();
							if (link != null)
							{
								var href = link.Attribute("href"); // no xn + with attributes
								if (href != null)
								{
									Uri testUri = null;
									if (Uri.TryCreate(href.Value, UriKind.RelativeOrAbsolute, out testUri))
									{
										hyperLink = href.Value;
										var text = link.Descendants(xn + "text").FirstOrDefault();
										if (text != null) hyperLinkText = text.Value;
									}
								}
							}

							newDataRecords.Add(new PointRecord()
							{
								Altitude = altitude,
								Latitude = latitude,
								Longitude = longitude,
								PositionSource = positionSource,
								TimePoint = timePoint,
								HowManySatellites = howManySatellites,
								SpeedInMetreSec = speedInMetreSec,
								//GPSName = gpsName,
								//GPSComment = comment,
								HumanDescription = humanDescription,
								HyperLink = hyperLink,
								HyperLinkText = hyperLinkText
								//HorizontalDilutionOfPrecision = horizontalDilutionOfPrecision,
								//VerticalDilutionOfPrecision = verticalDilutionOfPrecision,
								//PositionDilutionOfPrecision = positionDilutionOfPrecision
							});
							token.ThrowIfCancellationRequested();
						}
					}
				}
			}
			catch (Exception exc)
			{
				Debug.WriteLine("ReadNodes raised " + exc.ToString());
			}
			return newDataRecords;
		}

		private static List<XElement> GetWpts_Route0(XElement xmlData, XNamespace xn)
		{
			List<XElement> mapPoints = new List<XElement>();
			var routesAndTracks = (from e in xmlData.DescendantsAndSelf()
								   select new { RouteElements = e.Descendants(xn + "rte"), TrackElements = e.Descendants(xn + "trk") }).FirstOrDefault();
			// Create a list of map points from the route <rte> element, otherwise use the track <trk> element.
			if (routesAndTracks.RouteElements.Count() > 0)
			{
				mapPoints = (from p in routesAndTracks.RouteElements.First().Descendants(xn + "rtept").Take(PersistentData.MaxRecordsInRoute) select p).ToList();
			}
			else if (routesAndTracks.TrackElements.Count() > 0)
			{
				mapPoints = (from p in routesAndTracks.TrackElements.First().Descendants(xn + "trkpt").Take(PersistentData.MaxRecordsInRoute) select p).ToList();
			}
			return mapPoints;
		}
		private static List<XElement> GetWpts_Landmarks(XElement xmlData, XNamespace xn)
		{
			return xmlData.Descendants(xn + "wpt").Take(PersistentData.MaxRecordsInLandmarks).ToList();
		}
		#endregion load route

		#region save route
		/// <summary>
		/// Save the tracking history into a GPX file.
		/// This method returns a task that is completed after saving the data and queues user notifications on the ui thread.
		/// This method should run on the threadpool.
		/// </summary>
		/// <param name="gpxFile"></param>
		/// <param name="coll"></param>
		/// <param name="fileCreationDateTime"></param>
		/// <param name="whichTable"></param>
		/// <param name="token"></param>
		/// <returns></returns>
		public static async Task<Tuple<bool, string>> SaveAsync(StorageFile gpxFile, Collection<PointRecord> coll, DateTime fileCreationDateTime, PersistentData.Tables whichTable, CancellationToken token)
		{
			string outMessage = string.Empty;
			bool outIsOk = false;

			Logger.Add_TPL("Start writing GPX", Logger.ForegroundLogFilename, Logger.Severity.Info);
			if (gpxFile != null)
			{
				try
				{
					XmlDocument gpxDoc = await GetEmptyXml(whichTable).ConfigureAwait(false);
					token.ThrowIfCancellationRequested();
					//await PersistentData.GetInstance().RunFunctionUnderSemaphore(
					//                   delegate
					//                   {
					EditXmlData(coll, gpxDoc, whichTable, token);
					EditXmlMetadata(coll, gpxDoc, fileCreationDateTime);
					//},
					//whichTable).ConfigureAwait(false);
					token.ThrowIfCancellationRequested();

					// we don't need this Prevent updates to the remote version of the file until we finish making changes and call CompleteUpdatesAsync. 
					// CachedFileManager.DeferUpdates(gpxFile); // http://msdn.microsoft.com/en-us/library/windows/apps/windows.storage.cachedfilemanager(v=win.10).aspx
					await gpxDoc.SaveToFileAsync(gpxFile).AsTask().ConfigureAwait(false);

					Logger.Add_TPL("File " + gpxFile.Name + " was saved", Logger.ForegroundLogFilename, Logger.Severity.Info);
					outIsOk = true;
					outMessage = "file saved";

					// this is only for testing
					//string contentString = null;
					//using (IInputStream inStream = gpxFile.OpenSequentialReadAsync().AsTask<IInputStream>().Result) //OpenReadAsync() also works
					//{
					//    using (StreamReader streamReader = new StreamReader(inStream.AsStreamForRead()))
					//    {
					//        contentString = streamReader.ReadToEnd();
					//        gpxDoc = new XmlDocument();
					//        gpxDoc.LoadXml(contentString);
					//    }
					//}
					//int qqq = contentString.Length;

				}
				catch (Exception exc1)
				{
					await Logger.AddAsync("Error writing GPX: " + exc1.ToString(), Logger.ForegroundLogFilename, Logger.Severity.Info).ConfigureAwait(false);
					outMessage = "file could not be saved, " + exc1.Message;
				}
			}
			else
			{
				Logger.Add_TPL("GPX file null", Logger.ForegroundLogFilename, Logger.Severity.Info);
				outMessage = "file could not be saved";
			}

			return Tuple.Create<bool, string>(outIsOk, outMessage);
		}

		private static void EditXmlData(Collection<PointRecord> coll, XmlDocument gpxDoc, PersistentData.Tables whichSeries, CancellationToken token)
		{
			if (whichSeries == PersistentData.Tables.History || whichSeries == PersistentData.Tables.Route0)
				EditXmlData_trk_trkseg_trkpt(coll, gpxDoc, token);
			else
				EditXmlData_wpt(coll, gpxDoc, token);
		}
		private static void EditXmlData_trk_trkseg_trkpt(Collection<PointRecord> coll, XmlDocument gpxDoc, CancellationToken token)
		{
			var nodeTrkseg = gpxDoc.GetElementsByTagName("trkseg")[0];
			object nameSpaceUri = gpxDoc.DocumentElement.GetAttribute("xmlns"); // we must use this and CreateElementNS, otherwise MS adds xmlns=\"\" and breaks the xml.
																				// this happens because it finds qualified names that come from a namespace but no declaration to resolve it.
			foreach (var dataRecord in coll)
			{
				XmlElement nodeTrkpt = gpxDoc.CreateElementNS(nameSpaceUri, "trkpt");
				EditXmlPointDetails(gpxDoc, nameSpaceUri, dataRecord, nodeTrkpt);

				nodeTrkseg.AppendChild(nodeTrkpt);
				token.ThrowIfCancellationRequested();
			}
		}
		private static void EditXmlData_wpt(Collection<PointRecord> coll, XmlDocument gpxDoc, CancellationToken token)
		{
			var nodeGpx = gpxDoc.GetElementsByTagName("gpx")[0];
			object nameSpaceUri = gpxDoc.DocumentElement.GetAttribute("xmlns"); // we must use this and CreateElementNS, otherwise MS adds xmlns=\"\" and breaks the xml.
																				// this happens because it finds qualified names that come from a namespace but no declaration to resolve it.
			foreach (var dataRecord in coll)
			{
				XmlElement nodeWpt = gpxDoc.CreateElementNS(nameSpaceUri, "wpt");
				EditXmlPointDetails(gpxDoc, nameSpaceUri, dataRecord, nodeWpt);

				nodeGpx.AppendChild(nodeWpt);
				token.ThrowIfCancellationRequested();
			}
		}

		private static void EditXmlPointDetails(XmlDocument gpxDoc, object nameSpaceUri, PointRecord dataRecord, XmlElement nodeTrkpt)
		{
			nodeTrkpt.SetAttribute("lat", dataRecord.Latitude.ToString(CultureInfo.InvariantCulture)); // no namespaces with attributes
			nodeTrkpt.SetAttribute("lon", dataRecord.Longitude.ToString(CultureInfo.InvariantCulture)); // no namespaces with attributes

			XmlElement nodeTrkptEle = gpxDoc.CreateElementNS(nameSpaceUri, "ele");
			nodeTrkptEle.InnerText = dataRecord.Altitude.ToString(CultureInfo.InvariantCulture);
			nodeTrkpt.AppendChild(nodeTrkptEle);

			XmlElement nodeTrkptTime = gpxDoc.CreateElementNS(nameSpaceUri, "time");
			nodeTrkptTime.InnerText = dataRecord.TimePoint.ToUniversalTime().ToString(ConstantData.DATE_TIME_FORMAT, CultureInfo.InvariantCulture);
			nodeTrkpt.AppendChild(nodeTrkptTime);

			if (!string.IsNullOrWhiteSpace(dataRecord.PositionSource))
			{
				XmlElement nodeTrkptSrc = gpxDoc.CreateElementNS(nameSpaceUri, "src");
				nodeTrkptSrc.InnerText = dataRecord.PositionSource;
				nodeTrkpt.AppendChild(nodeTrkptSrc);
			}

			if (dataRecord.HowManySatellites != 0)
			{
				XmlElement nodeTrkptSat = gpxDoc.CreateElementNS(nameSpaceUri, "sat");
				nodeTrkptSat.InnerText = dataRecord.HowManySatellites.ToString(CultureInfo.InvariantCulture);
				nodeTrkpt.AppendChild(nodeTrkptSat);
			}

			//XmlElement nodeTrkptName = gpxDoc.CreateElementNS(nameSpaceUri, "name");
			//nodeTrkptName.InnerText = dataRecord.GPSName ?? string.Empty;
			//if (!string.IsNullOrWhiteSpace(nodeTrkptName.InnerText)) nodeWpt.AppendChild(nodeTrkptName);

			//XmlElement nodeTrkptCmt = gpxDoc.CreateElementNS(nameSpaceUri, "cmt");
			//nodeTrkptCmt.InnerText = dataRecord.GPSComment ?? string.Empty;
			//if (!string.IsNullOrWhiteSpace(nodeTrkptCmt.InnerText)) nodeWpt.AppendChild(nodeTrkptCmt);

			// speed is an optional field in GPX and http://www.topografix.com/gpx_manual.asp#speed is obsolete, but it works here.
			if (dataRecord.SpeedInMetreSec != default(double))
			{
				XmlElement nodeTrkptSpeed = gpxDoc.CreateElementNS(nameSpaceUri, "speed");
				nodeTrkptSpeed.InnerText = dataRecord.SpeedInMetreSec.ToString(CultureInfo.InvariantCulture);
				nodeTrkpt.AppendChild(nodeTrkptSpeed);
			}

			if (!string.IsNullOrWhiteSpace(dataRecord.HumanDescription))
			{
				XmlElement nodeTrkptDesc = gpxDoc.CreateElementNS(nameSpaceUri, "desc");
				nodeTrkptDesc.InnerText = dataRecord.HumanDescription;
				nodeTrkpt.AppendChild(nodeTrkptDesc);
			}

			if (!string.IsNullOrWhiteSpace(dataRecord.HyperLink))
			{
				XmlElement nodeTrkptHL = gpxDoc.CreateElementNS(nameSpaceUri, "link");
				nodeTrkptHL.SetAttribute("href", dataRecord.HyperLink); // no namespaces with attributes
				if (!string.IsNullOrWhiteSpace(dataRecord.HyperLinkText))
				{
					XmlElement nodeTrkptHLText = gpxDoc.CreateElementNS(nameSpaceUri, "text");
					nodeTrkptHLText.InnerText = dataRecord.HyperLinkText;
					nodeTrkptHL.AppendChild(nodeTrkptHLText);
				}
				nodeTrkpt.AppendChild(nodeTrkptHL);
			}
		}

		private static void EditXmlMetadata(Collection<PointRecord> coll, XmlDocument gpxDoc, DateTime fileCreationDateTime)
		{
			if (coll != null && gpxDoc != null)
			{
				try
				{
					XmlElement nodeMetadata = gpxDoc.GetElementsByTagName("metadata")[0] as XmlElement;
					//string strMetadata = nodeMetadata.GetXml();
					XmlElement nodeTime = nodeMetadata.GetElementsByTagName("time")[0] as XmlElement;
					nodeTime.FirstChild.NodeValue = fileCreationDateTime.ToUniversalTime().ToString(ConstantData.DATE_TIME_FORMAT, CultureInfo.InvariantCulture);

					XmlElement nodeBounds = nodeMetadata.GetElementsByTagName("bounds")[0] as XmlElement;
					if (coll.Count > 0)
					{
						nodeBounds.SetAttribute("minlat", coll.Min(a => a.Latitude).ToString(CultureInfo.InvariantCulture));
						nodeBounds.SetAttribute("maxlat", coll.Max(a => a.Latitude).ToString(CultureInfo.InvariantCulture));
						nodeBounds.SetAttribute("minlon", coll.Min(a => a.Longitude).ToString(CultureInfo.InvariantCulture));
						nodeBounds.SetAttribute("maxlon", coll.Max(a => a.Longitude).ToString(CultureInfo.InvariantCulture));
					}
					else
					{
						nodeBounds.SetAttribute("minlat", "0");
						nodeBounds.SetAttribute("maxlat", "0");
						nodeBounds.SetAttribute("minlon", "0");
						nodeBounds.SetAttribute("maxlon", "0");
					}
				}
				catch (Exception exc0)
				{
					Logger.Add_TPL(exc0.ToString(), Logger.ForegroundLogFilename);
				}
			}
		}

		private static async Task<XmlDocument> GetEmptyXml(PersistentData.Tables whichSeries)
		{
			XmlDocument gpxDoc = null;
			StorageFolder installLocationFolder = Windows.ApplicationModel.Package.Current.InstalledLocation;
			StorageFolder assetsFolder = await installLocationFolder.GetFolderAsync("Assets").AsTask<StorageFolder>().ConfigureAwait(false);
			StorageFile emptyGpxFile = null;
			if (whichSeries == PersistentData.Tables.History || whichSeries == PersistentData.Tables.Route0)
				emptyGpxFile = await assetsFolder.GetFileAsync("Empty_trk_trkseg_trkpt.gpx").AsTask<StorageFile>().ConfigureAwait(false);
			else
				emptyGpxFile = await assetsFolder.GetFileAsync("Empty_wpt.gpx").AsTask<StorageFile>().ConfigureAwait(false);
			using (IInputStream inStream = await emptyGpxFile.OpenSequentialReadAsync().AsTask<IInputStream>().ConfigureAwait(false)) //OpenReadAsync() also works
			{
				using (StreamReader streamReader = new StreamReader(inStream.AsStreamForRead()))
				{
					string content = streamReader.ReadToEnd();
					gpxDoc = new XmlDocument();
					gpxDoc.LoadXml(content);
				}
			}

			return gpxDoc;
		}
		#endregion save route
	}
}
