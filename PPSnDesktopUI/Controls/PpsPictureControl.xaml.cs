using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using TecWare.PPSn.Data;

namespace TecWare.PPSn.Controls
{
	public interface IPpsPictureItem
	{
		bool Clear();
		void Exchange(PpsObject data);
		BitmapImage Picture { get; }
	}

	/// <summary>
	/// Interaction logic for PpsPictureControl.xaml
	/// </summary>
	public partial class PpsPictureControl : UserControl
	{
		private readonly Lazy<PpsEnvironment> getEnvironment;

		public PpsPictureControl()
		{
			InitializeComponent();

			this.getEnvironment = new Lazy<PpsEnvironment>(() => PpsEnvironment.GetEnvironment(this));

			CommandBindings.Add(
				new CommandBinding(RemovePictureCommand,
					(sender, e) =>
					{
						PictureSource.Clear();
					},
					(sender, e) => e.CanExecute = true
				)
			);

			CommandBindings.Add(
				new CommandBinding(ChangePictureCommand,
					async (sender, e) =>
					{
						var ofd = new OpenFileDialog();
						ofd.Multiselect = false;
						ofd.CheckFileExists = true;
						if (ofd.ShowDialog() ?? false)
						{
							using (var trans = await Environment.MasterData.CreateTransactionAsync(PpsMasterDataTransactionLevel.Write))
							{
								var obj = await Environment.CreateNewObjectAsync(Environment.ObjectInfos[PpsEnvironment.AttachmentObjectTyp]);

								obj.Tags.UpdateTag(Environment.UserId, "Filename", PpsObjectTagClass.Text, ofd.FileName);

								var data = await obj.GetDataAsync<PpsObjectBlobData>();
								await data.ReadFromFileAsync(ofd.FileName);
								await data.CommitAsync();

								Dispatcher.Invoke(() => PictureSource.Exchange(obj));

								trans.Commit();
							}
						}
						e.Handled = true;
					},
					(sender, e) => e.CanExecute = true
				)
			);
		}

		public readonly static DependencyProperty PictureSourceProperty = DependencyProperty.Register(nameof(PictureSource), typeof(IPpsPictureItem), typeof(PpsPictureControl));

		public IPpsPictureItem PictureSource { get => (IPpsPictureItem)GetValue(PictureSourceProperty); set => SetValue(PictureSourceProperty, value); }

		public readonly static RoutedUICommand RemovePictureCommand = new RoutedUICommand("RemovePicture", "RemovePicture", typeof(PpsPictureControl));
		public readonly static RoutedUICommand ChangePictureCommand = new RoutedUICommand("ChangePicture", "ChangePicture", typeof(PpsPictureControl));

		public PpsEnvironment Environment => getEnvironment.Value;
	}

	public sealed class PpsDataObjectPictureConverter : IMultiValueConverter
	{
		private sealed class PpsPictureItemImplementation : IPpsPictureItem
		{
			private readonly IPpsDataView view;
			private readonly PpsLinkedObjectExtendedValue pictureId;
			private readonly int linkColumnIndex;

			public PpsPictureItemImplementation(IPpsDataView view, string linkColumnName, PpsLinkedObjectExtendedValue pictureId)
			{
				this.view = view;
				this.pictureId = pictureId;
				this.linkColumnIndex = view.Table.TableDefinition.FindColumnIndex(linkColumnName ?? throw new ArgumentNullException(nameof(linkColumnName)), true);
			}
			/*
			private PpsObject GetLinkedObject()
				=> (PpsObject)row[linkColumnIndex];
*/
			public bool Clear()
			{
				throw new NotImplementedException();
			}

			private PpsUndoManagerBase GetUndoManager(PpsDataSet ds)
				=> (PpsUndoManagerBase)ds.UndoSink;

			public void Exchange(PpsObject data)
			{
				using (var trans = GetUndoManager(view.Table.DataSet).BeginTransaction("Datei hinzugefügt."))
				{
					var row = view.NewRow(null, null);
					row[linkColumnIndex] = data;
					view.Add(row);
					
					trans.Commit();
				}
			}

			public BitmapImage Picture
			{
				get
				{
					var bi = new BitmapImage();
					bi.BeginInit();

					var obj = ((PpsObject)view.Table.AllRows[(int)pictureId.Value][linkColumnIndex]);
					//var data = await obj.GetDataAsync<PpsObjectBlobData>();

					//bi.StreamSource = ((dynamic)GetLinkedObject())?.RawData;
					bi.EndInit();
					return bi;
				}
			}
		}

		public object Convert(object[] value, Type targetType, object parameter, CultureInfo culture)
		{
			return null;
			if (value[0] is IPpsDataView view)
				return new PpsPictureItemImplementation(view, LinkColumnName, (PpsLinkedObjectExtendedValue)value[1]);
			return null;
		}

		public object[] ConvertBack(object value, Type[] targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
		
		public string LinkColumnName { get; set; }
	}
}
