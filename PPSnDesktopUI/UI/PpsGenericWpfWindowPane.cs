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

namespace TecWare.PPSn.UI
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Inhalt, welche aus einem Xaml und einem Lua-Script besteht.</summary>
	public class PpsGenericWpfWindowPane : LuaTable, IPpsWindowPane
	{
		private static readonly XName xnCode = XName.Get("Code", "http://schemas.microsoft.com/winfx/2006/xaml");
		private string sXamlFile;
		private FrameworkElement control;

		private Lua lua = new Lua(); // todo: muss von außen kommen

		#region -- Ctor/Dtor --------------------------------------------------------------

		public PpsGenericWpfWindowPane(string sXamlFile)
		{
			this.sXamlFile = sXamlFile;
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

		public virtual async Task LoadAsync()
		{
			await Task.Yield();

			var xaml = XDocument.Load(sXamlFile, LoadOptions.SetBaseUri | LoadOptions.SetLineInfo);

			// Load the content of the code-tag, to initialize extend functionality
			var xCode = xaml.Root.Element(xnCode);
			if (xCode != null)
			{
				var chunk = lua.CompileChunk(xCode.Value, Path.GetFileName(sXamlFile), null);

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

		/// <summary>Name of the panel</summary>
		public virtual string Title { get { return Path.GetFileName(sXamlFile); } }

		/// <summary>Wpf-Control</summary>
		protected FrameworkElement Control { get { return control; } }
		/// <summary>Source of the Wpf-Control</summary>
		protected string XamlFileName { get { return sXamlFile; } }
		/// <summary>BaseUri of the Wpf-Control</summary>
		public string BaseUri { get { return Path.GetDirectoryName(sXamlFile); } }

		object IPpsWindowPane.Control { get { return control; } }

		/// <summary>Synchronization to the UI.</summary>
		public Dispatcher Dispatcher { get { return Application.Current.Dispatcher; } }
		/// <summary>Access to the current lua compiler</summary>
		public Lua Lua { get { return lua; } }
	} // class PpsGenericWpfWindowPane
}
