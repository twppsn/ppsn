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
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using TecWare.DE.Data;
using TecWare.PPSn.Data;

namespace TecWare.PPSn.UI
{
	#region -- interface IPpsAttachmentItem -------------------------------------------

	/// <summary>Representation for the attachments</summary>
	public interface IPpsAttachmentItem
	{
		/// <summary>Remove the current attachment</summary>
		/// <returns></returns>
		bool Remove();

		/// <summary>Displayname</summary>
		string Name { get; }
		/// <summary>Has the attachment a linked object.</summary>
		bool IsNull { get; }
		/// <summary>Is this item editable.</summary>
		bool IsReadOnly { get; }

		/// <summary>Access the data column.</summary>
		IPpsDataInfo LinkedObject { get; }
	} // interface IPpsAttachmentItem

	#endregion

	#region -- interface IPpsAttachments ----------------------------------------------

	/// <summary>Access to the attachments of an object, or table.</summary>
	public interface IPpsAttachments : IEnumerable<IPpsAttachmentItem>
	{
		/// <summary>Add's a new attachment to the list.</summary>
		/// <param name="data">Object to connect.</param>
		void Append(IPpsDataInfo data);

		/// <summary>Can we add new items.</summary>
		bool CanAdd { get; }
	} // interface IPpsAttachments

	#endregion

	#region -- class PpsAttachments ---------------------------------------------------

	/// <summary>Create proxy list/items for the attachment controls</summary>
	public static class PpsAttachments
	{
		#region -- class PpsAttachmentItemImplementation ------------------------------

		private sealed class PpsAttachmentItemImplementation : IPpsAttachmentItem, IEquatable<PpsDataRow>, INotifyPropertyChanged, ICompareFulltext
		{
			public event PropertyChangedEventHandler PropertyChanged;

			private readonly IDataRow row;			// row
			private readonly bool rowOwner;         // is the object owner of the row
			private readonly int linkColumnIndex;   // index of the object column

			private IPpsDataInfo currentLinkedObject = null;

			#region -- Ctor/Dtor ------------------------------------------------------

			public PpsAttachmentItemImplementation(IDataRow row, string linkColumnName, bool rowOwner)
			{
				this.row = row;
				this.rowOwner = rowOwner;
				this.linkColumnIndex = row.FindColumnIndex(linkColumnName ?? throw new ArgumentNullException(nameof(linkColumnName)), true);

				AttachRowPropertyChanged();
			} // ctor

			public PpsAttachmentItemImplementation(IDataRow row, int linkColumnIndex, bool rowOwner)
			{
				this.row = row;
				this.rowOwner = rowOwner;
				this.linkColumnIndex = linkColumnIndex;

				AttachRowPropertyChanged();
			} // ctor

			public override int GetHashCode()
				=> row.GetHashCode();

			public override bool Equals(object obj)
				=> obj is PpsAttachmentItemImplementation a ? a.row == row : false;

			public bool Equals(PpsDataRow other)
				=> other == row;

			private void AttachRowPropertyChanged()
			{
				AttachObjectPropertyChanged();

				if (row is INotifyPropertyChanged pc)
				{
					WeakEventManager<INotifyPropertyChanged, PropertyChangedEventArgs>.AddHandler(
					  pc, nameof(PropertyChanged), ForwardRowPropertyChanged
				  );
				}
			} // proc AttachRowPropertyChanged

