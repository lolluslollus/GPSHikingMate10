using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Utilz.Data
{
	public abstract class OpenableObservableData : ObservableData
	{
		#region properties
		protected volatile SemaphoreSlimSafeRelease _isOpenSemaphore = null;

		protected volatile bool _isOpen = false;
		public bool IsOpen { get { return _isOpen; } protected set { if (_isOpen != value) { _isOpen = value; RaisePropertyChanged_UI(); } } }

		private volatile SmartCancellationTokenSource _cts = null;
		protected SmartCancellationTokenSource Cts { get { return _cts; } }
		protected CancellationToken CancToken
		{
			get
			{
				var cts = _cts;
				if (cts.IsAlive()) return cts.Token;
				else if (cts.IsDisposed) return new CancellationToken(true);
				else return new CancellationToken(false); // we must be optimistic, or the methods running in separate tasks will always crap out
			}
		}
		#endregion properties


		#region open close
		public async Task<bool> OpenAsync()
		{
			if (!_isOpen)
			{
				if (!SemaphoreSlimSafeRelease.IsAlive(_isOpenSemaphore)) _isOpenSemaphore = new SemaphoreSlimSafeRelease(1, 1);
				try
				{
					await _isOpenSemaphore.WaitAsync().ConfigureAwait(false);
					if (!_isOpen)
					{
						var cts = _cts; if (cts != null) cts.Dispose();// LOLLO TODO TEST THIS
						_cts = new SmartCancellationTokenSource();

						await OpenMayOverrideAsync().ConfigureAwait(false);

						IsOpen = true;
						return true;
					}
				}
				catch (Exception ex)
				{
					if (SemaphoreSlimSafeRelease.IsAlive(_isOpenSemaphore))
						await Logger.AddAsync(GetType().Name + ex.ToString(), Logger.ForegroundLogFilename);
				}
				finally
				{
					SemaphoreSlimSafeRelease.TryRelease(_isOpenSemaphore);
				}
			}
			return false;
		}

		protected virtual Task OpenMayOverrideAsync()
		{
			return Task.CompletedTask; // avoid warning
		}

		public virtual async Task<bool> CloseAsync()
		{
			if (_isOpen)
			{
				_cts?.CancelSafe(true);

				try
				{
					await _isOpenSemaphore.WaitAsync().ConfigureAwait(false);
					_cts?.Dispose();
					_cts = null;

					if (_isOpen)
					{
						IsOpen = false;
						await CloseMayOverrideAsync().ConfigureAwait(false);
						return true;
					}
				}
				catch (Exception ex)
				{
					if (SemaphoreSlimSafeRelease.IsAlive(_isOpenSemaphore))
						await Logger.AddAsync(GetType().Name + ex.ToString(), Logger.ForegroundLogFilename);
				}
				finally
				{
					SemaphoreSlimSafeRelease.TryDispose(_isOpenSemaphore);
					_isOpenSemaphore = null;
				}
			}
			return false;
		}

		protected virtual Task CloseMayOverrideAsync()
		{
			return Task.CompletedTask; // avoid warning
		}
		#endregion open close


		#region while open
		protected async Task<bool> RunFunctionIfOpenAsyncA(Action func)
		{
			if (_isOpen)
			{
				try
				{
					await _isOpenSemaphore.WaitAsync(); //.ConfigureAwait(false);
					if (_isOpen)
					{
						func();
						return true;
					}
				}
				catch (Exception ex)
				{
					if (SemaphoreSlimSafeRelease.IsAlive(_isOpenSemaphore))
						await Logger.AddAsync(GetType().Name + ex.ToString(), Logger.ForegroundLogFilename);
				}
				finally
				{
					SemaphoreSlimSafeRelease.TryRelease(_isOpenSemaphore);
				}
			}
			return false;
		}
		protected async Task<bool> RunFunctionIfOpenAsyncB(Func<bool> func)
		{
			if (_isOpen)
			{
				try
				{
					await _isOpenSemaphore.WaitAsync(); //.ConfigureAwait(false);
					if (_isOpen) return func();
				}
				catch (Exception ex)
				{
					if (SemaphoreSlimSafeRelease.IsAlive(_isOpenSemaphore))
						await Logger.AddAsync(GetType().Name + ex.ToString(), Logger.ForegroundLogFilename);
				}
				finally
				{
					SemaphoreSlimSafeRelease.TryRelease(_isOpenSemaphore);
				}
			}
			return false;
		}
		protected async Task<bool> RunFunctionIfOpenAsyncT(Func<Task> funcAsync)
		{
			if (_isOpen)
			{
				try
				{
					await _isOpenSemaphore.WaitAsync(); //.ConfigureAwait(false);
					if (_isOpen)
					{
						await funcAsync().ConfigureAwait(false);
						return true;
					}
				}
				catch (Exception ex)
				{
					if (SemaphoreSlimSafeRelease.IsAlive(_isOpenSemaphore))
						await Logger.AddAsync(GetType().Name + ex.ToString(), Logger.ForegroundLogFilename);
				}
				finally
				{
					SemaphoreSlimSafeRelease.TryRelease(_isOpenSemaphore);
				}
			}
			return false;
		}
		protected async Task<bool> RunFunctionIfOpenAsyncTB(Func<Task<bool>> funcAsync)
		{
			if (_isOpen)
			{
				try
				{
					await _isOpenSemaphore.WaitAsync(); //.ConfigureAwait(false);
					if (_isOpen) return await funcAsync().ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					if (SemaphoreSlimSafeRelease.IsAlive(_isOpenSemaphore))
						await Logger.AddAsync(GetType().Name + ex.ToString(), Logger.ForegroundLogFilename);
				}
				finally
				{
					SemaphoreSlimSafeRelease.TryRelease(_isOpenSemaphore);
				}
			}
			return false;
		}
		protected async Task<bool> RunFunctionIfOpenAsyncT_MT(Func<Task> funcAsync)
		{
			if (_isOpen)
			{
				try
				{
					await _isOpenSemaphore.WaitAsync(); //.ConfigureAwait(false);
					if (_isOpen)
					{
						await Task.Run(delegate { return funcAsync(); }).ConfigureAwait(false);
						return true;
					}
				}
				catch (Exception ex)
				{
					if (SemaphoreSlimSafeRelease.IsAlive(_isOpenSemaphore))
						await Logger.AddAsync(GetType().Name + ex.ToString(), Logger.ForegroundLogFilename);
				}
				finally
				{
					SemaphoreSlimSafeRelease.TryRelease(_isOpenSemaphore);
				}
			}
			return false;
		}
		#endregion while open
	}
}
