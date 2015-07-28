using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using System.Xml.Linq;
using Neo.IronLua;
using System.Windows.Controls;
using System.Diagnostics;
using System.Windows.Input;

namespace TecWare.PPSn.UI
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Inhalt, welche aus einem Xaml und einem Lua-Script besteht.</summary>
	public class PpsGenericWpfWindowPane : LuaTable, IPpsWindowPane
	{
		#region -- class LuaCommandImplementation -----------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary>Wrapper for translate to Lua functions to a command object.</summary>
		private class LuaCommandImplementation : ICommand, IPpsIdleAction
		{
			private Action<object> command;
			private Func<object, bool> canExecute;

			public LuaCommandImplementation(Action<object> command, Func<object, bool> canExecute)
			{
				this.command = command;
				this.canExecute = canExecute;
			} // ctor

			#region -- ICommand Member ---------------------------------------------------------

			public event EventHandler CanExecuteChanged;

			public bool CanExecute(object parameter)
			{
				return canExecute == null || canExecute(parameter);
			} // func CanExecute

			public void Execute(object parameter)
			{
				command(parameter);
			} // proc Execute

			#endregion

			public void Refresh()
			{
				if (CanExecuteChanged != null)
					CanExecuteChanged(this, EventArgs.Empty);
			} // proc Refresh

			void IPpsIdleAction.OnIdle() { Refresh(); }
		} // class LuaCommandImplementation


		#endregion

		private static readonly XName xnCode = XName.Get("Code", "http://schemas.microsoft.com/winfx/2006/xaml");
		private string sXamlFile;
		private FrameworkElement control;

		private PpsEnvironment environment;
		private LuaTable arguments; // arguments

		#region -- Ctor/Dtor --------------------------------------------------------------

		public PpsGenericWpfWindowPane(PpsEnvironment environment)
		{
			this.environment = environment;
		} // ctor

		~PpsGenericWpfWindowPane()
		{
			Dispose(false);
		} // ctor

		public void Dispose()
		{
			GC.SuppressFinalize(this);
			Dispose(true);
		} // proc Dispose

		protected virtual void Dispose(bool lDisposing)
		{
		} // proc Dispose

		#endregion

		#region -- Load/Unload ------------------------------------------------------------

		public virtual async Task LoadAsync(LuaTable arguments)
		{
			await Task.Yield();

			// use the argument as base table
			this.arguments = arguments;

			// get the basic template
			sXamlFile = arguments["template"].ToString();
			if (String.IsNullOrEmpty(sXamlFile))
				throw new ArgumentException("template is missing."); // todo: exception

			var xaml = XDocument.Load(sXamlFile, LoadOptions.SetBaseUri | LoadOptions.SetLineInfo);

			// Load the content of the code-tag, to initialize extend functionality
			var xCode = xaml.Root.Element(xnCode);
			if (xCode != null)
			{
				var chunk = Lua.CompileChunk(xCode.Value, Path.GetFileName(sXamlFile), null);

				// Lua code runs in the UI-Thread
				Dispatcher.Invoke(() => chunk.Run(this));

				xCode.Remove();
			}

			// Create the Wpf-Control
			var xamlReader = new XamlReader();
			await Dispatcher.InvokeAsync(() =>
				{
					control = xamlReader.LoadAsync(xaml.CreateReader()) as FrameworkElement;
					OnControlCreated();
					control.DataContext = this;

					// notify changes on control
					OnPropertyChanged("Control");
				});
		} // proc LoadAsync

		protected virtual void OnControlCreated()
		{
		} // proc OnControlCreated

		public Task<bool> UnloadAsync()
		{
			return Task.FromResult<bool>(true);
		} // func UnloadAsync

		#endregion

		#region -- Lua-Interface ----------------------------------------------------------

		[LuaMember("print")]
		private void LuaPrint(string text)
		{
			Trace.TraceInformation(text);
		} // proc LuaPrint

		[LuaMember("msgbox")]
		private void LuaMsgBox(string text, string caption)
		{
			MessageBox.Show(text, caption ?? "Information", MessageBoxButton.OK, MessageBoxImage.Information);
		} // proc LuaMsgBox
		
		[LuaMember("require")]
		private void LuaRequire(string fileName)
		{
			Lua.CompileChunk(Path.Combine(BaseUri, fileName), null).Run(this);
		} // proc LuaRequire
		
		[LuaMember("command")]
		private object LuaCommand(Action<object> command, Func<object, bool> canExecute = null, bool idleCall = true)
		{
			var cmd = new LuaCommandImplementation(command, canExecute);
			if (idleCall)
				Environment.AddIdleAction(cmd);
			return cmd;
		} // func LuaCommand

		#endregion

		/// <summary>Access to the current environemnt.</summary>
		[LuaMember("Environment")]
		public PpsEnvironment Environment { get { return environment; } }
		/// <summary>Arguments of the generic content.</summary>
		[LuaMember("Arguments")]
		public LuaTable Arguments { get { return arguments; } }

		/// <summary>Wpf-Control</summary>
		public FrameworkElement Control { get { return control; } }
		/// <summary>Source of the Wpf-Control</summary>
		protected string XamlFileName { get { return sXamlFile; } }
		/// <summary>BaseUri of the Wpf-Control</summary>
		public string BaseUri { get { return Path.GetDirectoryName(sXamlFile); } }

		/// <summary>This member is resolved dynamic, that is the reason the FrameworkElement Control is public.</summary>
		object IPpsWindowPane.Control { get { return control; } }
		/// <summary></summary>
		public virtual string Title
		{
			get
			{
				var title = GetMemberValue("Title");
				if (title == null)
				{
					if (String.IsNullOrEmpty(sXamlFile))
						return String.Empty;
					else
						title = Path.GetFileName(sXamlFile);
				}
				return title.ToString();
			}
		} // prop Title

		/// <summary>Synchronization to the UI.</summary>
		public Dispatcher Dispatcher { get { return Application.Current.Dispatcher; } }
		/// <summary>Access to the current lua compiler</summary>
		public Lua Lua { get { return environment.Lua; } }
	} // class PpsGenericWpfWindowPane
}
