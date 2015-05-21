using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
		private class LuaCommandImplementaion : ICommand
		{
			private Action command;

			public LuaCommandImplementaion(Action command)
			{
				this.command = command;
			} // ctor

			public bool CanExecute(object parameter)
			{
				return true;
			}

			public event EventHandler CanExecuteChanged;

			public void Execute(object parameter)
			{
				command();
			}
		} // class LuaCommandImplementaion

		private PpsDataSet data;

		public PpsGenericMaskWindowPane(string sXamlFile)
			: base(sXamlFile)
		{
		} // ctor

		public override async Task LoadAsync()
		{
			await Task.Yield();

			// Lade die Definition
			string sSchema = Path.ChangeExtension(XamlFileName, ".sxml");
			var def = new PpsDataSetClientDefinition(XDocument.Load(sSchema).Root);
			data = def.CreateDataSet();

			string sData = Path.ChangeExtension(XamlFileName, ".dxml");
			data.Read(XDocument.Load(sData).Root);

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
		private object LuaCommand(Action command)
		{
			return new LuaCommandImplementaion(command);
		} // func LuaCommand

		[LuaMember("require")]
		private void LuaRequire(string sFileName)
		{
			Lua.CompileChunk(Path.Combine(BaseUri, sFileName), null).Run(this);
		} // proc LuaRequire

		[LuaMember("Data")]
		public PpsDataSet Data { get { return data; } }

		public override string Title
		{
			get
			{
				if (data == null)
					return "Maske wird geladen...";
				else
					return ((dynamic)data).Caption;
			}
		} // prop Title
	} // class PpsGenericMaskWindowPane
}
