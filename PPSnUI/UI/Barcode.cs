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
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading;
using System.Threading.Tasks;
using TecWare.DE.Stuff;
using TecWare.PPSn.Core.UI;

namespace TecWare.PPSn.UI
{
	#region -- interface IPpsBarcodeResult --------------------------------------------

	/// <summary>Result of an barcode dialog</summary>
	public interface IPpsBarcodeResult
	{
		/// <summary>Emit barcode via dispatch</summary>
		void Dispatch();

		/// <summary>Barcode</summary>
		string Text { get; }
		/// <summary>Barcode format.</summary>
		string Format { get; }
	} // interface IPpsBarcodeResult

	#endregion

	#region -- interface IPpsBarcodeProvider ------------------------------------------

	/// <summary>Barcode provider.</summary>
	public interface IPpsBarcodeProvider
	{
		/// <summary>Description of the provider.</summary>
		string Description { get; }
		/// <summary>Type of the provider. (e.g. keyboard)</summary>
		string Type { get; }
	} // interface IPpsBarcodeProvider

	#endregion

	#region -- interface IPpsBarcodeDialogProvider ------------------------------------

	/// <summary>Provider that is invoked with an user dialog.</summary>
	public interface IBarcodeDialogProvider : IPpsBarcodeProvider
	{
		/// <summary>Start user dialog.</summary>
		/// <returns></returns>
		Task<IPpsBarcodeResult> GetBarcodeAsync();
	} // interface IPpsBarcodeDialogProvider

	#endregion

	#region -- interface IPpsBarcodeReceiver ------------------------------------------

	/// <summary>Implemented event by an barcode receiver.</summary>
	public interface IPpsBarcodeReceiver
	{
		/// <summary>Callback in UI-Thread with the scanned barcode.</summary>
		/// <param name="code"></param>
		/// <returns><c>true</c>, if barcode is processed.</returns>
		Task<bool> OnBarcodeAsync(PpsBarcodeInfo code);

		/// <summary>Is the barcode receiver active.</summary>
		bool IsActive { get; }
	} // interface IPpsBarcodeReceiver

	#endregion

	#region -- class PpsBarcodeInfo ---------------------------------------------------

	/// <summary>Event parameter during read</summary>
	public abstract class PpsBarcodeInfo
	{
		#region -- class PpsTextBarcodeInfo -------------------------------------------

		private sealed class PpsTextBarcodeInfo : PpsBarcodeInfo
		{
			private readonly string rawCode;
			private readonly string format;
			private readonly Lazy<PpsBarcode> parseCode;

			public PpsTextBarcodeInfo(PpsBarcodeService service, IPpsBarcodeProvider provider, string rawCode, string format)
				: base(provider)
			{
				this.rawCode = rawCode ?? throw new ArgumentNullException(nameof(rawCode));
				this.format = format;

				parseCode = new Lazy<PpsBarcode>(() => service.ParseCode(rawCode));
			} // ctor

			public override string RawCode => rawCode;
			public override PpsBarcode Code => parseCode.Value;
			public override string Format => format;
		} // class PpsTextBarcodeInfo

		#endregion

		#region -- class PpsCodeBarcodeInfo -------------------------------------------

		private sealed class PpsCodeBarcodeInfo : PpsBarcodeInfo
		{
			private readonly PpsBarcode barcode;
			private readonly Lazy<string> encodeCode;

			public PpsCodeBarcodeInfo(IPpsBarcodeProvider provider, PpsBarcode barcode)
				: base(provider)
			{
				this.barcode = barcode ?? throw new ArgumentNullException(nameof(barcode));

				encodeCode = new Lazy<string>(() => barcode.Code);
			} // ctor

			public override string RawCode => encodeCode.Value;
			public override PpsBarcode Code => barcode;
			public override string Format => null;
		} // class PpsCodeBarcodeInfo

		#endregion

		private readonly IPpsBarcodeProvider provider;

		private PpsBarcodeInfo(IPpsBarcodeProvider provider)
		{
			this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
		} // ctor