			private void AttachObjectPropertyChanged()
			{
				// disconnect old
				if (currentLinkedObject is INotifyPropertyChanged pc)
					pc.PropertyChanged -= ForwarObjectPropertyChanged;

				// connect to new
				currentLinkedObject = row[linkColumnIndex] as IPpsDataInfo;
				if (currentLinkedObject is INotifyPropertyChanged pc1)
					pc1.PropertyChanged += ForwarObjectPropertyChanged;

				// raise events
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LinkedObject)));
			} // proc AttachObjectPropertyChanged

			private void ForwardRowPropertyChanged(object sender, PropertyChangedEventArgs args)
			{
				if (args.PropertyName == row.Columns[linkColumnIndex].Name)
				{
					if (currentLinkedObject != row[linkColumnIndex])
					{
						AttachObjectPropertyChanged();
					}
				}
			} // proc ForwardRowPropertyChanged

			private void ForwarObjectPropertyChanged(object sender, PropertyChangedEventArgs args)
			{
				switch (args.PropertyName)
				{
					case "Nr":
					case nameof(IPpsDataInfo.Name):
						PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
						break;
				}
			} // proc ForwarObjectPropertyChanged

			#endregion

			bool ICompareFulltext.SearchText(string text, bool startsWith)
			{
				var name = Name;
				return name == null ? false : (startsWith ? name.StartsWith(text, StringComparison.CurrentCultureIgnoreCase) : name.IndexOf(text) != -1);
			} // func ICompareFulltext.SearchText

			public bool Remove()
			{
				if (row is PpsDataRow dataRow)
				{
					if (rowOwner)
						return dataRow.Remove();
					else
					{
						dataRow[linkColumnIndex] = null;
						return true;
					}
				}
				else
					return false;
			} // proc Remove

			public bool IsNull => row[linkColumnIndex] == null;

			public bool IsReadOnly => row is PpsDataRow;

			/// <summary>Access to the object.</summary>
			public IPpsDataInfo LinkedObject => (IPpsDataInfo)row[linkColumnIndex];

			/// <summary>Displayname for the object</summary>
			public string Name => LinkedObject?.Name;
		} // class PpsAttachmentItemImplementation

		#endregion

		#region -- class PpsAttachmentsCollectionView ---------------------------------

		private sealed class PpsAttachmentsCollectionView : PpsFilterableListCollectionView, IPpsDataRowViewFilter
		{
			public PpsAttachmentsCollectionView(PpsAttachmentsImplementation list) 
				: base(list)
			{
			} // ctor

			protected override Predicate<object> CreateFilterPredicate(PpsDataFilterExpression filterExpression)
			{
				var filterFunc = PpsDataFilterVisitorLambda.CompileTypedFilter<IPpsAttachmentItem>(filterExpression);
				return new Predicate<object>(o => filterFunc((IPpsAttachmentItem)o));
			} // func CreateFilterPredicate
		} // class PpsAttachmentsCollectionView

		#endregion

		#region -- class PpsAttachmentsImplementation ---------------------------------

		private sealed class PpsAttachmentsImplementation : IList, INotifyCollectionChanged, IPpsAttachments, ICollectionViewFactory
		{
			public event NotifyCollectionChangedEventHandler CollectionChanged;

			private readonly IEnumerable<IDataRow> sourceRows;
			private readonly List<IPpsAttachmentItem> attachmentRows;
			private readonly int linkColumnIndex;

			public PpsAttachmentsImplementation(IEnumerable<IDataRow> rows, int linkColumnIndex)
			{
				sourceRows = rows;
				this.linkColumnIndex = linkColumnIndex;

				attachmentRows = new List<IPpsAttachmentItem>();

				if(sourceRows is INotifyCollectionChanged ncc)
				{
					WeakEventManager<INotifyCollectionChanged, NotifyCollectionChangedEventArgs>.AddHandler(
						ncc, nameof(INotifyCollectionChanged.CollectionChanged), (sender, e) => SourceCollectionChanged(sender, e)
					);
				}

				RefreshAttachmentRows();
			} // ctor

			#region -- Append new object ----------------------------------------------

			public void Append(IPpsDataInfo data)
			{
				if (sourceRows is IPpsDataView view)
				{
					using (var trans = view.Table.DataSet.UndoSink?.BeginTransaction("Datei hinzugefügt."))
					{
						var r = view.NewRow(null, null);
						r[linkColumnIndex] = data;
						view.Add(r);
						trans.Commit();
					}
				}
			} // proc Append

			public bool CanAdd => sourceRows is IPpsDataView;

			#endregion

			#region -- Row Management -------------------------------------------------

			public ICollectionView CreateView()
				=> new PpsAttachmentsCollectionView(this);
			
			private void RefreshAttachmentRows()
			{
				attachmentRows.Clear();
				attachmentRows.AddRange(
					sourceRows.Select(c => new PpsAttachmentItemImplementation(c, linkColumnIndex, true))
				);

				OnCollectionChanged();
			} // proc RefreshAttachmentRows

			private void SourceCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
			{
				switch (e.Action)
				{
					case NotifyCollectionChangedAction.Add:
						{
							var startIndex = e.NewStartingIndex;
							foreach (var o in e.NewItems)
								attachmentRows.Insert(startIndex++, new PpsAttachmentItemImplementation((IDataRow)o, linkColumnIndex, true));

							OnCollectionChanged();
						}
						break;
					case NotifyCollectionChangedAction.Remove:
						{
							var startIndex = e.OldStartingIndex;
							foreach (var o in e.OldItems)
							{
								if (o != attachmentRows[startIndex])
									throw new InvalidOperationException();
								attachmentRows.RemoveAt(startIndex++);
							}

							OnCollectionChanged();
						}
						break;
					case NotifyCollectionChangedAction.Reset:
						RefreshAttachmentRows();
						break;
					case NotifyCollectionChangedAction.Replace:
					case NotifyCollectionChangedAction.Move:
					default:
						throw new NotSupportedException();
				}
			} // proc OnCollectionChanged

			private void OnCollectionChanged()
				=> CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

			public IEnumerator<IPpsAttachmentItem> GetEnumerator()
				=> attachmentRows.GetEnumerator();

			IEnumerator IEnumerable.GetEnumerator()
				=> GetEnumerator();

			public int IndexOf(IPpsAttachmentItem value)
				=> attachmentRows.IndexOf(value);

			public IPpsAttachmentItem this[int index] => attachmentRows[index];
			public int Count => attachmentRows.Count;

			void ICollection.CopyTo(Array array, int index) => ((IList)attachmentRows).CopyTo(array, index);
			bool IList.Contains(object value) => IndexOf((IPpsAttachmentItem)value) >= 0;
			int IList.IndexOf(object value) => IndexOf((IPpsAttachmentItem)value);

			int IList.Add(object value) => throw new NotSupportedException();
			void IList.Insert(int index, object value) => throw new NotSupportedException();
			void IList.Remove(object value) => throw new NotSupportedException();
			void IList.RemoveAt(int index) => throw new NotSupportedException();
			void IList.Clear() => throw new NotSupportedException();

			object IList.this[int index] { get => this[index]; set => throw new NotSupportedException(); }

			bool IList.IsReadOnly => true;
			bool IList.IsFixedSize => false;
			object ICollection.SyncRoot => null;
			bool ICollection.IsSynchronized => false;

			#endregion

			public IEnumerable<IDataRow> SourceRows => sourceRows;
		} // class PpsAttachmentImplementation

		#endregion

		#region -- class PpsAttachmentsConverter --------------------------------------

		private sealed class PpsAttachmentsConverter : IValueConverter
		{
			object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
			{
				switch (value)
				{
					case IPpsDataView v:
						return CreateAttachments(v, parameter as string);
					case PpsDataRow i:
						return CreateAttachmentItem(i, parameter as string);
					case null:
						throw new ArgumentNullException(nameof(value));
					default:
						throw new NotSupportedException($"Convert '{value.GetType().Name}' to a attachment interface.");
				}
			} // func Convert

			object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
				=> value is PpsAttachmentsImplementation v
					? v.SourceRows
					: throw new NotSupportedException();
		} // class PpsAttachmentsConverter

		#endregion

		/// <summary>Create a attachemnts converter for a <see cref="IPpsDataView"/>.</summary>
		/// <param name="dataView"></param>
		/// <param name="linkColumnName"></param>
		public static IPpsAttachments CreateAttachments(IPpsDataView dataView, string linkColumnName = null)
			=> new PpsAttachmentsImplementation(dataView, dataView.FindColumnIndex(linkColumnName ?? "ObjkId", true));

		/// <summary>Create a attachment wrapper for a column.</summary>
		/// <param name="row"></param>
		/// <param name="linkColumnName"></param>
		/// <returns></returns>
		public static IPpsAttachmentItem CreateAttachmentItem(IDataRow row, string linkColumnName)
			=> new PpsAttachmentItemImplementation(row, row.FindColumnIndex(linkColumnName ?? throw new ArgumentNullException(nameof(linkColumnName)), true), false);

		/// <summary>Default converter for Zusa-Tables (the table needs a ObjkId</summary>
		public static IValueConverter DefaultZusaTableConverter { get; } = new PpsAttachmentsConverter();
	} // class PpsAttachments

	#endregion
}
