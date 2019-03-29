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
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using TecWare.DE.Data;
using TecWare.PPSn.Data;

namespace TecWare.PPSn.UI
{
	#region -- class PpsWindowPaneObjectInfo ------------------------------------------

	/// <summary>Holds information container of an data-object.</summary>
	public sealed class PpsWindowPaneObjectInfo : ObservableObject
	{
		private readonly PpsWindowPaneCharmBarControl owner;

		private string text;
		private object image;
		private object content;

		/// <summary></summary>
		/// <param name="owner"></param>
		/// <param name="text"></param>
		/// <param name="image"></param>
		/// <param name="content"></param>
		public PpsWindowPaneObjectInfo(PpsWindowPaneCharmBarControl owner, string text = null, object image = null, object content = null)
		{
			this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
			this.text = text;
			this.image = image;
			this.content = content;
		} // ctor

		/// <summary>Title of the pane.</summary>
		public string Text
		{
			get => text;
			set => Set(ref text, value, nameof(Text));
		} // prop Text

		/// <summary>Image of the pane.</summary>
		public object Image
		{
			get => image;
			set => Set(ref image, value, nameof(Image));
		} // prop Image

		/// <summary>Content information of the pane.</summary>
		public object Content
		{
			get => content;
			set => Set(ref content, value, nameof(Content));
		} // prop Content

		internal void FireActiveChanged()
			=> OnPropertyChanged(nameof(IsActive));

		/// <summary>This pane active</summary>
		public bool IsActive => owner.CurrentPane == this;
	} // class PpsWindowPaneObjectInfo

	#endregion

	/// <summary>Control to present properties of the current data object.</summary>
	public sealed class PpsWindowPaneCharmBarControl : Control
	{
		private readonly PpsWindowPaneObjectInfo helpPage;
		private readonly PpsHelpPageViewer helpPageViewer;

		private readonly PpsWindowPaneObjectInfo[] ppsObjectPanes;
		private readonly PpsObjectTagsEditor[] ppsTagsEditors;