		/// <summary>Return parse code.</summary>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		public T Parse<T>()
			where T : PpsBarcode
			=> Code as T;

		/// <summary>Provider of this code.</summary>
		public IPpsBarcodeProvider Provider => provider;
		/// <summary>Raw data of the code.</summary>
		public abstract string RawCode { get; }
		/// <summary>Optional barcode format, the format of 'picture'.</summary>
		public abstract string Format { get; }
		/// <summary>Parsed barcode.</summary>
		public abstract PpsBarcode Code { get; }

		internal static PpsBarcodeInfo Create(PpsBarcodeService service, IPpsBarcodeProvider provider, string text, string format)
			=> new PpsTextBarcodeInfo(service, provider, text, format);

		internal static PpsBarcodeInfo Create(IPpsBarcodeProvider provider, PpsBarcode barcode)
			=> new PpsCodeBarcodeInfo(provider, barcode);
	} // class PpsBarcodeInfo

	#endregion

	#region -- class PpsBarcodeService ------------------------------------------------

	/// <summary>Implements barcode receiver.</summary>
	[PpsService(typeof(PpsBarcodeService)), PpsLazyService]
	public sealed class PpsBarcodeService : IEnumerable<IPpsBarcodeProvider>, INotifyCollectionChanged
	{
		#region -- class BarcodeTokenBase ---------------------------------------------

		private abstract class BarcodeTokenBase<T> : IDisposable
			where T : class
		{
			protected readonly WeakReference<T> reference;
			private int refCount = 0;

			public BarcodeTokenBase(T receiver)
			{
				reference = new WeakReference<T>(receiver);
				AddRef();
			} // ctor

			public void AddRef()
				=> refCount++;

			public void Dispose()
				=> refCount--;

			protected bool TryGetTargetCore(out T target)
			{
				if (reference.TryGetTarget(out target))
					return true;
				else
				{
					target = null;
					refCount = 0;
					return false;
				}
			} // func TryGetTargetCore

			public bool TryGetTarget(out T target)
			{
				if (IsAlive && TryGetTargetCore(out target))
					return true;
				else
				{
					target = null;
					return false;
				}
			} // func TryGetTarget

			public bool IsAlive => refCount > 0;
		} // class BarcodeTokenBase

		#endregion

		#region -- class BarcodeToken -------------------------------------------------

		private sealed class BarcodeToken<T> : BarcodeTokenBase<T>, IEquatable<T>
			where T : class
		{
			public BarcodeToken(T receiver)
				: base(receiver)
			{
			} // ctor

			public bool Equals(T other)
				=> TryGetTarget(out var r) && ReferenceEquals(r, other);
		} // class BarcodeToken

		#endregion

		#region -- class BarcodeTokenPriority -----------------------------------------

		private sealed class BarcodeTokenPriority<T> : BarcodeTokenBase<T>, IComparable<BarcodeTokenPriority<T>>
			where T : class
		{
			private readonly int priority;

			public BarcodeTokenPriority(int priority, T receiver)
				: base(receiver)
			{
				this.priority = priority;
			} // ctor

			public int CompareTo(BarcodeTokenPriority<T> other)
			{
				if (priority == other.priority)
				{
					var exists = TryGetTargetCore(out var target);
					var otherExists = TryGetTargetCore(out var otherTarget);
					if (exists && otherExists)
					{
						if (Equals(target, otherTarget))
							return 0;
						else
							return target.GetHashCode() - other.GetHashCode();
					}
					else if (exists && !otherExists)
						return -1;
					else if (!exists && otherExists)
						return 1;
					else
						return 0;
				}
				else
					return priority - other.priority;
			} // func Equals

			public int Priority => priority;
		} // class BarcodeToken

		#endregion

		#region -- class BarcodeFactory -----------------------------------------------

		private sealed class BarcodeFactory : IComparable<BarcodeFactory>
		{
			private readonly int priority;
			private readonly Func<string, PpsBarcode> decode;
			
			public BarcodeFactory(int priority, Func<string, PpsBarcode> decode)
			{
				this.priority = priority;
				this.decode = decode ?? throw new ArgumentNullException(nameof(decode));
			} // ctor

