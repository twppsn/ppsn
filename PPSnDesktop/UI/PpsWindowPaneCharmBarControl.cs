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
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TecWare.PPSn.Data;

namespace TecWare.PPSn.UI
{
	#region -- class PpsWindowPaneObjectInfo ------------------------------------------

	/// <summary>Holds information container of an data-object.</summary>
	public sealed class PpsWindowPaneObjectInfo : INotifyPropertyChanged
	{
		/// <summary></summary>
		public event PropertyChangedEventHandler PropertyChanged;

		private string text = null;
		private object image = null;
		private object content = null;

		public string Text
		{
			get => text;
			set
			{
				if (text != value)
				{
					text = value;
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
				}
			}
		} // prop Text

		public object Image
		{
			get => image;
			set
			{
				if (!Equals(image, value))
				{
					image = value;
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Image)));
				}
			}
		} // prop Image

		public object Content
		{
			get => content;
			set
			{
				if (!Equals(content, value))
				{
					content = value;
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Content)));
				}
			}
		} // prop Content
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

		public static readonly DependencyProperty CurrentDataProperty = DependencyProperty.Register(nameof(CurrentData), typeof(object), typeof(PpsWindowPaneCharmBarControl), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnCurentDataChanged)));

		private static void OnCurentDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsWindowPaneCharmBarControl)d).OnCurrentDataChanged(e.NewValue, e.OldValue);

		private void OnCurrentDataChanged(object newValue, object oldValue)
		{
			if (oldValue != null)
				ClearObject(oldValue);
			if (newValue != null)
				SetObject(newValue);
		} // proc OnCurrentDataChanged

		public object CurrentData { get => GetValue(CurrentDataProperty); set => SetValue(CurrentDataProperty, value); }

		#endregion

		#region -- Views - Property ---------------------------------------------------

		private static readonly DependencyPropertyKey viewsPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Views), typeof(IEnumerable<PpsWindowPaneObjectInfo>), typeof(PpsWindowPaneCharmBarControl), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty ViewsProperty = viewsPropertyKey.DependencyProperty;

		/// <summary>List of extented object information.</summary>
		public IEnumerable<PpsWindowPaneObjectInfo> Views => (IEnumerable<PpsWindowPaneObjectInfo>)GetValue(ViewsProperty);

		#endregion

		#region -- ObjectName - Property ----------------------------------------------

		private static readonly DependencyPropertyKey objectNamePropertyKey = DependencyProperty.RegisterReadOnly(nameof(ObjectName), typeof(string), typeof(PpsWindowPaneCharmBarControl), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty ObjectNameProperty = objectNamePropertyKey.DependencyProperty;

		public string ObjectName => (string)GetValue(ObjectNameProperty);

		#endregion

		#region -- HelpKey - Property -------------------------------------------------

		public static readonly DependencyProperty HelpKeyProperty = PpsHelpPageViewer.HelpKeyProperty.AddOwner(typeof(PpsWindowPaneCharmBarControl), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnHelpKeyChanged)));

		private static void OnHelpKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsWindowPaneCharmBarControl)d).OnHelpKeyChanged((string)e.NewValue);

		private void OnHelpKeyChanged(string newValue)
			=> helpPageViewer.HelpKey = newValue;

		public string HelpKey { get => (string)GetValue(HelpKeyProperty); set => SetValue(HelpKeyProperty, value); }

		#endregion

		#region -- CurrentVisiblePane - Property --------------------------------------

		private static readonly DependencyPropertyKey currentPanePropertyKey = DependencyProperty.RegisterReadOnly(nameof(CurrentPane), typeof(PpsWindowPaneObjectInfo), typeof(PpsWindowPaneCharmBarControl), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnCurrentPaneChanged)));
		public static readonly DependencyProperty CurrentVisiblePaneProperty = currentPanePropertyKey.DependencyProperty;

		private static void OnCurrentPaneChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsWindowPaneCharmBarControl)d).OnCurrentPaneChanged(e.NewValue, e.OldValue);

		private void OnCurrentPaneChanged(object newValue, object oldValue)
			=> IsPaneVisible = newValue != null;

		public PpsWindowPaneObjectInfo CurrentPane { get => (PpsWindowPaneObjectInfo)GetValue(CurrentVisiblePaneProperty); private set => SetValue(currentPanePropertyKey, value); }

		#endregion

		#region -- IsPaneVisible - Property -------------------------------------------

		private static readonly DependencyPropertyKey isPaneVisiblePropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsPaneVisible), typeof(bool), typeof(PpsWindowPaneCharmBarControl), new FrameworkPropertyMetadata(false));
		public static readonly DependencyProperty IsInfoVisibleProperty = isPaneVisiblePropertyKey.DependencyProperty;

		public bool IsPaneVisible { get => (bool)GetValue(IsInfoVisibleProperty); private set => SetValue(isPaneVisiblePropertyKey, value); }

		#endregion

		public readonly static RoutedUICommand ChangeContentCommand = new RoutedUICommand("ChangeContent", "ChangeContent", typeof(PpsWindowPaneCharmBarControl));

		public PpsWindowPaneCharmBarControl()
		{
			helpPage = new PpsWindowPaneObjectInfo() { Text = "Hilfe", Image = "helpCircle" };

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
				new PpsWindowPaneObjectInfo() { Text = "Notizen", Content = ppsTagsEditors[0], Image = "noteOutline" },
				new PpsWindowPaneObjectInfo() { Text = "Tags", Content = ppsTagsEditors[1], Image = "hashTag" },
				new PpsWindowPaneObjectInfo() { Text = "Attribute", Content = ppsTagsEditors[2], Image = "tagText" },
				new PpsWindowPaneObjectInfo() { Text = "Termine / Aufgaben", Content = ppsTagsEditors[3], Image = "calendarClock" }
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

		static PpsWindowPaneCharmBarControl()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsWindowPaneCharmBarControl), new FrameworkPropertyMetadata(typeof(PpsWindowPaneCharmBarControl)));
		} // cstor
	} // class PpsWindowPaneCharmBarControl
}
