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
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TecWare.DE.Stuff;
using TecWare.PPSn.UI;

namespace TecWare.PPSn
{
	public static partial class PpsWpfShell
	{
		#region -- ChangeTypeWithConverter --------------------------------------------

		/// <summary></summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="value"></param>
		/// <returns></returns>
		public static T ChangeTypeWithConverter<T>(this object value)
			=> (T)ChangeTypeWithConverter(value, typeof(T));

		/// <summary></summary>
		/// <param name="value"></param>
		/// <param name="typeTo"></param>
		/// <returns></returns>
		public static object ChangeTypeWithConverter(this object value, Type typeTo)
		{
			if (value == null)
				return Procs.ChangeType(null, typeTo);
			else if (typeTo.IsAssignableFrom(value.GetType()))
				return value;
			else
			{
				var convTo = TypeDescriptor.GetConverter(value.GetType());
				if (convTo.CanConvertTo(typeTo))
					return convTo.ConvertTo(null, CultureInfo.InvariantCulture, value, typeTo);
				else
				{
					var convFrom = TypeDescriptor.GetConverter(typeTo);
					if (convFrom.CanConvertFrom(value.GetType()))
						return convFrom.ConvertFrom(null, CultureInfo.InvariantCulture, value);
					else
						return Procs.ChangeType(value, typeTo);
				}
			}
		} // func ChangeTypeWithConverter

		#endregion

		#region -- Commands -----------------------------------------------------------

		/// <summary>Create a command binding with the ui element.</summary>
		/// <param name="ui"></param>
		/// <param name="shell"></param>
		/// <param name="target"></param>
		/// <param name="command"></param>
		public static void AddCommandBinding(this UIElement ui, IPpsShell shell, object target, PpsCommandBase command)
			=> ui.CommandBindings.Add(PpsCommandBase.CreateBinding(shell, target, command));

		/// <summary>Create a command binding with the ui element.</summary>
		/// <param name="ui"></param>
		/// <param name="shell"></param>
		/// <param name="command"></param>
		/// <param name="commandImpl"></param>
		public static void AddCommandBinding(this UIElement ui, IPpsShell shell, RoutedCommand command, PpsCommandBase commandImpl)
			=> ui.CommandBindings.Add(PpsCommandBase.CreateBinding(shell, command, commandImpl));

		/// <summary>Executes a command of a command source</summary>
		/// <param name="commandSource"></param>
		/// <param name="inputElement"></param>
		/// <returns></returns>
		public static bool Execute(this ICommandSource commandSource, IInputElement inputElement)
		{
			var command = commandSource.Command;
			if (command != null)
			{
				var commandParameter = commandSource.CommandParameter;
				var commandTarget = commandSource.CommandTarget ?? inputElement;

				if (command is RoutedCommand rc)
				{
					if (rc.CanExecute(commandParameter, commandTarget))
					{
						rc.Execute(commandParameter, commandTarget);
						return true;
					}
				}
				else
				{
					if (command.CanExecute(commandParameter))
					{
						command.Execute(commandParameter);
						return true;
					}
				}
			}
			return false;
		} // func Execute

		#endregion

		#region -- Progress Extensions ------------------------------------------------

		#region -- class PpsProgressStackWpf ------------------------------------------

		private sealed class PpsProgressStackWpf : PpsProgressStack
		{
			private readonly Dispatcher dispatcher;

			public PpsProgressStackWpf(Dispatcher dispatcher)
			{
				this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
			} // ctor

			protected override void InvokeUI(Delegate dlg, object arg)
				=> dispatcher.Invoke(dlg, DispatcherPriority.Normal, arg);
		} // class PpsProgressStackWpf

		#endregion

		/// <summary>Return a dummy progress.</summary>
		/// <param name="sender"></param>
		/// <param name="blockUI"></param>
		/// <param name="progressText"></param>
		/// <returns></returns>
		public static IPpsProgress CreateProgress(this DependencyObject sender, bool blockUI = true, string progressText = null)
			=> PpsUI.CreateProgress(GetControlService<IPpsProgressFactory>(sender, false), blockUI, progressText);

		/// <summary></summary>
		/// <param name="sender"></param>
		/// <param name="taskText"></param>
		/// <param name="action"></param>
		/// <returns></returns>
		public static Task RunTaskAsync(this DependencyObject sender, string taskText, Func<IPpsProgress, Task> action)
			=> PpsUI.RunTaskAsync(GetControlService<IServiceProvider>(sender, true), taskText, action);

		/// <summary>Create a progress stack.</summary>
		/// <param name="dispatcher"></param>
		/// <returns></returns>
		public static PpsProgressStack CreateProgressStack(this Dispatcher dispatcher)
			=> new PpsProgressStackWpf(dispatcher);

		#endregion

		#region -- IsAdministrator, IsWow64Process ------------------------------------

		/// <summary>Check administrator privilegs</summary>
		/// <returns></returns>
		public static bool IsAdministrator()
		{
			var identity = WindowsIdentity.GetCurrent();
			var principal = new WindowsPrincipal(identity);
			return principal.IsInRole(WindowsBuiltInRole.Administrator);
		} // func IsAdministrator

		/// <summary>Is this a wow process</summary>
		public static bool IsWow64Process
		{
			get
			{
				try
				{
					NativeMethods.IsWow64Process(Process.GetCurrentProcess().Handle, out var ret);
					return ret;
				}
				catch (Exception)
				{
					return false;
				}
			}
		} // prop IsWow64Process

		#endregion

		#region -- HsvColor - converter -----------------------------------------------

		/// <summary><see cref="Color"/> to <see cref="HsvColor"/></summary>
		/// <param name="color"></param>
		/// <returns></returns>
		public static HsvColor ToHsvColor(this Color color)
			=> HsvColor.FromArgb(color.A, color.R, color.G, color.B);

		/// <summary><see cref="HsvColor"/> to <see cref="Color"/></summary>
		/// <param name="color"></param>
		/// <returns></returns>
		public static Color ToMediaColor(this HsvColor color)
		{
			color.ToRgb(out var r, out var g, out var b);
			return Color.FromArgb(color.Alpha, r, g, b);
		} // func ToMediaColor

		#endregion
	} // class PpsWpfShell
}
