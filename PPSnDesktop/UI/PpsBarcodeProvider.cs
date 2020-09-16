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
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TecWare.DE.Stuff;
using TecWare.PPSn.Controls;

namespace TecWare.PPSn.UI
{
	#region -- interface IPpsWindowBarcodeProvider ------------------------------------

	/// <summary>Barcode simulator</summary>
	public interface IPpsWindowBarcodeProvider
	{
		/// <summary>Add command bindings to the ui.</summary>
		/// <param name="element"></param>
		void AddCommandBinding(UIElement element);

		/// <summary></summary>
		/// <param name="barcode"></param>
		void AppendBarcode(string barcode);
		/// <summary></summary>
		/// <param name="barcode"></param>
		void RemoveBarcode(string barcode);

		/// <summary>List of stored barcodes</summary>
		IList<string> Barcodes { get; }
	} // interface IPpsWindowBarcodeProvider

	#endregion

	#region -- class PpsWindowBarcodeProvider -----------------------------------------

	[PpsService(typeof(IPpsWindowBarcodeProvider))]
	internal sealed class PpsWindowBarcodeProvider : IPpsShellService, IPpsWindowBarcodeProvider, IPpsBarcodeProvider
	{
		private const string barcodeStoreKey = "PPSn.Barcodes.Local";

		private readonly IPpsShell shell;
		private readonly ObservableCollection<string> storedBarcodes = new ObservableCollection<string>();

		public PpsWindowBarcodeProvider(IPpsShell shell)
		{
			this.shell = shell ?? throw new ArgumentNullException(nameof(shell));

			ReadBarcodes();
		} // ctor

		private void ReadBarcodes()
		{
			storedBarcodes.Clear();
			foreach (var cur in shell.Settings.GetProperty(barcodeStoreKey, String.Empty).Split('\n'))
			{
				if (!String.IsNullOrEmpty(cur))
					storedBarcodes.Add(cur);
			}
		} // proc ReadBarcodes

		private async Task SaveBarcodesAsync()
		{
			using (var edit = shell.Settings.Edit())
			{
				edit.Set(barcodeStoreKey, String.Join("\n", storedBarcodes));
				await edit.CommitAsync();
			}
		} // proc SaveBarcodes

		public void AppendBarcode(string barcode)
		{
			var idx = storedBarcodes.IndexOf(barcode);
			if (idx >= 0)
			{
				if (idx < storedBarcodes.Count - 1)
				{
					storedBarcodes.Move(idx, storedBarcodes.Count - 1);
					SaveBarcodesAsync().OnException();
				}
			}
			else
			{
				storedBarcodes.Add(barcode);
				SaveBarcodesAsync().OnException();
			}
		} // proc AppendBarcode

		public void RemoveBarcode(string barcode)
		{
			storedBarcodes.Remove(barcode);
			SaveBarcodesAsync().OnException();
		} // proc RemoveBarcode

		private bool CanExecuteBarcode(PpsCommandContext ctx)
		{
			return ctx.Parameter is string barcode
				? !String.IsNullOrEmpty(barcode)
				: ctx.Parameter is TextBox txt
					? !String.IsNullOrEmpty(txt.Text)
					: false;
		} // func CanExecuteBarcode

		private void DispatchBarcode(string barcode)
			=> PpsShell.GetService<PpsBarcodeService>(true).DispatchBarcode(this, barcode);

		private void SendBarcode(PpsCommandContext ctx)
		{
			if (ctx.Parameter is string barcode)
				DispatchBarcode(barcode);
			else if (ctx.Parameter is TextBox txt)
			{
				barcode = txt.Text.Trim();
				AppendBarcode(barcode);
				DispatchBarcode(barcode);
				txt.Clear();
			}
		} // proc SendBarcode

		private void RemoveBarcode(PpsCommandContext ctx)
		{
			if (ctx.Parameter is string barcode)
				RemoveBarcode(barcode);
		} // proc RemoveBarcode

		public void AddCommandBinding(UIElement element)
		{
			element.AddCommandBinding(shell, PpsBarcodePopup.RemoveBarcodeCommand, new PpsCommand(RemoveBarcode));
			element.AddCommandBinding(shell, PpsBarcodePopup.SendBarcodeCommand, new PpsCommand(SendBarcode, CanExecuteBarcode));
		} // proc AddCommandBinding

		public IList<string> Barcodes => storedBarcodes;

		string IPpsBarcodeProvider.Description => "Simulator";
		string IPpsBarcodeProvider.Type => "ui";

		public IPpsShell Shell { get => shell; set { } }
	} // class PpsWindowBarcodeProvider

	#endregion
}
