using Utilz.Controlz;
using LolloChartMobile;
using LolloGPS.Data;
using LolloGPS.Data.Runtime;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.Core;
using Utilz;
using Windows.UI.Xaml;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236
// Multithreading documentation:
//https://msdn.microsoft.com/en-us/library/vstudio/hh191443(v=vs.110).aspx
//http://blogs.msdn.com/b/pfxteam/archive/2011/10/24/10229468.aspx
//https://msdn.microsoft.com/en-us/magazine/jj991977.aspx

namespace LolloGPS.Core
{
	public sealed partial class AltitudeProfiles : OpenableObservableControl, IMapAltProfCentrer, IInfoPanelEventReceiver
	{
		#region properties
		public MainVM MainVM
		{
			get { return (MainVM)GetValue(MainVMProperty); }
			set { SetValue(MainVMProperty, value); }
		}
		public static readonly DependencyProperty MainVMProperty =
			DependencyProperty.Register("MainVM", typeof(MainVM), typeof(AltitudeProfiles), new PropertyMetadata(null));

		private AltitudeProfilesVM _altitudeProfilesVM = null;
		public AltitudeProfilesVM AltitudeProfilesVM { get { return _altitudeProfilesVM; } }

		public PersistentData PersistentData { get { return App.PersistentData; } }
		public RuntimeData RuntimeData { get { return App.RuntimeData; } }

		private readonly SemaphoreSlimSafeRelease _drawSemaphore = new SemaphoreSlimSafeRelease(1, 1);
		#endregion properties


