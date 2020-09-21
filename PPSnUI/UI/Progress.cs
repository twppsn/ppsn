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

	#region -- class PpsUI ------------------------------------------------------------

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

		/// <summary>Return a dummy progress.</summary>
		/// <param name="sp"></param>
		/// <param name="blockUI"></param>
		/// <returns></returns>
		public static IPpsProgress CreateProgress(this IServiceProvider sp, bool blockUI = true, string progressText = null)
			=> SetProgressText(sp.GetService<IPpsProgressFactory>()?.CreateProgress(blockUI), progressText) ?? EmptyProgress;
	
		/// <summary>Empty progress bar implementation</summary>
		public static IPpsProgress EmptyProgress { get; } = new PpsDummyProgress();
	} // class PpsUI

	#endregion
}
