#region -- copyright --
//
// Licensed under the EUPL, Version 1.1 or - as soon they will be approved by the
// European Commission - subsequent versions of the EUPL(the "Licence"); You may
// not use this work except in compliance with the Licence.
//
// You may obtain a copy of the Licence at:
// http://ec.europa.eu/idabc/eupl
//
// Unless required by applicable law or agreed to in writing, software distributed
// under the Licence is distributed on an "AS IS" basis, WITHOUT WARRANTIES OR
// CONDITIONS OF ANY KIND, either express or implied. See the Licence for the
// specific language governing permissions and limitations under the Licence.
//
#endregion
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TecWare.PPSn.Stuff
{
	#region -- class LazyProperty -----------------------------------------------------

	/// <summary>Property implementation for Lazy via Binding or Task..</summary>
	/// <typeparam name="T"></typeparam>
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

		/// <summary></summary>
		/// <param name="getValue"></param>
		/// <param name="onPropertyChanged"></param>
		public LazyProperty(Func<Task<T>> getValue, Action onPropertyChanged)
		{
			this.getValue = getValue;
			this.onPropertyChanged = onPropertyChanged;

			Reset();
		} // ctor

		/// <summary>Clear value of the property.</summary>
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

		/// <summary>Get the value in async mode.</summary>
		/// <returns></returns>
		public Task<T> GetValueAsync()
		{
			lock (getValue)
			{
				switch (state)
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

		/// <summary>Get current value, or start loading of the value.</summary>
		/// <returns></returns>
		public T GetValue()
		{
			lock (getValue)
			{
				switch (state)
				{
					case LazyPropertyState.Nothing:
						state = LazyPropertyState.Loading;
						getValue().ContinueWith(
							t =>
							{
								if (t.IsFaulted)
									SetValueIntern(default(T), t.Exception);
								else
									SetValueIntern(t.Result, null);
							}, TaskContinuationOptions.ExecuteSynchronously
						);
						return state == LazyPropertyState.Loaded ? value : default(T);
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

	#endregion
}
