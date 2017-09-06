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
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using TecWare.DE.Networking;

namespace TecWare.PPSn.Controls
{
	/// <summary>Interaction logic for PpsPictureControl.xaml</summary>
	public partial class PpsPictureControl : UserControl
	{
		public readonly static DependencyProperty PictureSourceProperty = DependencyProperty.Register(nameof(PictureSource), typeof(IPpsAttachmentItem), typeof(PpsPictureControl));

		public readonly static RoutedUICommand RemovePictureCommand = new RoutedUICommand("RemovePicture", "RemovePicture", typeof(PpsPictureControl));
		public readonly static RoutedUICommand ChangePictureCommand = new RoutedUICommand("ChangePicture", "ChangePicture", typeof(PpsPictureControl));

		private readonly Lazy<PpsEnvironment> getEnvironment;
		
		public PpsPictureControl()
		{
			InitializeComponent();

			this.getEnvironment = new Lazy<PpsEnvironment>(() => PpsEnvironment.GetEnvironment(this));

			CommandBindings.Add(
				new CommandBinding(RemovePictureCommand,
					(sender, e) =>
					{
						ClearImage();
						e.Handled = true;
					},
					(sender, e) => e.CanExecute = !PictureSource.IsNull
				)
			);

			CommandBindings.Add(
				new CommandBinding(ChangePictureCommand,
					async (sender, e) =>
					{
						await SetImageAsync();
						e.Handled = true;
					},
					(sender, e) => e.CanExecute = true
				)
			);
		}

		private async Task SetImageAsync()
		{
			var ofd = new OpenFileDialog
			{
				Multiselect = false,
				CheckFileExists = true,
				Filter = StuffIO.FilterFromMimeType(new string[] { "image" }, new string[] { MimeTypes.Image.Icon })
			};
			
			if (ofd.ShowDialog() ?? false)
			{
				// todo: block ui
				PictureSource.LinkedObject = await Environment.CreateNewObjectFromFileAsync(ofd.FileName);
			}
		} // proc SetImageAsync

		private void ClearImage()
			=> PictureSource.Remove();

		public IPpsAttachmentItem PictureSource { get => (IPpsAttachmentItem)GetValue(PictureSourceProperty); set => SetValue(PictureSourceProperty, value); }
		public PpsEnvironment Environment => getEnvironment.Value;
	} // class PpsPictureControl
}
