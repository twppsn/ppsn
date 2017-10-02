using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TecWare.PPSn.Stuff
{
	public sealed class LazyProperty<T>
	{
		#region -- enum LazyPropertyState -----------------------------------------------

		private enum LazyPropertyState
		{
			Nothing,
			Loading,
			Loaded
		} // enum LazyPropertyState

		#endregion

		private readonly Func<Task<T>> getValue;
		private readonly Action onPropertyChanged;

		private T value;
		private LazyPropertyState state;

		private List<TaskCompletionSource<T>> valueListener = new List<TaskCompletionSource<T>>();

		public LazyProperty(Func<Task<T>> getValue, Action onPropertyChanged)
		{
			this.getValue = getValue;
			this.onPropertyChanged = onPropertyChanged;

			Reset();
		} // ctor

		public void Reset()
		{
			this.state = LazyPropertyState.Nothing;
			this.value = default(T);

			onPropertyChanged?.Invoke();
		} // proc Reset

		private async Task<T> GetDataInternalAsync()
		{
			try
			{
				return SetValueIntern(await getValue(), null);
			}
			catch (Exception e)
			{
				SetValueIntern(default(T), e);
				throw;
			}
		} // func GetDataInternalAsync

		private T SetValueIntern(T t, Exception e)
		{
			lock (getValue)
			{
				value = t;
				state = LazyPropertyState.Loaded;

				foreach (var c in valueListener)
				{
					if (e == null)
						c.SetResult(t);
					else
						c.SetException(e);
				}
				valueListener.Clear();
			}
			onPropertyChanged?.Invoke();

			return value;
		} // func SetValueIntern

		public Task<T> GetValueAsync()
		{
			lock(getValue)
			{
				switch(state)
				{
					case LazyPropertyState.Nothing:
						state = LazyPropertyState.Loading;
						return GetDataInternalAsync();
					case LazyPropertyState.Loading:
						var t = new TaskCompletionSource<T>();
						valueListener.Add(t);
						return t.Task;
					case LazyPropertyState.Loaded:
						return Task.FromResult(value);
					default:
						throw new InvalidOperationException();
				}
			}
		} // func GetValueAsync

		public T GetValue()
		{
			lock (getValue)
			{
				switch (state)
				{
					case LazyPropertyState.Nothing:
						getValue().ContinueWith(
							t =>
							{
								if (t.IsFaulted)
									SetValueIntern(default(T), t.Exception);
								else
									SetValueIntern(t.Result, null);
							}, TaskContinuationOptions.ExecuteSynchronously
						);
						return default(T);
					case LazyPropertyState.Loading:
						return default(T);
					case LazyPropertyState.Loaded:
						return value;
					default:
						throw new InvalidOperationException();
				}
			}
		} // func GetValue
	} // class LazyProperty
}
