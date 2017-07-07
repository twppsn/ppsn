using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using TecWare.DE.Networking;
using TecWare.PPSn.Data;

namespace TecWare.PPSn.Controls
{
	public interface IPpsPictureItem
	{
		bool Clear();
		void Exchange(PpsObject data);
		ImageSource Image { get; }
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
						ofd.Filter = StuffIO.FilterFromMimeType(new string[] { "image" }, new string[] { MimeTypes.Image.Icon });
						if (ofd.ShowDialog() ?? false)
						{
							using (var trans = await Environment.MasterData.CreateTransactionAsync(PpsMasterDataTransactionLevel.Write))
							{
								var obj = await Environment.CreateNewObjectAsync(Environment.ObjectInfos[PpsEnvironment.AttachmentObjectTyp]);

								obj.Tags.UpdateTag(Environment.UserId, "Filename", PpsObjectTagClass.Text, ofd.FileName);
								obj.Tags.UpdateTag(Environment.UserId, "PictureItemType", PpsObjectTagClass.Text, "Grundriss");

								var data = await obj.GetDataAsync<PpsObjectImageData>();
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

	public sealed class PpsDataObjectPictureConverter : IValueConverter
	{
		private sealed class PpsPictureItemImplementation : IPpsPictureItem, INotifyPropertyChanged
		{
			private readonly IPpsDataView view;
			private readonly string pictureTag;
			private readonly int linkColumnIndex;
			private PpsObject obj;
			private PpsObjectImageData dat;

			public event PropertyChangedEventHandler PropertyChanged;

			public PpsPictureItemImplementation(IPpsDataView view, string linkColumnName, string pictureTag)
			{
				view.CollectionChanged += ((sender, e) => NotifyPropertyChanged());
				this.view = view;
				this.linkColumnIndex = view.Table.TableDefinition.FindColumnIndex(linkColumnName ?? throw new ArgumentNullException(nameof(linkColumnName)), true);
				this.pictureTag = pictureTag;

				LoadObject();
			}

			private void LoadObject()
			{
				for (var i = 0; i < view.Table.AllRows.Count; i++)
				{
					var tobj = ((PpsObject)view.Table.AllRows[i][linkColumnIndex]);
					if (tobj == null)
						continue;
					tobj.UpdateLocalAsync().AwaitTask();
					var idx = tobj.Tags.IndexOf("PictureItemType");
					if (idx >= 0 && (string)tobj.Tags[idx].Value == pictureTag)
					{
						this.obj = tobj;
					}
				}
				dat = new PpsObjectImageData(obj);
				dat.PropertyChanged += Dat_PropertyChanged;
				NotifyPropertyChanged(nameof(Image));
				NotifyPropertyChanged(nameof(Preview));
			}

			private void Dat_PropertyChanged(object sender, PropertyChangedEventArgs e)
			{
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(e.PropertyName));
			}

			public bool Clear()
			{
				throw new NotImplementedException();
			}

			private PpsUndoManagerBase GetUndoManager(PpsDataSet ds)
				=> (PpsUndoManagerBase)ds.UndoSink;

			public void Exchange(PpsObject data)
			{
				using (var trans = GetUndoManager(view.Table.DataSet).BeginTransaction("Bild bearbeitet."))
				{
					if (obj != null)
						for (var i = 0; i < view.Table.AllRows.Count; i++)
						{
							var tobj = ((PpsObject)view.Table.AllRows[i][linkColumnIndex]);
							if (tobj == obj)
							{
								view.Table.RemoveAt(i);
								continue;
							}
						}

					var row = view.NewRow(null, null);
					row[linkColumnIndex] = data;
					view.Add(row);

					trans.Commit();

					this.obj = data;
					data.PropertyChanged += Dat_PropertyChanged;
				}

			}

			private void NotifyPropertyChanged(string propertyName = "")
			{
				if (String.IsNullOrEmpty(propertyName))
					LoadObject();
				else
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
			}
			//	=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

			public ImageSource Image => dat.Image;

			public ImageSource Preview => dat.Preview;

			public ImageSource Overlay => dat.Overlay;
		}

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is PpsDataRelatedFilterDesktop view)
				return new PpsPictureItemImplementation(view, LinkColumnName, PictureTag);
			return DependencyProperty.UnsetValue;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}

		public string LinkColumnName { get; set; }

		public string PictureTag { get; set; }
	}
}
