using System.Threading.Tasks;
using Utilz.Data;

namespace UnitTestProject1
{
	public class LongRunnerNoSemaphore : OpenableObservableData
	{
		private int _howManyDelays = -1;
		public int HowManyDelays { get { return _howManyDelays; } }
		public async Task ManySeconds()
		{
			_howManyDelays = 0;
			if (Cts.IsCancellationRequestedSafe) return;
			await Task.Delay(2000);

			_howManyDelays ++;
			if (Cts.IsCancellationRequestedSafe) return;
			await Task.Delay(2000);

			_howManyDelays ++;
			if (Cts.IsCancellationRequestedSafe) return;
			await Task.Delay(2000);

			_howManyDelays++;
			if (Cts.IsCancellationRequestedSafe) return;
			await Task.Delay(2000);

			_howManyDelays++;
			if (Cts.IsCancellationRequestedSafe) return;
			await Task.Delay(2000);
		}

		public void Cancel()
		{
			Cts?.CancelSafe(true);
		}
	}
	public class LongRunnerUnderSemaphore : OpenableObservableData
	{
		private int _howManyDelays = -1;
		public int HowManyDelays { get { return _howManyDelays; } }
		public Task ManySecondsWhenOpen()
		{
			return RunFunctionIfOpenAsyncT(async delegate
			{
				_howManyDelays = 0;
				if (Cts.IsCancellationRequestedSafe) return;
				await Task.Delay(2000);

				_howManyDelays++;
				if (Cts.IsCancellationRequestedSafe) return;
				await Task.Delay(2000);

				_howManyDelays++;
				if (Cts.IsCancellationRequestedSafe) return;
				await Task.Delay(2000);

				_howManyDelays++;
				if (Cts.IsCancellationRequestedSafe) return;
				await Task.Delay(2000);

				_howManyDelays++;
				if (Cts.IsCancellationRequestedSafe) return;
				await Task.Delay(2000);
			});
		}
		public void Cancel()
		{
			Cts?.CancelSafe(true);
		}
	}
}