		#region -- CurrentData - Property ---------------------------------------------

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty CurrentDataProperty = DependencyProperty.Register(nameof(CurrentData), typeof(object), typeof(PpsWindowPaneCharmBarControl), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnCurentDataChanged)));
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		private static void OnCurentDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsWindowPaneCharmBarControl)d).OnCurrentDataChanged(e.NewValue, e.OldValue);

		private void OnCurrentDataChanged(object newValue, object oldValue)
		{
			if (oldValue != null)
				ClearObject(oldValue);
			if (newValue != null)
				SetObject(newValue);
		} // proc OnCurrentDataChanged

		/// <summary>Current attached data object.</summary>
		public object CurrentData { get => GetValue(CurrentDataProperty); set => SetValue(CurrentDataProperty, value); }

		#endregion

		#region -- Views - Property ---------------------------------------------------

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		private static readonly DependencyPropertyKey viewsPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Views), typeof(IEnumerable<PpsWindowPaneObjectInfo>), typeof(PpsWindowPaneCharmBarControl), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty ViewsProperty = viewsPropertyKey.DependencyProperty;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		/// <summary>List of extented object information.</summary>
		public IEnumerable<PpsWindowPaneObjectInfo> Views => (IEnumerable<PpsWindowPaneObjectInfo>)GetValue(ViewsProperty);

		#endregion

		#region -- ObjectName - Property ----------------------------------------------

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		private static readonly DependencyPropertyKey objectNamePropertyKey = DependencyProperty.RegisterReadOnly(nameof(ObjectName), typeof(string), typeof(PpsWindowPaneCharmBarControl), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty ObjectNameProperty = objectNamePropertyKey.DependencyProperty;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		/// <summary>Name of the current object.</summary>
		public string ObjectName => (string)GetValue(ObjectNameProperty);

		#endregion

		#region -- HelpKey - Property -------------------------------------------------

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty HelpKeyProperty = PpsHelpPageViewer.HelpKeyProperty.AddOwner(typeof(PpsWindowPaneCharmBarControl), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnHelpKeyChanged)));
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		private static void OnHelpKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsWindowPaneCharmBarControl)d).OnHelpKeyChanged((string)e.NewValue);

		private void OnHelpKeyChanged(string newValue)
			=> helpPageViewer.HelpKey = newValue;

		/// <summary>Current used helpkey.</summary>
		public string HelpKey { get => (string)GetValue(HelpKeyProperty); set => SetValue(HelpKeyProperty, value); }

		#endregion

		#region -- CurrentPane - Property ---------------------------------------------

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		private static readonly DependencyPropertyKey currentPanePropertyKey = DependencyProperty.RegisterReadOnly(nameof(CurrentPane), typeof(PpsWindowPaneObjectInfo), typeof(PpsWindowPaneCharmBarControl), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnCurrentPaneChanged)));
		public static readonly DependencyProperty CurrentPaneProperty = currentPanePropertyKey.DependencyProperty;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		private static void OnCurrentPaneChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsWindowPaneCharmBarControl)d).OnCurrentPaneChanged((PpsWindowPaneObjectInfo)e.NewValue, (PpsWindowPaneObjectInfo)e.OldValue);

		private void OnCurrentPaneChanged(PpsWindowPaneObjectInfo newValue, PpsWindowPaneObjectInfo oldValue)
		{
			IsPaneVisible = newValue != null;

			newValue?.FireActiveChanged();
			oldValue?.FireActiveChanged();
		} // func OnCurrentPaneChanged

		/// <summary>Current content pane.</summary>
		public PpsWindowPaneObjectInfo CurrentPane { get => (PpsWindowPaneObjectInfo)GetValue(CurrentPaneProperty); private set => SetValue(currentPanePropertyKey, value); }

		#endregion

		#region -- IsPaneVisible - Property -------------------------------------------

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		private static readonly DependencyPropertyKey isPaneVisiblePropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsPaneVisible), typeof(bool), typeof(PpsWindowPaneCharmBarControl), new FrameworkPropertyMetadata(false));
		public static readonly DependencyProperty IsInfoVisibleProperty = isPaneVisiblePropertyKey.DependencyProperty;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		/// <summary>Is the content pane visible.</summary>
		public bool IsPaneVisible { get => (bool)GetValue(IsInfoVisibleProperty); private set => SetValue(isPaneVisiblePropertyKey, value); }

		#endregion

		/// <summary>Change content of the charm-bar-control.</summary>
		public readonly static RoutedUICommand ChangeContentCommand = new RoutedUICommand("ChangeContent", "ChangeContent", typeof(PpsWindowPaneCharmBarControl));

		/// <summary>Charm bar control.</summary>
		public PpsWindowPaneCharmBarControl()
		{
			helpPage = new PpsWindowPaneObjectInfo(this) { Text = "Hilfe", Image = "help" };

			helpPageViewer = new PpsHelpPageViewer();
			helpPage.Content = helpPageViewer; // todo: add to logical tree

			ppsTagsEditors = new PpsObjectTagsEditor[]
			{
				new PpsObjectTagsEditor() { TagClass = PpsObjectTagClass.Note },
				new PpsObjectTagsEditor() { TagClass = PpsObjectTagClass.Tag },
				new PpsObjectTagsEditor() { TagClass = PpsObjectTagClass.Text },
				new PpsObjectTagsEditor() { TagClass = PpsObjectTagClass.Date },
			};
			ppsObjectPanes = new PpsWindowPaneObjectInfo[]
			{
				new PpsWindowPaneObjectInfo(this) { Text = "Notizen", Content = ppsTagsEditors[0], Image = "noteOutline" },
				new PpsWindowPaneObjectInfo(this) { Text = "Tags", Content = ppsTagsEditors[1], Image = "hashTag" },
				new PpsWindowPaneObjectInfo(this) { Text = "Attribute", Content = ppsTagsEditors[2], Image = "tagTextOutline" },
				new PpsWindowPaneObjectInfo(this) { Text = "Termine / Aufgaben", Content = ppsTagsEditors[3], Image = "calendarClock" }
			};

			SetViews(null);

			CommandBindings.Add(
				new CommandBinding(ChangeContentCommand,
					(sender, e) =>
					{
						if(e.Parameter is PpsWindowPaneObjectInfo pane)
							ChangeVisiblePane(pane);
						e.Handled = true;
					},
					(sender, e) => e.CanExecute = true
				)
			);
		} // ctor

		private void ChangeVisiblePane(PpsWindowPaneObjectInfo pane)
			=> CurrentPane = Equals(pane, CurrentPane) ? null : pane;

		private void ClearObject(object data)
		{
			foreach (var t in ppsTagsEditors)
				t.Object = null;

			SetValue(objectNamePropertyKey, null); // hide object name
			SetViews(null); // show only help
		} // proc ClearObject

		private void SetObject(object data)
		{
			SetValue(objectNamePropertyKey, (data as IPpsDataInfo)?.Name);

			if (data is PpsObject obj) // object info
			{
				foreach (var t in ppsTagsEditors)
					t.Object = obj;
				SetViews(ppsObjectPanes);
			}
			else
				SetViews(null);
		} // proc SetObject

		private void SetViews(IEnumerable<PpsWindowPaneObjectInfo> views)
		{
			SetValue(viewsPropertyKey,
				views == null
				? new PpsWindowPaneObjectInfo[] { helpPage }
				: views.Concat(new PpsWindowPaneObjectInfo[] { helpPage })
			);
		} // proc SetViews

		// -- Static ----------------------------------------------------------

		#region -- class PpsWindowPaneCharmBarWidthConverter ------------------------------

		private sealed class PpsWindowPaneCharmBarWidthConverter : IMultiValueConverter
		{
			object IMultiValueConverter.Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
			{
				var animatedWidth = (double)values[0];
				if (values[1] is FrameworkElement element)
					return animatedWidth * element.ActualWidth / 3.00;

				return animatedWidth * 480.00;
			} // func IValueConverter.Convert

			object[] IMultiValueConverter.ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
				=> throw new NotSupportedException();
		} // class PpsWindowPaneCharmBarWidthConverter

		#endregion

		static PpsWindowPaneCharmBarControl()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsWindowPaneCharmBarControl), new FrameworkPropertyMetadata(typeof(PpsWindowPaneCharmBarControl)));
		} // cstor

		/// <summary>Calculate the width of CharmBarControl when property-pane is visible.</summary>
		public static IMultiValueConverter WithConverter { get; } = new PpsWindowPaneCharmBarWidthConverter();
	} // class PpsWindowPaneCharmBarControl


}
