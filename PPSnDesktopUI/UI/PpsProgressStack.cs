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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using TecWare.DE.Data;

namespace TecWare.PPSn.UI
{
	#region -- interface IPpsProgress ---------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public interface IPpsProgress : IDisposable
	{
		/// <summary>Statustext</summary>
		string Text { get; set; }
		/// <summary>Progressbar position</summary>
		int Value { get; set; }
	} // interface IPpsProgress

	#endregion

	#region -- class PpsProgressStack ---------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsProgressStack : ObservableObject
	{
		#region -- class PpsDummyProgress -------------------------------------------------

		private sealed class PpsDummyProgress : IPpsProgress
		{
			public PpsDummyProgress()
			{
			} // ctor

			public void Dispose()
			{
			} // proc Dispose

			public int Value { get; set; }
			public string Text { get; set; }

		} // class PpsDummyProgress

		#endregion

		#region -- class PpsWindowPaneControlProgressStub ---------------------------------

		private sealed class PpsProgressStub : IPpsProgress
		{
			private readonly PpsProgressStack stack;

			private int currentValue = -1;
			private string currentText = String.Empty;

			public PpsProgressStub(PpsProgressStack stack)
			{
				this.stack = stack;

				stack.RegisterProgress(this);
			} // ctor

			public void Dispose()
			{
				stack.UnregisterProgress(this);
			} // proc Dispose

			public string Text
			{
				get { return currentText; }
				set
				{
					if (currentText != value)
					{
						currentText = value;
						stack.NotifyProgressText(this);
					}
				}
			} // prop Text

			public int Value
			{
				get { return currentValue; }
				set
				{
					if (currentValue != value)
					{
						currentValue = value;
						stack.NotifyProgressValue(this);
					}
				}
			} // prop Progress
		} // class PpsProgressStub

		#endregion

		private readonly Dispatcher dispatcher;
		private readonly List<IPpsProgress> progressStack;

		public PpsProgressStack(Dispatcher dispatcher)
		{
			this.dispatcher = dispatcher;
			this.progressStack = new List<IPpsProgress>();
		} // ctor

		public IPpsProgress CreateProgress()
			=> new PpsProgressStub(this);

		private void RegisterProgress(PpsProgressStub stub)
		{
			Dispatcher.Invoke(() =>
			{
				var oldCount = -1;
				lock (progressStack)
				{
					oldCount = progressStack.Count;
					var index = progressStack.IndexOf(stub);
					if (index >= 0)
						progressStack.RemoveAt(index);
					progressStack.Add(stub);
				}

				if (oldCount == 0)
					OnPropertyChanged(nameof(IsEnabled));

				OnPropertyChanged(nameof(DisableText));
				OnPropertyChanged(nameof(DisableProgress));
			});
		} // proc RegisterProgress

		private void UnregisterProgress(PpsProgressStub stub)
		{
			Dispatcher.Invoke(() =>
			{
				var updateUI = false;
				var updateEnabled = false;
				lock (progressStack)
				{
					if (progressStack.Remove(stub))
					{
						updateUI = true;
						updateEnabled = progressStack.Count == 0;
					}
				}

				if (updateEnabled)
					OnPropertyChanged(nameof(IsEnabled));
				if (updateUI)
				{
					OnPropertyChanged(nameof(DisableText));
					OnPropertyChanged(nameof(DisableProgress));
				}
			});
		} // proc UnregisterProgress

		private void NotifyProgressText(PpsProgressStub stub)
		{
			if (Top == stub)
				Dispatcher.Invoke(() => OnPropertyChanged(nameof(DisableText)));
		} // proc NotifyProgressText

		private void NotifyProgressValue(PpsProgressStub stub)
		{
			if (Top == stub)
				Dispatcher.Invoke(() => OnPropertyChanged(nameof(DisableProgress)));
		} // proc NotifyProgressValue

		private IPpsProgress Top
		{
			get
			{
				lock (progressStack)
					return progressStack.Count > 0 ? progressStack[progressStack.Count - 1] : null;
			}
		} // prop Top

		/// <summary>Is the ui useable.</summary>
		public bool IsEnabled => progressStack.Count > 0;

		/// <summary>Text that should present the user.</summary>
		/// <remarks>This member should not return any exception. Dispatching to the UI is done by caller.</remarks>
		public string DisableText => Top?.Text ?? String.Empty;
		/// <summary><c>-1</c>, shows a marquee, or use a value between 0 and 1000 for percentage</summary>
		/// <remarks>This member should not return any exception. Dispatching to the UI is done by caller.</remarks>
		int DisableProgress => Top?.Value ?? -1;
		/// <summary></summary>
		ICommand DisableCancel => null;

		/// <summary>Synchronization context</summary>
		Dispatcher Dispatcher => dispatcher;

		public static IPpsProgress Dummy { get; } = new PpsDummyProgress();
	} // class PpsProgressStack

	#endregion
}