			public int CompareTo(BarcodeFactory other)
			{
				if (priority == other.priority)
				{
					if (decode == other.decode)
						return 0;
					else
						return decode.GetHashCode() - other.decode.GetHashCode();
				}
				else
					return priority - other.priority;
			} // func CompareTo

			public bool TryParse(string text, out PpsBarcode code)
			{
				code = decode(text);
				return code != null && code.IsCodeValid;
			} // func TryParse

			public int Priority => priority;
		} // class BarcodeFactory

		#endregion

		#region -- class ProgramProvider ----------------------------------------------

		private sealed class ProgramProvider : IPpsBarcodeProvider
		{
			public string Description => "Program";
			public string Type => "ui";
		} // class ProgramProvider

		#endregion

		/// <summary>Notify device changes</summary>
		public event NotifyCollectionChangedEventHandler CollectionChanged;

		private readonly List<BarcodeToken<IPpsBarcodeProvider>> providers = new List<BarcodeToken<IPpsBarcodeProvider>>();
		private readonly List<BarcodeToken<IPpsBarcodeReceiver>> receivers = new List<BarcodeToken<IPpsBarcodeReceiver>>();
		private readonly List<BarcodeTokenPriority<IPpsBarcodeReceiver>> defaultReceivers = new List<BarcodeTokenPriority<IPpsBarcodeReceiver>>();
		private readonly List<BarcodeFactory> decoders = new List<BarcodeFactory>();
		private readonly AsyncQueue barcodeProcessQueue = new AsyncQueue();

		private readonly SynchronizationContext synchronizationContext;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		public PpsBarcodeService()
		{
			synchronizationContext = SynchronizationContext.Current ?? throw new ArgumentNullException(nameof(SynchronizationContext));

			RegisterDecoder(2000, Core.UI.Barcodes.GS1.TryParse);
		} // ctor

		/// <summary>Fire collection reset.</summary>
		private void FireProvidersChanged()
			=> CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

		#endregion

		#region -- RegisterReceiver, RegisterProvider, RegisterDecoder ----------------

		private static IDisposable Register<T>(List<BarcodeToken<T>> tokens, T item)
			where T : class
		{
			lock (tokens)
			{
				BarcodeToken<T> token;
				var idx = tokens.FindIndex(c => c.Equals(item));
				if (idx == -1)
					tokens.Add(token = new BarcodeToken<T>(item));
				else
				{
					// move to top
					token = tokens[idx];
					token.AddRef();
					tokens.RemoveAt(idx);
					tokens.Add(token);
				}
				return token;
			}
		} // func Register

		/// <summary>Sets the new active receiver for barcodes.</summary>
		/// <param name="receiver"></param>
		/// <returns></returns>
		public IDisposable RegisterReceiver(IPpsBarcodeReceiver receiver)
			=> Register(receivers, receiver);

		/// <summary>Sets the new active receiver for barcodes.</summary>
		/// <param name="priority"></param>
		/// <param name="receiver"></param>
		/// <returns></returns>
		public IDisposable RegisterDefaultReceiver(int priority, IPpsBarcodeReceiver receiver)
		{
			lock (defaultReceivers)
			{
				var token = new BarcodeTokenPriority<IPpsBarcodeReceiver>(priority, receiver);
				var idx = defaultReceivers.BinarySearch(token);
				if (idx < 0)
					defaultReceivers.Insert(~idx, token);
				else
					throw new ArgumentException("Already registered.", nameof(receiver));
				return token;
			}
		} // func RegisterDefaultReceiver

		/// <summary>Register a new barcode provider.</summary>
		/// <param name="provider"></param>
		/// <returns></returns>
		public IDisposable RegisterProvider(IPpsBarcodeProvider provider)
			=> Register(providers, provider);

