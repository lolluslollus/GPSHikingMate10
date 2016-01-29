using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Utilz
{
	public class SafeCancellationTokenSource : CancellationTokenSource
	{
		private volatile bool _isDisposed = false;
		public bool IsDisposed { get { return _isDisposed; } }
		protected override void Dispose(bool disposing)
		{
			_isDisposed = true;
			base.Dispose(disposing);
		}
		public void CancelSafe(bool throwOnFirstException = false)
		{
			try
			{
				if (!_isDisposed) Cancel(throwOnFirstException);
			}
			catch (OperationCanceledException) { } // maniman
			catch (ObjectDisposedException) { }
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
			}
		}
		public bool IsCancellationRequestedSafe
		{
			get
			{
				try
				{
					if (!_isDisposed) return Token.IsCancellationRequested;
					else return true;
				}
				catch (OperationCanceledException) // maniman
				{
					return true;
				}
				catch (ObjectDisposedException)
				{
					return true;
				}
				catch (Exception ex)
				{
					Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
					return true;
				}
			}
		}
	}
	public static class SmartCancellationTokenSourceExtensions
	{
		public static bool IsAlive(this SafeCancellationTokenSource cts)
		{
			var lcts = cts;
			return (lcts != null && !lcts.IsDisposed);
		}
	}
}