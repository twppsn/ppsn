using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.PPSn.Data;

namespace TecWare.PPSn.UI
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Inhalt, welcher aus einem dynamisch geladenen Xaml besteht
	/// und einer Dataset</summary>
	public class PpsGenericMaskWindowPane : PpsGenericWpfWindowPane
	{
		#region -- Q and D --

		private class LuaCommandImplementaion : ICommand
		{
			public event EventHandler CanExecuteChanged;

			private Action command;
			private Func<bool> canExecute;

			public LuaCommandImplementaion(Action command, Func<bool> canExecute)
			{
				this.command = command;
				this.canExecute = canExecute;
			} // ctor

			public bool CanExecute(object parameter)
			{
				return canExecute == null || canExecute();
			}

			public void Execute(object parameter)
			{
				command();
			}

			public void Refresh()
			{
				if (CanExecuteChanged != null)
					CanExecuteChanged(this, EventArgs.Empty);
			}
		} // class LuaCommandImplementaion

		#endregion

		private PpsUndoManager undoManager = new PpsUndoManager();
		private PpsDataSetClient dataSet; // DataSet which is controlled by the mask

		public PpsGenericMaskWindowPane(string sXamlFile)
			: base(sXamlFile)
		{
		} // ctor

		public override async Task LoadAsync()
		{
			await Task.Yield();

			// Lade die Definition
			string sSchema = Path.ChangeExtension(XamlFileName, ".sxml");
			var def = new PpsDataSetDefinitionClient(XDocument.Load(sSchema).Root);
			dataSet = (PpsDataSetClient)def.CreateDataSet();

			string sData = Path.ChangeExtension(XamlFileName, ".dxml");
			dataSet.Read(XDocument.Load(sData).Root);

			dataSet.RegisterUndoSink(undoManager);

			// Lade die Maske
			await base.LoadAsync();

			await Dispatcher.InvokeAsync(() => OnPropertyChanged("Data"));
		} // proc LoadAsync

		[LuaMember("print")]
		private void LuaPrint(string sText)
		{
			System.Windows.MessageBox.Show(sText);
		} // proc LuaPrint

		[LuaMember("command")]
		private object LuaCommand(Action command, Func<bool> canExecute = null)
		{
			return new LuaCommandImplementaion(command, canExecute);
		} // func LuaCommand

		[LuaMember("require")]
		private void LuaRequire(string sFileName)
		{
			Lua.CompileChunk(Path.Combine(BaseUri, sFileName), null).Run(this);
		} // proc LuaRequire

		[LuaMember("UndoManager")]
		public PpsUndoManager LuaUndoManager { get { return undoManager; } }

		[LuaMember("Data")]
		public PpsDataSet Data { get { return dataSet; } }

		public override string Title
		{
			get
			{
				if (dataSet == null)
					return "Maske wird geladen...";
				else
					return ((dynamic)dataSet).Caption;
			}
		} // prop Title
	} // class PpsGenericMaskWindowPane
}
