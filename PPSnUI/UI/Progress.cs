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
using System.Threading;
using TecWare.DE.Data;
using TecWare.DE.Stuff;

namespace TecWare.PPSn.UI
{
	#region -- interface IPpsProgress -------------------------------------------------

	/// <summary>Return a progress control.</summary>
	public interface IPpsProgress : IProgress<string>, IDisposable
	{
		/// <summary>Statustext</summary>
		string Text { get; set; }
		/// <summary>Progressbar position (0...1000)</summary>
		int Value { get; set; }
	} // interface IPpsProgress

	#endregion

	#region -- interface IPpsProgressFactory ------------------------------------------

	/// <summary>Progress bar contract.</summary>
	public interface IPpsProgressFactory
	{
		/// <summary>Create a new progress.</summary>
		/// <param name="blockUI">Should the whole ui blocked.</param>
		/// <returns>Progress bar or a dummy implementation.</returns>
		IPpsProgress CreateProgress(bool blockUI = true);
	} // interface IPpsProgressFactory

	#endregion

	#region -- class PpsProgressStack -------------------------------------------------

	/// <summary>Progress stack implementation.</summary>
	public abstract class PpsProgressStack : ObservableObject, IPpsProgressFactory
	{
		#region -- class PpsWindowPaneControlProgressStub -----------------------------

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
				=> stack.UnregisterProgress(this);

			void IProgress<string>.Report(string value)
				=> Text = value;

			public string Text
			{
				get => currentText;
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
				get => currentValue;
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

		private readonly List<IPpsProgress> progressStack;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary>Create a progress stack.</summary>
		protected PpsProgressStack()
		{
			progressStack = new List<IPpsProgress>();
		} // ctor

		/// <summary>Override to synchronize to ui</summary>
		/// <param name="dlg"></param>
		/// <param name="arg"></param>
		protected abstract void InvokeUI(Delegate dlg, object arg);

		#endregion

		#region -- Progress Control ---------------------------------------------------

		/// <summary>Create a new progress item.</summary>
		/// <param name="blockUI">Should the ui be blocked.</param>
		/// <returns></returns>
		public IPpsProgress CreateProgress(bool blockUI)
			=> new PpsProgressStub(this);

		/// <summary>Register progress bar, is call from PpsProgressStub:ctor.</summary>
		/// <param name="stub"></param>
		private void RegisterProgress(PpsProgressStub stub)
			=> InvokeUI(new Action<PpsProgressStub>(RegisterProgressUI), stub);

		/// <summary>Unregister progress bar, is called from PpsProgressStub:dtor</summary>
		/// <param name="stub"></param>
		private void UnregisterProgress(PpsProgressStub stub)
			=> InvokeUI(new Action<PpsProgressStub>(UnregisterProgressUI), stub);

		private void RegisterProgressUI(PpsProgressStub stub)
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
				OnPropertyChanged(nameof(IsActive));

			OnPropertyChanged(nameof(CurrentText));
			OnPropertyChanged(nameof(CurrentProgress));
		} // proc RegisterProgressUI

		private void UnregisterProgressUI(PpsProgressStub stub)
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
				OnPropertyChanged(nameof(IsActive));
			if (updateUI)
			{
				OnPropertyChanged(nameof(CurrentText));
				OnPropertyChanged(nameof(CurrentProgress));
			}
		} // proc UnregisterProgress

		private void NotifyPropertyChanged(PpsProgressStub stub, string propertyName)
		{
			if (Top == stub)
				InvokeUI(new Action<string>(OnPropertyChanged), propertyName);
		} // proc NotifyProgressText

		private void NotifyProgressText(PpsProgressStub stub)
			=> NotifyPropertyChanged(stub, nameof(CurrentText));

		private void NotifyProgressValue(PpsProgressStub stub)
			=> NotifyPropertyChanged(stub, nameof(CurrentProgress));

		private IPpsProgress Top
		{
			get
			{
				lock (progressStack)
					return progressStack.Count > 0 ? progressStack[progressStack.Count - 1] : null;
			}
		} // prop Top

		#endregion

		/// <summary>Is the ui useable.</summary>
		public bool IsActive => progressStack.Count > 0;

		/// <summary>Text that should present the user.</summary>
		/// <remarks>This member should not return any exception. Dispatching to the UI is done by caller.</remarks>
		public string CurrentText => Top?.Text ?? null;
		/// <summary><c>-1</c>, shows a marquee, or use a value between 0 and 1000 for percentage</summary>
		/// <remarks>This member should not return any exception. Dispatching to the UI is done by caller.</remarks>
		public int CurrentProgress => Top?.Value ?? -1;
	} // class PpsProgressStack

	#endregion

	#region -- class PpsUI ------------------------------------------------------------

	/// <summary>Helper for PpsUI interfaces.</summary>
	public static partial class PpsUI
	{
		#region -- class PpsDummyProgress ---------------------------------------------

		private sealed class PpsDummyProgress : IPpsProgress
		{
			public PpsDummyProgress()
			{
			} // ctor

			public void Dispose()
			{
			} // proc Dispose

			public void Report(string text) { }

			public int Value { get; set; }
			public string Text { get; set; }
		} // class PpsDummyProgress

		#endregion

		private static IPpsProgress SetProgressText(IPpsProgress p, string progressText)
		{
			if (p != null)
				p.Text = progressText;
			return p;
		} // func SetProgressText

		/// <summary>Create a progress.</summary>
		/// <param name="progressFactory"></param>
		/// <param name="blockUI"></param>
		/// <param name="progressText"></param>
		/// <returns></returns>
		public static IPpsProgress CreateProgress(this IPpsProgressFactory progressFactory, bool blockUI = true, string progressText = null)
			=> SetProgressText(progressFactory?.CreateProgress(blockUI) ?? EmptyProgress, progressText);

		/// <summary>Create a progress.</summary>
		/// <param name="sp"></param>
		/// <param name="blockUI"></param>
		/// <param name="progressText"></param>
		/// <returns></returns>
		public static IPpsProgress CreateProgress(this IServiceProvider sp, bool blockUI = true, string progressText = null)
			=> CreateProgress(sp.GetService<IPpsProgressFactory>(), blockUI, progressText);
	
		/// <summary>Empty progress bar implementation</summary>
		public static IPpsProgress EmptyProgress { get; } = new PpsDummyProgress();
	} // class PpsUI

	#endregion
}