		/// <summary>Register a new barcode decoder.</summary>
		/// <param name="decoder"></param>
		/// <param name="priority"></param>
		/// <returns></returns>
		public bool RegisterDecoder(int priority, Func<string, PpsBarcode> decoder)
		{
			lock (decoders)
			{
				var f = new BarcodeFactory(priority, decoder);
				var idx = decoders.BinarySearch(f);
				if (idx < 0)
				{
					decoders.Insert(~idx, f);
					return true;
				}
				else
					return false;
			}
		} // func RegisterDecoder

		#endregion

		#region -- DispatchBarcode ----------------------------------------------------

		private async Task DisposeBarcodeUIAsync(PpsBarcodeInfo info)
		{
			IPpsBarcodeReceiver receiver = null;

			lock (receivers)
			{
				for (var i = receivers.Count - 1; i >= 0; i--)
				{
					if (receivers[i].TryGetTarget(out var r))
					{
						if (r != null && r.IsActive && receiver == null)
							receiver = r;
					}
					else
						receivers.RemoveAt(i);
				}
			}

			// debug message
			if (receiver == null || !await receiver.OnBarcodeAsync(info))
				await OnDefaultBarcodeAsync(info);
		} // proc DisposeBarcodeUIAsync

		/// <summary>Dispatch a barcode to an receiver.</summary>
		/// <param name="provider">Barcode source.</param>
		/// <param name="text">Barcode text</param>
		/// <param name="format">Optional barcode format.</param>
		public void DispatchBarcode(IPpsBarcodeProvider provider, string text, string format = null)
		{
			var info = PpsBarcodeInfo.Create(this, provider, text, format);
			synchronizationContext.Post(state => barcodeProcessQueue.Enqueue(() => DisposeBarcodeUIAsync(info)), null);
		} // func DispatchBarcode

		/// <summary>Dispatch a barcode within the ui-thread.</summary>
		/// <param name="barcode"></param>
		/// <returns></returns>
		public Task DispatchBarcodeAsync(PpsBarcode barcode)
			=> DisposeBarcodeUIAsync(PpsBarcodeInfo.Create(Program, barcode));

		/// <summary>Run default barcode receivers.</summary>
		/// <param name="info"></param>
		/// <returns></returns>
		public async Task<bool> OnDefaultBarcodeAsync(PpsBarcodeInfo info)
		{
			var activeReseivers = new List<IPpsBarcodeReceiver>();
			lock (defaultReceivers)
			{
				var i = 0;
				while (i < defaultReceivers.Count)
				{
					if (defaultReceivers[i].TryGetTarget(out var r))
					{
						activeReseivers.Add(r);
						i++;
					}
					else
						defaultReceivers.RemoveAt(i);
				}
			}

			foreach (var r in activeReseivers)
			{
				if (await r.OnBarcodeAsync(info))
					return true;
			}

			return false;
		} // proc OnDefaultBarcodeAsync

		#endregion

		#region -- ParseCode ----------------------------------------------------------

		internal PpsBarcode ParseCode(string rawCode)
		{
			if (String.IsNullOrEmpty(rawCode))
				throw new ArgumentNullException(nameof(rawCode));

			lock (decoders)
			{
				for (var i = 0; i < decoders.Count; i++)
				{
					if (decoders[i].TryParse(rawCode, out var code))
						return code;
				}
			}
			return new Core.UI.Barcodes.GenericCode(rawCode);
		} // func ParseCode

		#endregion

		/// <summary>Enumerate all barcode provider.</summary>
		/// <returns></returns>
		public IEnumerator<IPpsBarcodeProvider> GetEnumerator()
		{
			lock (providers)
			{
				for (var i = providers.Count - 1; i >= 0; i--)
				{
					if (providers[i].TryGetTarget(out var p))
						yield return p;
					else
						providers.RemoveAt(i);
				}
			}
		} // func GetEnumerator

		IEnumerator IEnumerable.GetEnumerator()
			=> GetEnumerator();

		/// <summary>Is a receiver active.</summary>
		public bool HasReceivers => receivers.Count > 0;

		/// <summary>Program provider for dispatch of barcodes.</summary>
		public static IPpsBarcodeProvider Program { get; } = new ProgramProvider();
	} // interface PpsBarcodeService

	#endregion
}
