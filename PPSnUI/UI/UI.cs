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
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using TecWare.DE.Stuff;

namespace TecWare.PPSn.UI
{
	#region -- enum ExceptionShowFlags ------------------------------------------------

	/// <summary>Propose of the exception.</summary>
	[Flags]
	public enum PpsExceptionShowFlags
	{
		/// <summary>No hint</summary>
		None = 0,
		/// <summary>Start the shutdown of the application.</summary>
		Shutown = 1,
		/// <summary>Do not show any user message.</summary>
		Background = 4,
		/// <summary>Classify the exception as warning</summary>
		Warning = 8
	} // enum ExceptionShowFlags

	#endregion

	#region -- enum PpsImage ----------------------------------------------------------

	/// <summary>Image collection.</summary>
	public enum PpsImage
	{
		/// <summary>No image.</summary>
		None,
		/// <summary>Information</summary>
		Information,
		/// <summary>Warning</summary>
		Warning,
		/// <summary>Error</summary>
		Error,
		/// <summary>Question</summary>
		Question
	} // enum PpsImage

	#endregion

	#region -- interface IPpsUIService ------------------------------------------------

	/// <summary>UI helper</summary>
	public interface IPpsUIService
	{
		/// <summary></summary>
		/// <param name="flags"></param>
		/// <param name="exception"></param>
		/// <param name="alternativeMessage"></param>
		void ShowException(PpsExceptionShowFlags flags, Exception exception, string alternativeMessage = null);

		/// <summary>Display a simple messagebox</summary>
		/// <param name="text"></param>
		/// <param name="button"></param>
		/// <param name="image"></param>
		/// <param name="defaultResult"></param>
		/// <returns></returns>
		int MsgBox(string text, PpsImage image = PpsImage.Information, params string[] buttons);

		/// <summary>Run this action in ui-thread.</summary>
		/// <param name="action"></param>
		Task RunUI(Action action);

		/// <summary>Run this action in ui-thread.</summary>
		/// <param name="action"></param>
		Task<T> RunUI<T>(Func<T> action);

		/// <summary>Predefined buttons</summary>
		string[] Ok { get; }
		/// <summary>Predefined buttons</summary>
		string[] YesNo { get; }
		/// <summary>Predfined buttons</summary>
		string[] OkCancel { get; }
	} // interface IPpsUIService

	#endregion

	#region -- interface PpsUI --------------------------------------------------------

	public static partial class PpsUI
	{
		public static void ShowException(this IPpsUIService ui, Exception exception, string alternativeMessage = null)
			=> ui.ShowException(PpsExceptionShowFlags.None, exception, alternativeMessage);

		public static void ShowException(this IServiceProvider sp, Exception exception, string alternativeMessage = null)
			=> ShowException(sp.GetService<IPpsUIService>(true), exception, alternativeMessage);

		public static Task ShowExceptionAsync(this IPpsUIService ui, bool background, Exception exception, string alternativeMessage = null)
			=> ui.RunUI(() => ui.ShowException(background ? PpsExceptionShowFlags.Background : PpsExceptionShowFlags.None, exception, alternativeMessage));

		public static Task ShowExceptionAsync(this IServiceProvider sp, bool background, Exception exception, string alternativeMessage = null)
			=> ShowExceptionAsync(sp.GetService<IPpsUIService>(true), background, exception, alternativeMessage);

		public static async Task RunTaskAsync(this IServiceProvider sp, string taskText, Func<IPpsProgress,Task> action)
		{
			using (var p = sp.CreateProgress(true, taskText))
				await action(p);
		} // proc RunTaskAsync
	} // class PpsUI

	#endregion
}