		#region lifecycle
		public AltitudeProfiles()
		{
			InitializeComponent();
		}
		protected override Task OpenMayOverrideAsync()
		{
			Logger.Add_TPL("AltitudeProfiles started OpenMayOverrideAsync()", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
			_altitudeProfilesVM = new AltitudeProfilesVM(MainVM);
			RaisePropertyChanged_UI(nameof(AltitudeProfilesVM));

			HistoryChart.Open();
			Route0Chart.Open();
			CheckpointsChart.Open();

			AddHandlers();

			// if the app is file activating, ignore the series that are already present; 
			// the newly read series will draw automatically once they are pushed in and the chosen one will be centred with an external command.
			if (App.IsFileActivating) return Task.CompletedTask;

			// this panel may be hidden: if so, do not draw anything
			if (!PersistentData.IsShowingAltitudeProfiles) return Task.CompletedTask;

			Task drawH = Task.Run(DrawHistoryAsync);
			if (!App.IsResuming || MainVM.WhichSeriesJustLoaded == PersistentData.Tables.Route0)
			{
				Task drawR = Task.Run(DrawRoute0Async);
			}
			if (!App.IsResuming || MainVM.WhichSeriesJustLoaded == PersistentData.Tables.Checkpoints)
			{
				Task drawC = Task.Run(DrawCheckpointsAsync);
			}
			if (!App.IsResuming)
			{
				var whichSeriesIsJustLoaded = MainVM.WhichSeriesJustLoaded; // I read it now to avoid switching threads later
				Task restore = Task.Run(() => RestoreViewCenteringAsync(whichSeriesIsJustLoaded));
			}

			return Task.CompletedTask;
		}

		protected override Task CloseMayOverrideAsync()
		{
			RemoveHandlers();

			PersistentData.AltLastVScroll = MyScrollViewer.VerticalOffset;

			HistoryChart.Close();
			Route0Chart.Close();
			CheckpointsChart.Close();

			return Task.CompletedTask;
		}
		#endregion lifecycle


		#region data event handlers
		private void AddHandlers()
		{
			if (PersistentData != null)
			{
				PersistentData.PropertyChanged += OnPersistentData_PropertyChanged;
				PersistentData.CurrentChanged += OnPersistentData_CurrentChanged;
				PersistentData.History.CollectionChanged += OnHistory_CollectionChanged;
				PersistentData.Route0.CollectionChanged += OnRoute0_CollectionChanged;
				PersistentData.Checkpoints.CollectionChanged += OnCheckpoints_CollectionChanged;
			}
		}

		private void RemoveHandlers()
		{
			if (PersistentData != null)
			{
				PersistentData.PropertyChanged -= OnPersistentData_PropertyChanged;
				PersistentData.CurrentChanged -= OnPersistentData_CurrentChanged;
				PersistentData.History.CollectionChanged -= OnHistory_CollectionChanged;
				PersistentData.Route0.CollectionChanged -= OnRoute0_CollectionChanged;
				PersistentData.Checkpoints.CollectionChanged -= OnCheckpoints_CollectionChanged;
			}
		}

		private void OnPersistentData_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if ((e.PropertyName == nameof(PersistentData.IsShowingAltitudeProfiles) || e.PropertyName == nameof(PersistentData.IsShowImperialUnits))
				&& PersistentData.IsShowingAltitudeProfiles)
			{
				//bool isVisible = await GetIsVisibleAsync().ConfigureAwait(false);
				Task drawH = RunFunctionIfOpenAsyncT_MT(DrawHistoryAsync);
				Task drawR = RunFunctionIfOpenAsyncT_MT(DrawRoute0Async);
				Task drawC = RunFunctionIfOpenAsyncT_MT(DrawCheckpointsAsync);
			}
		}
		private void OnPersistentData_CurrentChanged(object sender, EventArgs e)
		{
			// I must not run to the current point when starting, I want to stick to the last frame when last suspended instead.
			// Unless the tracking is on and the autocentre too.
			if (PersistentData.IsShowingAltitudeProfiles)
			{
				Task cur = RunFunctionIfOpenAsyncA(() =>
				{
					if (PersistentData?.IsCentreOnCurrent == true && RuntimeData.IsAllowCentreOnCurrent)
					{
						Task cen = CentreOnHistoryAsync();
					}
				});
			}
		}
		private void OnHistory_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			if ((e.Action != NotifyCollectionChangedAction.Reset || !PersistentData.History.Any())
				&& PersistentData.IsShowingAltitudeProfiles)
			{
				Task draw = RunFunctionIfOpenAsyncT_MT(async delegate
				{
					await DrawHistoryAsync().ConfigureAwait(false);
					// if (e.Action == NotifyCollectionChangedAction.Replace) await CentreOnHistoryAsync().ConfigureAwait(false);
				});
			}
		}
		private void OnRoute0_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			if ((e.Action != NotifyCollectionChangedAction.Reset || !PersistentData.Route0.Any())
				&& PersistentData.IsShowingAltitudeProfiles)
			{
				Task draw = RunFunctionIfOpenAsyncT_MT(async delegate
				{
					await DrawRoute0Async().ConfigureAwait(false);
					// if (e.Action == NotifyCollectionChangedAction.Replace) await CentreOnRoute0Async().ConfigureAwait(false);
				});
			}
		}
		private void OnCheckpoints_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			if ((e.Action != NotifyCollectionChangedAction.Reset || !PersistentData.Checkpoints.Any())
				&& PersistentData.IsShowingAltitudeProfiles)
			{
				Task draw = RunFunctionIfOpenAsyncT_MT(async delegate
				{
					await DrawCheckpointsAsync().ConfigureAwait(false);
					// if (e.Action == NotifyCollectionChangedAction.Replace) await CentreOnCheckpointsAsync().ConfigureAwait(false);
				});
			}
		}
		#endregion data event handlers


		#region user event handlers
		public void OnInfoPanelPointChanged(object sender, EventArgs e)
		{
			Task cen = RunFunctionIfOpenAsyncA(delegate
			{
				try
				{
					// leave if there is no selected point
					if (!PersistentData.IsSelectedSeriesNonNullAndNonEmpty()) return;
					// draw the selected point
					switch (PersistentData.SelectedSeries)
					{
						case PersistentData.Tables.History:
							HistoryChart.CrossPoint(HistoryChart.XY1DataSeries, PersistentData.SelectedIndex_Base1 - 1, PersistentData.Selected.Altitude);
							break;
						case PersistentData.Tables.Route0:
							Route0Chart.CrossPoint(Route0Chart.XY1DataSeries, PersistentData.SelectedIndex_Base1 - 1, PersistentData.Selected.Altitude);
							break;
						case PersistentData.Tables.Checkpoints:
							CheckpointsChart.CrossPoint(CheckpointsChart.XY1DataSeries, PersistentData.SelectedIndex_Base1 - 1, PersistentData.Selected.Altitude);
							break;
						default:
							break;
					}
					// if this panel is being displayed, centre it
					if (!PersistentData.IsShowingAltitudeProfiles) return;
					switch (PersistentData.SelectedSeries)
					{
						case PersistentData.Tables.History:
							CentreOnHistoryAsync();
							break;
						case PersistentData.Tables.Route0:
							CentreOnRoute0Async();
							break;
						case PersistentData.Tables.Checkpoints:
							CentreOnCheckpointsAsync();
							break;
						default:
							break;
					}
				}
				catch (Exception ex)
				{
					Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
				}
			});
		}

		public void OnInfoPanelClosed(object sender, object e)
		{
			try
			{
				switch (PersistentData.SelectedSeries)
				{
					case PersistentData.Tables.History:
						HistoryChart.UncrossPoint(HistoryChart.XY1DataSeries);
						break;
					case PersistentData.Tables.Route0:
						Route0Chart.UncrossPoint(Route0Chart.XY1DataSeries);
						break;
					case PersistentData.Tables.Checkpoints:
						CheckpointsChart.UncrossPoint(CheckpointsChart.XY1DataSeries);
						break;
					default:
						break;
				}
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
			}
		}

		public event EventHandler<ShowOnePointDetailsRequestedArgs> ShowOnePointDetailsRequested;
		public sealed class ShowOnePointDetailsRequestedArgs : EventArgs
		{
			private readonly PointRecord _selectedRecord;
			public PointRecord SelectedRecord { get { return _selectedRecord; } }
			private readonly PersistentData.Tables _selectedSeries;
			public PersistentData.Tables SelectedSeries { get { return _selectedSeries; } }
			public ShowOnePointDetailsRequestedArgs(PointRecord selectedRecord, PersistentData.Tables selectedSeries)
			{
				_selectedRecord = selectedRecord;
				_selectedSeries = selectedSeries;
			}
		}

		// the following methods do not seem critical for mt: I am in a screen where, at most, a history point could be added.
		private void OnHistoryChartTapped(object sender, LolloChart.ChartTappedArguments e)
		{
			try
			{
				if (e.XMax * PersistentData.History.Count > 0)
				{
					ShowOnePointDetailsRequested?.Invoke(this, new ShowOnePointDetailsRequestedArgs(PersistentData.History[Convert.ToInt32(Math.Floor(e.X / e.XMax * PersistentData.History.Count))], PersistentData.Tables.History));
				}
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
			}
		}
		private void OnRoute0ChartTapped(object sender, LolloChart.ChartTappedArguments e)
		{
			try
			{
				if (e.X / e.XMax * PersistentData.Route0.Count > 0)
				{
					ShowOnePointDetailsRequested?.Invoke(this, new ShowOnePointDetailsRequestedArgs(PersistentData.Route0[Convert.ToInt32(Math.Floor(e.X / e.XMax * PersistentData.Route0.Count))], PersistentData.Tables.Route0));
				}
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
			}
		}
		private void OnCheckpointsChartTapped(object sender, LolloChart.ChartTappedArguments e)
		{
			try
			{
				if (e.X / e.XMax * PersistentData.Checkpoints.Count > 0)
				{
					ShowOnePointDetailsRequested?.Invoke(this, new ShowOnePointDetailsRequestedArgs(PersistentData.Checkpoints[Convert.ToInt32(Math.Floor(e.X / e.XMax * PersistentData.Checkpoints.Count))], PersistentData.Tables.Checkpoints));
				}
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
			}
		}
		#endregion user event handlers


		#region services
		private async Task RestoreViewCenteringAsync(PersistentData.Tables whichSeriesJustLoaded)
		{
			try
			{
				await _drawSemaphore.WaitAsync(CancToken).ConfigureAwait(false);

				//var whichSeriesJustLoaded = PersistentData.Tables.Nil;
				//// LOLLO NOTE dependency properties (MainVM here) must be referenced in the UI thread
				//await RunInUiThreadAsync(() => { whichSeriesJustLoaded = MainVM.WhichSeriesJustLoaded; }).ConfigureAwait(false);

				if (whichSeriesJustLoaded == PersistentData.Tables.Nil) await RunInUiThreadAsync(delegate { MyScrollViewer.ChangeView(0.0, PersistentData.AltLastVScroll, 1, true); }).ConfigureAwait(false);
				//else if (whichSeriesJustLoaded == PersistentData.Tables.History) await CentreOnHistoryAsync().ConfigureAwait(false);
				//else if (whichSeriesJustLoaded == PersistentData.Tables.Route0) await CentreOnRoute0Async().ConfigureAwait(false);
				//else if (whichSeriesJustLoaded == PersistentData.Tables.Checkpoints) await CentreOnCheckpointsAsync().ConfigureAwait(false);
			}
			catch { }
			finally
			{
				SemaphoreSlimSafeRelease.TryRelease(_drawSemaphore);
			}
		}

		//private async Task<bool> GetIsVisibleAsync()
		//{
		//	bool isVisible = false;
		//	await RunInUiThreadAsync(delegate
		//	{
		//		isVisible = (Visibility == Visibility.Visible);
		//	}).ConfigureAwait(false);
		//	return isVisible;
		//}
		private async Task DrawHistoryAsync()
		{
			try
			{
				await _drawSemaphore.WaitAsync(CancToken).ConfigureAwait(false);
				await DrawOneSeriesAsync(PersistentData.History, HistoryChart, false, true).ConfigureAwait(false);
			}
			catch (OperationCanceledException) { } // fires when cts is cancelled
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
			}
			finally
			{
				SemaphoreSlimSafeRelease.TryRelease(_drawSemaphore);
			}
		}
		private async Task DrawRoute0Async()
		{
			try
			{
				await _drawSemaphore.WaitAsync(CancToken).ConfigureAwait(false);
				await DrawOneSeriesAsync(PersistentData.Route0, Route0Chart, false, true).ConfigureAwait(false);
			}
			catch (OperationCanceledException) { } // fires when cts is cancelled
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
			}
			finally
			{
				SemaphoreSlimSafeRelease.TryRelease(_drawSemaphore);
			}
		}
		private async Task DrawCheckpointsAsync()
		{
			try
			{
				await _drawSemaphore.WaitAsync(CancToken).ConfigureAwait(false);
				await DrawOneSeriesAsync(PersistentData.Checkpoints, CheckpointsChart, true, false).ConfigureAwait(false);
			}
			catch (OperationCanceledException) { } // fires when cts is cancelled
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
			}
			finally
			{
				SemaphoreSlimSafeRelease.TryRelease(_drawSemaphore);
			}
		}

		private async Task DrawOneSeriesAsync(Collection<PointRecord> coll, LolloChart chart, bool isHistogram, bool respectDatesAndTimes)
		{
			bool sortIfRespectingDatesAndTimes = true;
			double maxAltitude = default(double), minAltitude = default(double), maxTime = default(double), minTime = default(double);
			double[,] points = null;

			if (CancToken.IsCancellationRequested) return;

			_altitudeProfilesVM.InitialiseChartData(coll, respectDatesAndTimes, sortIfRespectingDatesAndTimes,
				ref maxAltitude, ref minAltitude, ref maxTime, ref minTime, ref points);

			if (CancToken.IsCancellationRequested) return;

			await RunInUiThreadAsync(delegate
			{
				DrawOneSeriesUI(maxAltitude, minAltitude, maxTime, minTime, points, chart, isHistogram);
			}).ConfigureAwait(false);
		}

		private void DrawOneSeriesUI(double maxAltitude, double minAltitude, double maxTime, double minTime, double[,] points,
			LolloChart chart, bool isHistogram)
		{
			chart.Visibility = Visibility.Collapsed; // we set the visibility again after drawing, it seems a little faster
			if (PersistentData != null && points != null && points.GetUpperBound(0) >= 0)
			{
				try
				{
#if DEBUG
					Stopwatch sw = new Stopwatch(); sw.Start();
#endif
					if (CancToken.IsCancellationRequested) return;

					chart.XGridScale = new GridScale(ScaleType.Linear, minTime, maxTime);
					chart.XY1DataSeries = new XYDataSeries(points, isHistogram);
					double[] xLabels = { maxTime, minTime };
					//chart.XGridLabels = new GridLabels(xLabels);
					chart.XPrimaryGridLines = new GridLines(xLabels);

					chart.Y1GridScale = new GridScale(ScaleType.Linear, minAltitude, maxAltitude);
					double[] yLabels = {
						maxAltitude,
						minAltitude + (maxAltitude - minAltitude) * .75,
						minAltitude + (maxAltitude - minAltitude) * .5,
						minAltitude + (maxAltitude - minAltitude) * .25,
						minAltitude };
					chart.YPrimaryGridLines = new GridLines(yLabels);
					chart.Y1GridLabels = PersistentData.IsShowImperialUnits ? new GridLabels(yLabels, "#0. ft") : new GridLabels(yLabels, "#0. m");

					if (CancToken.IsCancellationRequested) return;

					chart.Draw();

					chart.Visibility = Visibility.Visible;

#if DEBUG
					sw.Stop();
					Debug.WriteLine("DrawOneSeries took " + sw.ElapsedMilliseconds + " ms to draw the chart");
#endif
				}
				catch (Exception ex)
				{
					Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
				}
			}
			RefreshTBNoData();
		}

		private void RefreshTBNoData()
		{
			try
			{
				if (HistoryChart.Visibility == Visibility.Collapsed && Route0Chart.Visibility == Visibility.Collapsed && CheckpointsChart.Visibility == Visibility.Collapsed)
				{
					TBNoDataGrid.Visibility = Visibility.Visible;
				}
				else
				{
					TBNoDataGrid.Visibility = Visibility.Collapsed;
				}
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
			}
		}
		#endregion services


		#region IMapAltProfCentrer
		public Task CentreOnHistoryAsync()
		{
			try
			{
				return RunInUiThreadIdleAsync(delegate
				{
					if (PersistentData?.IsShowingAltitudeProfiles == true && HistoryChart.Visibility == Visibility.Visible)
					{
						MyScrollViewer.ChangeView(0.0, 0.0, 1, false);
					}
				});
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
			}
			return Task.CompletedTask;
		}

		public Task CentreOnRoute0Async()
		{
			try
			{
				return RunInUiThreadIdleAsync(delegate
				{
					if (PersistentData?.IsShowingAltitudeProfiles == true && Route0Chart.Visibility == Visibility.Visible)
					{
						double vOffset = HistoryChart.Visibility == Visibility.Visible ? HistoryChart.ActualHeight : 0.0;
						MyScrollViewer.ChangeView(0.0, vOffset, 1, false);
					}
				});
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
			}
			return Task.CompletedTask;
		}

		public Task CentreOnCheckpointsAsync()
		{
			try
			{
				return RunInUiThreadIdleAsync(delegate
				{
					if (PersistentData?.IsShowingAltitudeProfiles == true && CheckpointsChart.Visibility == Visibility.Visible)
					{
						double vOffset = HistoryChart.Visibility == Visibility.Visible ? HistoryChart.ActualHeight : 0.0;
						if (Route0Chart.Visibility == Visibility.Visible) vOffset += Route0Chart.ActualHeight;
						MyScrollViewer.ChangeView(0.0, vOffset, 1, false);
					}
				});
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
			}
			return Task.CompletedTask;
		}

		public Task CentreOnTargetAsync()
		{
			return Task.CompletedTask; // altitude profiles has no target
		}

		public Task CentreOnCurrentAsync()
		{
			return CentreOnHistoryAsync();
		}

		public Task Goto2DAsync()
		{
			try
			{
				return RunInUiThreadIdleAsync(delegate
				{
					MyScrollViewer.ChangeView(0.0, 0.0, 1, false);
				});
			}
			catch { }
			return Task.CompletedTask;
		}
		#endregion IMapAltProfCentrer
	}
}