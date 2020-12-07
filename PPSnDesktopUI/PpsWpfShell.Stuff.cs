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
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Neo.IronLua;
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
			=> PpsShell.CreateProgress(GetControlService<IPpsProgressFactory>(sender, true), blockUI, progressText);

		/// <summary></summary>
		/// <param name="sender"></param>
		/// <param name="taskText"></param>
		/// <param name="action"></param>
		/// <returns></returns>
		public static Task RunTaskAsync(this DependencyObject sender, string taskText, Func<IPpsProgress, Task> action)
			=> PpsShell.RunTaskAsync(GetControlService<IServiceProvider>(sender, true), taskText, action);

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

		#region -- class WebRequestHelper ---------------------------------------------

		/// <summary>Parse a relative query for the arguments.</summary>
		/// <param name="uri"></param>
		/// <returns></returns>
		public static NameValueCollection ParseQuery(string uri)
		{
			var pos = uri.IndexOf('?');
			return pos == -1
				? new NameValueCollection()
				: HttpUtility.ParseQueryString(uri.Substring(pos + 1));
		} // func ParseQuery

		/// <summary>Parse a relative or absolute query for the arguments.</summary>
		/// <param name="uri"></param>
		/// <returns></returns>
		public static NameValueCollection ParseQuery(this Uri uri)
			=> uri.IsAbsoluteUri ? HttpUtility.ParseQueryString(uri.Query) : ParseQuery(uri.OriginalString);

		/// <summary>Get the query from an relative uri.</summary>
		/// <param name="uri"></param>
		/// <returns></returns>
		public static string ParsePath(string uri)
		{
			var pos = uri.IndexOf('?');
			return pos == -1 ? uri : uri.Substring(0, pos);
		} // func ParsePath

		/// <summary>Get the query from an relative or absolute uri.</summary>
		/// <param name="uri"></param>
		/// <returns></returns>
		public static string ParsePath(this Uri uri)
			=> uri.IsAbsoluteUri ? uri.AbsolutePath : ParsePath(uri.OriginalString);

		/// <summary>Get the query and path from an relative or absolute uri.</summary>
		/// <param name="uri"></param>
		/// <returns></returns>
		public static (string path, NameValueCollection arguments) ParseUri(this Uri uri)
			=> uri.IsAbsoluteUri ? (uri.AbsolutePath, HttpUtility.ParseQueryString(uri.Query)) : ParseUri(uri.OriginalString);

		/// <summary>Get the query and path from an relative uri.</summary>
		/// <param name="uri"></param>
		/// <returns></returns>
		public static (string path, NameValueCollection arguments) ParseUri(string uri)
		{
			var pos = uri.IndexOf('?');
			return pos == -1 ? (uri, new NameValueCollection()) : (uri.Substring(0, pos), HttpUtility.ParseQueryString(uri.Substring(pos + 1)));
		} // func ParseUri

		/// <summary>Compares two Uri</summary>
		/// <param name="uri1">first Uri</param>
		/// <param name="uri2">second Uri</param>
		/// <returns>return true if both Uris have the same result</returns>
		public static bool EqualUri(Uri uri1, Uri uri2)
		{
			if (uri1.IsAbsoluteUri && uri2.IsAbsoluteUri)
				return uri1.Equals(uri2);
			else if (uri1.IsAbsoluteUri || uri2.IsAbsoluteUri)
				return false;
			else
			{
				(var path1, var args1) = uri1.ParseUri();
				(var path2, var args2) = uri2.ParseUri();

				if (path1 == path2 && args1.Count == args2.Count)
				{
					foreach (var k in args1.AllKeys)
					{
						if (args1[k] != args2[k])
							return false;
					}
					return true;
				}
				else
					return false;
			}
		} // func EqualUri

		/// <summary>Convert arguments to an table</summary>
		/// <param name="uri"></param>
		/// <returns></returns>
		public static LuaTable GetArgumentsAsTable(this Uri uri)
		{
			// copy arguments
			var args = new LuaTable();
			var tmp = uri.ParseQuery();
			foreach (var k in tmp.Keys.Cast<string>())
			{
				if (k == "_")
				{
					var v = tmp[k];
					
					// unpack table
					LuaTable t;
					if (v.StartsWith("base64-"))
						t = LuaTable.FromLson(Encoding.UTF8.GetString(Convert.FromBase64String(v.Substring(7))));
					else
						t = LuaTable.FromLson(v);

					// copy table
					foreach (var kv in t)
						args[kv.Key] = kv.Value;
				}
				else
					args[k] = tmp[k]; // copy value
			}
			return args;
		} // func GetArgumentsAsTable

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
