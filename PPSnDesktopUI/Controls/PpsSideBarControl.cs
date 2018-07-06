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
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;

namespace TecWare.PPSn.Controls
{
	#region -- class SideBarItemsBase -------------------------------------------------

	#region -- class PpsSideBarFilterChangedEventArgs ---------------------------------

	/// <summary></summary>
	public class PpsSideBarFilterChangedEventArgs : RoutedEventArgs
	{
		private readonly object newFilter;
		private readonly object oldFilter;

		/// <summary></summary>
		/// <param name="routedEvent"></param>
		/// <param name="newFilter"></param>
		/// <param name="oldFilter"></param>
		public PpsSideBarFilterChangedEventArgs(RoutedEvent routedEvent, object newFilter, object oldFilter)
			: this(routedEvent, null, newFilter, oldFilter)
		{
		} // ctor

		/// <summary></summary>
		/// <param name="routedEvent"></param>
		/// <param name="source"></param>
		/// <param name="newFilter"></param>
		/// <param name="oldFilter"></param>
		public PpsSideBarFilterChangedEventArgs(RoutedEvent routedEvent, object source, object newFilter, object oldFilter)
			: base(routedEvent, source)
		{
			this.newFilter = newFilter;
			this.oldFilter = oldFilter;
		} // ctor

		/// <summary></summary>
		public object NewFilter => newFilter;
		/// <summary></summary>
		public object OldFilter => oldFilter;
	} // class PpsSideBarFilterChangedEventArgs

	#endregion

	/// <summary></summary>
	/// <param name="sender"></param>
	/// <param name="e"></param>
	public delegate void PpsSideBarFilterChangedEventHandler(object sender, PpsSideBarFilterChangedEventArgs e);

	/// <summary></summary>
	public abstract class PpsSideBarItemsBase : MultiSelector
	{
		/// <summary></summary>
		public static readonly ComponentResourceKey PpsSideBarSeparator = new ComponentResourceKey(typeof(PpsSideBarControl), "PpsSideBarSeparator");
		private static readonly DependencyProperty isItemTemplateIsItsOwnContainerProperty = DependencyProperty.Register("IsItemTemplateIsItsOwnContainer", typeof(bool), typeof(PpsSideBarItemsBase), new FrameworkPropertyMetadata(BooleanBox.False));

		#region -- Content Properties -------------------------------------------------

		/// <summary></summary>
		public static readonly DependencyProperty ContentProperty = ContentControl.ContentProperty.AddOwner(typeof(PpsSideBarItemsBase), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnContentChanged)));
		/// <summary></summary>
		public static readonly DependencyProperty ContentTemplateProperty = ContentControl.ContentTemplateProperty.AddOwner(typeof(PpsSideBarItemsBase), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnContentTemplateChanged)));
		/// <summary></summary>
		public static readonly DependencyProperty ContentTemplateSelectorProperty = ContentControl.ContentTemplateSelectorProperty.AddOwner(typeof(PpsSideBarItemsBase), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnContentTemplateSelectorChanged)));
		/// <summary></summary>
		public static readonly DependencyProperty ContentStringFormatProperty = ContentControl.ContentStringFormatProperty.AddOwner(typeof(PpsSideBarItemsBase), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnContentStringFormatChanged)));

		private static readonly DependencyPropertyKey hasContentPropertyKey = DependencyProperty.RegisterReadOnly(nameof(HasContent), typeof(bool), typeof(PpsSideBarItemsBase), new FrameworkPropertyMetadata(BooleanBox.False));
		/// <summary></summary>
		public static readonly DependencyProperty HasContentProperty = hasContentPropertyKey.DependencyProperty;

		private static void OnContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var ctrl = (PpsSideBarItemsBase)d;
			ctrl.SetValue(hasContentPropertyKey, e.NewValue != null);
			ctrl.OnContentChanged(e.NewValue, e.OldValue);
		} // proc OnContentChanged

		private static void OnContentTemplateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var ctrl = (PpsSideBarItemsBase)d;
			ctrl.OnContentTemplateChanged((DataTemplate)e.NewValue, (DataTemplate)e.OldValue);
		} // proc OnContentTemplateChanged

		private static void OnContentTemplateSelectorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var ctrl = (PpsSideBarItemsBase)d;
			ctrl.OnContentTemplateSelectorChanged((DataTemplateSelector)e.NewValue, (DataTemplateSelector)e.OldValue);
		} // proc OnContentTemplateSelectorChanged

		private static void OnContentStringFormatChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var ctrl = (PpsSideBarItemsBase)d;
			ctrl.OnContentStringFormatChanged((string)e.NewValue, (string)e.OldValue);
		} // proc OnContentStringFormatChanged

		/// <summary>Content of the base panel</summary>
		public object Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

		/// <summary>Content template</summary>
		public DataTemplate ContentTemplate { get => (DataTemplate)GetValue(ContentTemplateProperty); set => SetValue(ContentTemplateProperty, value); }

		/// <summary>Content template selector for the content.</summary>
		public DataTemplateSelector ContentTemplateSelector { get => (DataTemplateSelector)GetValue(ContentTemplateSelectorProperty); set => SetValue(ContentTemplateSelectorProperty, value); }

		/// <summary></summary>
		public string ContentStringFormat { get => (string)GetValue(ContentStringFormatProperty); set => SetValue(ContentStringFormatProperty, value); }

		/// <summary>Is Content set</summary>
		public bool HasContent { get => BooleanBox.GetBool(GetValue(HasContentProperty)); private set => SetValue(hasContentPropertyKey, BooleanBox.GetObject(value)); }

		#endregion

		#region -- Selected Content Properties ----------------------------------------

		private static readonly DependencyPropertyKey selectedContentPropertyKey = DependencyProperty.RegisterReadOnly(nameof(SelectedContent), typeof(object), typeof(PpsSideBarItemsBase), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnSelectedContentChanged)));
		private static readonly DependencyPropertyKey selectedContentTemplatePropertyKey = DependencyProperty.RegisterReadOnly(nameof(SelectedContentTemplate), typeof(DataTemplate), typeof(PpsSideBarItemsBase), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnSelectedContentTemplateChanged)));
		private static readonly DependencyPropertyKey selectedContentTemplateSelectorPropertyKey = DependencyProperty.RegisterReadOnly(nameof(SelectedContentTemplateSelector), typeof(DataTemplateSelector), typeof(PpsSideBarItemsBase), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnSelectedContentTemplateSelectorChanged)));
		private static readonly DependencyPropertyKey selectedContentStringFormatPropertyKey = DependencyProperty.RegisterReadOnly(nameof(SelectedContentStringFormat), typeof(string), typeof(PpsSideBarItemsBase), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnSelectedContentStringFormatChanged)));
		private static readonly DependencyPropertyKey hasSelectedContentPropertyKey = DependencyProperty.RegisterReadOnly(nameof(HasSelectedContent), typeof(bool), typeof(PpsSideBarItemsBase), new FrameworkPropertyMetadata(BooleanBox.False));
		private static readonly DependencyPropertyKey selectedFilterPropertyKey = DependencyProperty.RegisterReadOnly(nameof(SelectedFilter), typeof(object), typeof(PpsSideBarItemsBase), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnSelectedFilterChanged)));
		private static readonly DependencyPropertyKey hasSelectedFilterPropertyKey = DependencyProperty.RegisterReadOnly(nameof(HasSelectedFilter), typeof(bool), typeof(PpsSideBarItemsBase), new FrameworkPropertyMetadata(BooleanBox.False));

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty SelectedContentProperty = selectedContentPropertyKey.DependencyProperty;
		public static readonly DependencyProperty SelectedContentTemplateProperty = selectedContentTemplatePropertyKey.DependencyProperty;
		public static readonly DependencyProperty SelectedContentTemplateSelectorProperty = selectedContentTemplateSelectorPropertyKey.DependencyProperty;
		public static readonly DependencyProperty SelectedContentStringFormatProperty = selectedContentStringFormatPropertyKey.DependencyProperty;
		public static readonly DependencyProperty HasSelectedContentProperty = hasSelectedContentPropertyKey.DependencyProperty;

		public static readonly DependencyProperty SelectedFilterProperty = selectedFilterPropertyKey.DependencyProperty;
		public static readonly DependencyProperty HasSelectedFilterProperty = hasSelectedFilterPropertyKey.DependencyProperty;

		public static readonly RoutedEvent SelectedContentChangedEvent = EventManager.RegisterRoutedEvent(nameof(SelectedContentChanged), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PpsSideBarItemsBase));
		public static readonly RoutedEvent SelectedFilterChangedEvent = EventManager.RegisterRoutedEvent(nameof(SelectedFilterChanged), RoutingStrategy.Bubble, typeof(PpsSideBarFilterChangedEventHandler), typeof(PpsSideBarItemsBase));
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		private static void OnSelectedContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var sidebar = (PpsSideBarItemsBase)d;
			sidebar.HasSelectedContent = e.NewValue != null;
			sidebar.OnSelectedContentChanged(e.NewValue, e.OldValue);
		} // proc OnSelectedContentChanged

		/// <summary></summary>
		/// <param name="newValue"></param>
		/// <param name="oldValue"></param>
		protected virtual void OnSelectedContentChanged(object newValue, object oldValue)
			=> RaiseEvent(new RoutedEventArgs(SelectedContentChangedEvent, this));

		private static void OnSelectedContentTemplateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var sidebar = (PpsSideBarItemsBase)d;
			sidebar.OnSelectedContentTemplateChanged((DataTemplate)e.NewValue, (DataTemplate)e.OldValue);
		} // proc OnSelectedContentTemplateChanged

		/// <summary></summary>
		/// <param name="newValue"></param>
		/// <param name="oldValue"></param>
		protected virtual void OnSelectedContentTemplateChanged(DataTemplate newValue, DataTemplate oldValue) { }

		private static void OnSelectedContentTemplateSelectorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var sidebar = (PpsSideBarItemsBase)d;
			sidebar.OnSelectedContentTemplateSelectorChanged((DataTemplateSelector)e.NewValue, (DataTemplateSelector)e.OldValue);
		} // proc OnSelectedContentTemplateSelectorChanged

		/// <summary></summary>
		/// <param name="newValue"></param>
		/// <param name="oldValue"></param>
		protected virtual void OnSelectedContentTemplateSelectorChanged(DataTemplateSelector newValue, DataTemplateSelector oldValue) { }

		private static void OnSelectedContentStringFormatChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var sidebar = (PpsSideBarItemsBase)d;
			sidebar.OnSelectedContentStringFormatChanged((string)e.NewValue, (string)e.OldValue);
		} // proc OnSelectedContentStringFormatChanged

		/// <summary></summary>
		/// <param name="newValue"></param>
		/// <param name="oldValue"></param>
		protected virtual void OnSelectedContentStringFormatChanged(string newValue, string oldValue) { }

		private static void OnSelectedFilterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var sidebar = (PpsSideBarItemsBase)d;
			sidebar.HasSelectedFilter = e.NewValue != null;
			sidebar.OnSelectedFilterChanged(e.NewValue, e.OldValue);
		} // proc OnSelectedFilterChanged

		/// <summary></summary>
		/// <param name="newValue"></param>
		/// <param name="oldValue"></param>
		protected virtual void OnSelectedFilterChanged(object newValue, object oldValue)
			=> RaiseEvent(new PpsSideBarFilterChangedEventArgs(SelectedFilterChangedEvent, this, newValue, oldValue));

		/// <summary></summary>
		public object SelectedContent { get => GetValue(SelectedContentProperty); private set => SetValue(selectedContentPropertyKey, value); }
		/// <summary></summary>
		public DataTemplate SelectedContentTemplate { get => (DataTemplate)GetValue(SelectedContentTemplateProperty); private set => SetValue(selectedContentTemplatePropertyKey, value); }
		/// <summary></summary>
		public DataTemplateSelector SelectedContentTemplateSelector { get => (DataTemplateSelector)GetValue(SelectedContentTemplateSelectorProperty); private set => SetValue(selectedContentTemplateSelectorPropertyKey, value); }
		/// <summary></summary>
		public string SelectedContentStringFormat { get => (string)GetValue(SelectedContentStringFormatProperty); private set => SetValue(selectedContentStringFormatPropertyKey, value); }
		/// <summary></summary>
		public bool HasSelectedContent { get => BooleanBox.GetBool(GetValue(HasSelectedContentProperty)); private set => SetValue(hasSelectedContentPropertyKey, BooleanBox.GetObject(value)); }

		/// <summary></summary>
		public object SelectedFilter { get => GetValue(SelectedFilterProperty); private set => SetValue(selectedFilterPropertyKey, value); }
		/// <summary></summary>
		public bool HasSelectedFilter { get => BooleanBox.GetBool(GetValue(HasSelectedFilterProperty)); private set => SetValue(hasSelectedFilterPropertyKey, BooleanBox.GetObject(value)); }

		/// <summary></summary>
		public event RoutedEventHandler SelectedContentChanged { add => AddHandler(SelectedContentChangedEvent, value); remove => RemoveHandler(SelectedContentChangedEvent, value); }
		/// <summary></summary>
		public event PpsSideBarFilterChangedEventHandler SelectedFilterChanged { add => AddHandler(SelectedFilterChangedEvent, value); remove => RemoveHandler(SelectedFilterChangedEvent, value); }

		#endregion

		/// <summary>Only one element can be selected.</summary>
		/// <param name="e"></param>
		protected override void OnInitialized(EventArgs e)
		{
			CanSelectMultipleItems = false;
			base.OnInitialized(e);
		} // proc OnInitialized

		/// <summary></summary>
		/// <param name="e"></param>
		protected override void OnSelectionChanged(SelectionChangedEventArgs e)
		{
			base.OnSelectionChanged(e);
			UpdateSelectedContent();
		} // proc OnSelectionChanged

		internal static void UpdateParentSelectedContent(DependencyObject d)
		{
			if (ItemsControlFromItemContainer(d) is PpsSideBarItemsBase sidebar)
				sidebar.UpdateSelectedContent();
		} // proc UpdateParentSelectedContent

		private DependencyObject GetContainerItem(object item)
		{
			if (item == null)
				return null;
			else if (IsItemItsOwnContainer(item) && item is DependencyObject d)
				return d;
			else 
			{
				d = ItemContainerGenerator.ContainerFromItem(item);
				return d == DependencyProperty.UnsetValue ? null : d;
			}
		} // func GetContainerItem

		/// <summary></summary>
		protected internal void UpdateSelectedContent()
		{
			switch (GetContainerItem(SelectedItem))
			{
				case PpsSideBarItemsBase itemsBase:
					SelectedContent = itemsBase.SelectedContent ?? Content;
					SelectedContentTemplate = itemsBase.SelectedContentTemplate ?? ContentTemplate;
					SelectedContentTemplateSelector = itemsBase.SelectedContentTemplateSelector ?? ContentTemplateSelector;
					SelectedContentStringFormat = itemsBase.SelectedContentStringFormat ?? ContentStringFormat;
					SelectedFilter = itemsBase.SelectedFilter ?? null;
					break;
				case PpsSideBarPanel panel:
					SelectedContent = panel.Content ?? Content;
					SelectedContentTemplate = panel.ContentTemplate ?? ContentTemplate;
					SelectedContentTemplateSelector = panel.ContentTemplateSelector ?? ContentTemplateSelector;
					SelectedContentStringFormat = panel.ContentStringFormat ?? ContentStringFormat;
					SelectedFilter = null;
					break;
				case PpsSideBarPanelFilter filter:
					SelectedContent = Content;
					SelectedContentTemplate = ContentTemplate;
					SelectedContentTemplateSelector = ContentTemplateSelector;
					SelectedContentStringFormat = ContentStringFormat;
					SelectedFilter = filter.Filter;
					break;
				default:
					SelectedContent = Content;
					SelectedContentTemplate = ContentTemplate;
					SelectedContentTemplateSelector = ContentTemplateSelector;
					SelectedContentStringFormat = ContentStringFormat;
					SelectedFilter = null;
					break;
			}
		} // func UpdateSelectedContent

		/// <summary></summary>
		/// <param name="newValue"></param>
		/// <param name="oldValue"></param>
		protected virtual void OnContentChanged(object newValue, object oldValue)
		{
			RemoveLogicalChild(oldValue);
			AddLogicalChild(newValue);

			UpdateSelectedContent();
		} // proc OnContentChanged

		/// <summary></summary>
		/// <param name="newValue"></param>
		/// <param name="oldValue"></param>
		protected virtual void OnContentTemplateChanged(DataTemplate newValue, DataTemplate oldValue)
			=> UpdateSelectedContent();

		/// <summary></summary>
		/// <param name="newValue"></param>
		/// <param name="oldValue"></param>
		protected virtual void OnContentTemplateSelectorChanged(DataTemplateSelector newValue, DataTemplateSelector oldValue)
			=> UpdateSelectedContent();

		/// <summary></summary>
		/// <param name="newValue"></param>
		/// <param name="oldValue"></param>
		protected virtual void OnContentStringFormatChanged(string newValue, string oldValue)
			=> UpdateSelectedContent();

		/// <summary></summary>
		/// <returns></returns>
		protected override DependencyObject GetContainerForItemOverride()
		{
			if (BooleanBox.GetBool(GetValue(isItemTemplateIsItsOwnContainerProperty)))
			{
				var d = ItemTemplate.LoadContent();
				if (IsItemItsOwnContainer(d))
					return d;
			}
			return new PpsSideBarPanel();
		} // func GetContainerForItemOverride

		/// <summary></summary>
		/// <param name="element"></param>
		/// <param name="item"></param>
		protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
		{
			if (element is PpsSideBarPanel panel) // do not copy TemplateXXX die HeaderTemplateXXX
				return;
			else if (element is Separator sep)
				sep.SetValue(DefaultStyleKeyProperty, PpsSideBarSeparator);

			base.PrepareContainerForItemOverride(element, item);
		} // proc PrepareContainerForItemOverride

		/// <summary></summary>
		/// <param name="oldValue"></param>
		/// <param name="newValue"></param>
		protected override void OnItemTemplateChanged(DataTemplate oldValue, DataTemplate newValue)
		{
			base.OnItemTemplateChanged(oldValue, newValue);
			if (newValue.HasContent)
			{
				var d = newValue.LoadContent();
				SetValue(isItemTemplateIsItsOwnContainerProperty, BooleanBox.GetObject(IsItemItsOwnContainer(d)));
			}
			else
				SetValue(isItemTemplateIsItsOwnContainerProperty, BooleanBox.False);
		} // proc OnItemTemplateChanged 

		/// <summary></summary>
		/// <param name="item"></param>
		/// <param name="filter"></param>
		/// <returns></returns>
		public bool SelectItem(object item, object filter = null)
		{
			if (HasItems)
			{
				foreach (var cur in Items)
				{
					var container = GetContainerItem(cur);
					if (cur == item)
					{
						if (container is PpsSideBarGroup g)
							return g.SetSelected(true);
						else
						{
							SetIsSelected(container, true);
							return true;
						}
					}
					else if (container is PpsSideBarItemsBase g && g.SelectItem(item, filter))
						return true;
				}
			}
			return false;
		} // func SelectItem

		/// <summary></summary>
		protected override System.Collections.IEnumerator LogicalChildren
			=> LogicalContentEnumerator.GetLogicalEnumerator(this, base.LogicalChildren, () => Content);
	} // class SideBarItemsBase

	#endregion

	#region -- class PpsSideBarControl ------------------------------------------------

	/// <summary></summary>
	public class PpsSideBarControl : PpsSideBarItemsBase
	{
		/// <summary></summary>
		public static readonly DependencyProperty AllowToggleSelectionProperty = DependencyProperty.Register(nameof(AllowToggleSelection), typeof(bool), typeof(PpsSideBarControl), new FrameworkPropertyMetadata(true));
		
		/// <summary></summary>
		/// <param name="item"></param>
		/// <returns></returns>
		protected override bool IsItemItsOwnContainerOverride(object item)
			=> item is PpsSideBarGroup
			|| item is PpsSideBarPanel
			|| item is PpsSideBarPanelFilter
			|| item is Separator;

		/// <summary></summary>
		public bool AllowToggleSelection { get => BooleanBox.GetBool(GetValue(AllowToggleSelectionProperty)); set => SetValue(AllowToggleSelectionProperty, BooleanBox.GetObject(value)); }

		internal static PpsSideBarControl GetSideBarControl(DependencyObject d)
		{
			switch (ItemsControlFromItemContainer(d))
			{
				case PpsSideBarControl r:
					return r;
				case PpsSideBarGroup g:
					return GetSideBarControl(g);
				default:
					return null;
			}
		} // func GetSideBarControl

		internal static int GetIndentLevel(DependencyObject d)
		{
			switch (ItemsControlFromItemContainer(d))
			{
				case PpsSideBarControl r:
					return 0;
				case PpsSideBarGroup g:
					return GetIndentLevel(g) + 1;
				default:
					return 0;
			}
		} // func GetIndentLevel

		internal static bool CanChangeSetSelection(DependencyObject d, bool setActive)
			=> setActive || GetAllowToggleSelection(d);

		private static bool GetAllowToggleSelection(DependencyObject d)
			=> GetSideBarControl(d)?.AllowToggleSelection ?? BooleanBox.GetBool(AllowToggleSelectionProperty.DefaultMetadata.DefaultValue);

		static PpsSideBarControl()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsSideBarControl), new FrameworkPropertyMetadata(typeof(PpsSideBarControl)));
		} // sctor
	} // class PpsSideBarControl

	#endregion

	#region -- class PpsSideBarGroup --------------------------------------------------

	/// <summary></summary>
	public class PpsSideBarGroup : PpsSideBarItemsBase
	{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		private static readonly DependencyPropertyKey isTopSelectedPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsTopSelected), typeof(bool), typeof(PpsSideBarGroup), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
		public static readonly DependencyProperty IsTopSelectedProperty = isTopSelectedPropertyKey.DependencyProperty;

		public static readonly DependencyProperty HeaderProperty = HeaderedContentControl.HeaderProperty.AddOwner(typeof(PpsSideBarGroup), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnHeaderChanged)));
		public static readonly DependencyProperty HeaderTemplateProperty = HeaderedContentControl.HeaderTemplateProperty.AddOwner(typeof(PpsSideBarGroup), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty HeaderTemplateSelectorProperty = HeaderedContentControl.HeaderTemplateSelectorProperty.AddOwner(typeof(PpsSideBarGroup), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty HeaderStringFormatProperty = HeaderedContentControl.HeaderStringFormatProperty.AddOwner(typeof(PpsSideBarGroup), new FrameworkPropertyMetadata(null));

		private static readonly DependencyPropertyKey hasHeaderPropertyKey = DependencyProperty.RegisterReadOnly(nameof(HasHeader), typeof(bool), typeof(PpsSideBarGroup), new FrameworkPropertyMetadata(BooleanBox.False));
		public static readonly DependencyProperty HasHeaderProperty = hasHeaderPropertyKey.DependencyProperty;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		static PpsSideBarGroup()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsSideBarGroup), new FrameworkPropertyMetadata(typeof(PpsSideBarGroup)));
			IsSelectedProperty.OverrideMetadata(typeof(PpsSideBarGroup), new FrameworkPropertyMetadata(BooleanBox.False, OnSelectedPropertyChanged));
		} // sctor

		private static void OnHeaderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var filter = (PpsSideBarGroup)d;
			filter.HasHeader = e.NewValue != null;
			filter.OnHeaderChanged(e.OldValue, e.NewValue);
		} // proc OnHeaderChanged

		/// <summary></summary>
		/// <param name="oldHeader"></param>
		/// <param name="newHeader"></param>
		protected virtual void OnHeaderChanged(object oldHeader, object newHeader)
		{
			RemoveLogicalChild(oldHeader);
			AddLogicalChild(newHeader);
		} // proc OnHeaderChanged

		private void RaiseSelectedContentChanged()
		{
			if (IsSelected)
				UpdateParentSelectedContent(this);
		} // proc RaiseSelectedContentChanged

		/// <summary></summary>
		/// <param name="newValue"></param>
		/// <param name="oldValue"></param>
		protected override void OnSelectedContentChanged(object newValue, object oldValue)
		{
			RaiseSelectedContentChanged();
			base.OnSelectedContentChanged(newValue, oldValue);
		} // proc OnSelectedContentChanged

		/// <summary></summary>
		/// <param name="newValue"></param>
		/// <param name="oldValue"></param>
		protected override void OnSelectedContentTemplateChanged(DataTemplate newValue, DataTemplate oldValue)
		{
			RaiseSelectedContentChanged();
			base.OnSelectedContentTemplateChanged(newValue, oldValue);
		} // proc OnSelectedContentTemplateChanged

		/// <summary></summary>
		/// <param name="newValue"></param>
		/// <param name="oldValue"></param>
		protected override void OnSelectedContentTemplateSelectorChanged(DataTemplateSelector newValue, DataTemplateSelector oldValue)
		{
			RaiseSelectedContentChanged();
			base.OnSelectedContentTemplateSelectorChanged(newValue, oldValue);
		} // proc OnSelectedContentTemplateSelectorChanged

		/// <summary></summary>
		/// <param name="newValue"></param>
		/// <param name="oldValue"></param>
		protected override void OnSelectedContentStringFormatChanged(string newValue, string oldValue)
		{
			RaiseSelectedContentChanged();
			base.OnSelectedContentStringFormatChanged(newValue, oldValue);
		} // proc OnSelectedContentStringFormatChanged

		/// <summary></summary>
		/// <param name="newValue"></param>
		/// <param name="oldValue"></param>
		protected override void OnSelectedFilterChanged(object newValue, object oldValue)
		{
			RaiseSelectedContentChanged();
			base.OnSelectedFilterChanged(newValue, oldValue);
		} // proc OnSelectedFilterChanged

		private static void OnSelectedPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var group = (PpsSideBarGroup)d;
			var value = BooleanBox.GetBool(e.NewValue);
			if (value)
				group.OnSelected(new RoutedEventArgs(SelectedEvent, d));
			else
			{
				group.UnselectAll();
				group.OnUnSelected(new RoutedEventArgs(UnselectedEvent, d));

				group.SetValue(isTopSelectedPropertyKey, BooleanBox.False);
			}
		} // proc OnSelectedPropertyChanged

		/// <summary></summary>
		/// <param name="e"></param>
		public virtual void OnSelected(RoutedEventArgs e)
			=> RaiseEvent(e);

		/// <summary></summary>
		/// <param name="e"></param>
		public virtual void OnUnSelected(RoutedEventArgs e)
			=> RaiseEvent(e);

		private bool HasSelectedItem()
		{
			foreach (var cur in Items)
			{
				var ctrl = ItemContainerGenerator.ContainerFromItem(cur);
				if (ctrl != null && GetIsSelected(ctrl))
					return true;
			}
			return false;
		} // func HasSelectedItem

		/// <summary></summary>
		/// <param name="e"></param>
		protected override void OnSelectionChanged(SelectionChangedEventArgs e)
		{
			base.OnSelectionChanged(e);
			if (HasSelectedItem())
				IsSelected = true;

			SetValue(isTopSelectedPropertyKey, BooleanBox.False);
		} // proc OnSelectionChanged

		internal bool SetSelected(bool setActive)
		{
			if (!PpsSideBarControl.CanChangeSetSelection(this, setActive))
				return false;
			
			// mark selected
			IsSelected = setActive;

			// unselect childs
			UnselectAll();
			UpdateSelectedContent();

			SetValue(isTopSelectedPropertyKey, BooleanBox.GetObject(setActive));
			return true;
		} // proc SetSelected

		/// <summary></summary>
		/// <param name="e"></param>
		protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
		{
			if (e.Source == this)
			{
				Focus();
				if (SetSelected(!IsTopSelected))
				{
					Keyboard.ClearFocus();
					e.Handled = true;
				}
			}
			base.OnMouseLeftButtonDown(e);
		} // proc OnMouseLeftButtonDown

		/// <summary></summary>
		/// <param name="e"></param>
		protected override void OnKeyDown(KeyEventArgs e)
		{
			if (e.Source == this && IsKeyboardFocused && (e.Key == Key.Enter || e.Key == Key.Space) && SetSelected(!IsTopSelected))
				e.Handled = true;
			base.OnKeyDown(e);
		} // proc OnKeyDown


		/// <summary></summary>
		/// <param name="item"></param>
		/// <returns></returns>
		protected override bool IsItemItsOwnContainerOverride(object item)
			=> item is PpsSideBarPanel
			|| item is PpsSideBarPanelFilter;

		/// <summary></summary>
		protected override System.Collections.IEnumerator LogicalChildren
			=> HasHeader ? LogicalContentEnumerator.GetLogicalEnumerator(this, base.LogicalChildren, () => Header) : base.LogicalChildren;

		/// <summary>Is this item selected.</summary>
		public bool IsSelected { get => (bool)GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }
		/// <summary>Is this element the current item.</summary>
		public bool IsTopSelected => BooleanBox.GetBool(GetValue(IsTopSelectedProperty));
		/// <summary>Title in sidebar control</summary>
		public object Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }
		/// <summary></summary>
		public DataTemplate HeaderTemplate { get => (DataTemplate)GetValue(HeaderTemplateProperty); set => SetValue(HeaderTemplateProperty, value); }
		/// <summary></summary>
		public DataTemplateSelector HeaderTemplateSelector { get => (DataTemplateSelector)GetValue(HeaderTemplateSelectorProperty); set => SetValue(HeaderTemplateSelectorProperty, value); }
		/// <summary></summary>
		public string HeaderStringFormat { get => (string)GetValue(HeaderStringFormatProperty); set => SetValue(HeaderStringFormatProperty, value); }
		/// <summary></summary>
		public bool HasHeader { get => BooleanBox.GetBool(GetValue(HasHeaderProperty)); set => SetValue(hasHeaderPropertyKey, BooleanBox.GetObject(value)); }
		/// <summary>IndentationLevel</summary>
		public int IndentationLevel => PpsSideBarControl.GetIndentLevel(this);
	} // class PpsSideBarGroup

	#endregion

	#region -- class PpsSideBarPanel --------------------------------------------------

	/// <summary>Content panel owner.</summary>
	public class PpsSideBarPanel : HeaderedContentControl
	{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty IsSelectedProperty = Selector.IsSelectedProperty.AddOwner(typeof(PpsSideBarPanel), new FrameworkPropertyMetadata(BooleanBox.False, OnSelectedPropertyChanged));
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		/// <summary></summary>
		/// <param name="oldContent"></param>
		/// <param name="newContent"></param>
		protected override void OnContentChanged(object oldContent, object newContent)
		{
			UpdateParentContent();
			base.OnContentChanged(oldContent, newContent);
		} // proc OnContentChanged

		/// <summary></summary>
		/// <param name="oldContentStringFormat"></param>
		/// <param name="newContentStringFormat"></param>
		protected override void OnContentStringFormatChanged(string oldContentStringFormat, string newContentStringFormat)
		{
			UpdateParentContent();
			base.OnContentStringFormatChanged(oldContentStringFormat, newContentStringFormat);
		} // proc OnContentStringFormatChanged

		/// <summary></summary>
		/// <param name="oldContentTemplate"></param>
		/// <param name="newContentTemplate"></param>
		protected override void OnContentTemplateChanged(DataTemplate oldContentTemplate, DataTemplate newContentTemplate)
		{
			UpdateParentContent();
			base.OnContentTemplateChanged(oldContentTemplate, newContentTemplate);
		} // proc OnContentTemplateChanged

		/// <summary></summary>
		/// <param name="oldContentTemplateSelector"></param>
		/// <param name="newContentTemplateSelector"></param>
		protected override void OnContentTemplateSelectorChanged(DataTemplateSelector oldContentTemplateSelector, DataTemplateSelector newContentTemplateSelector)
		{
			UpdateParentContent();
			base.OnContentTemplateSelectorChanged(oldContentTemplateSelector, newContentTemplateSelector);
		} // proc OnContentTemplateSelectorChanged

		private void UpdateParentContent()
		{
			if (IsSelected)
				PpsSideBarItemsBase.UpdateParentSelectedContent(this);
		} // proc UpdateParentContent

		private static void OnSelectedPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var panel = (PpsSideBarPanel)d;
			var value = BooleanBox.GetBool(e.NewValue);
			if (value)
				panel.OnSelected(new RoutedEventArgs(Selector.SelectedEvent, d));
			else
				panel.OnUnSelected(new RoutedEventArgs(Selector.UnselectedEvent, d));
		} // proc OnSelectedPropertyChanged

		/// <summary></summary>
		/// <param name="e"></param>
		public virtual void OnSelected(RoutedEventArgs e)
			=> RaiseEvent(e);

		/// <summary></summary>
		/// <param name="e"></param>
		public virtual void OnUnSelected(RoutedEventArgs e)
			=> RaiseEvent(e);

		private bool SetSelected(bool setActive)
		{
			if (!PpsSideBarControl.CanChangeSetSelection(this, setActive))
				return false;

			IsSelected = setActive; // mark selected
			return true;
		} // proc SetSelected

		/// <summary></summary>
		/// <param name="e"></param>
		protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
		{
			if (e.Source == this)
			{
				Focus();
				if (SetSelected(!IsSelected))
				{
					Keyboard.ClearFocus();
					e.Handled = true;
				}
			}
			base.OnMouseLeftButtonDown(e);
		} // proc OnMouseLeftButtonDown

		/// <summary></summary>
		/// <param name="e"></param>
		protected override void OnKeyDown(KeyEventArgs e)
		{
			if (e.Source == this && IsKeyboardFocused && (e.Key == Key.Enter || e.Key == Key.Space) && SetSelected(!IsSelected))
				e.Handled = true;
			base.OnKeyDown(e);
		} // proc OnKeyDown

		/// <summary>Is the current panel selected.</summary>
		public bool IsSelected { get => BooleanBox.GetBool(GetValue(IsSelectedProperty)); set => SetValue(IsSelectedProperty, BooleanBox.GetObject(value)); }

		/// <summary>IndentationLevel</summary>
		public int IndentationLevel => PpsSideBarControl.GetIndentLevel(this);

		static PpsSideBarPanel()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsSideBarPanel), new FrameworkPropertyMetadata(typeof(PpsSideBarPanel)));
		}
	} // class PpsSideBarPanel

	#endregion

	#region -- class PpsSideBarPanelFilter --------------------------------------------

	/// <summary>Headered filter control.</summary>
	public class PpsSideBarPanelFilter : Control
	{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty IsSelectedProperty = Selector.IsSelectedProperty.AddOwner(typeof(PpsSideBarPanelFilter), new FrameworkPropertyMetadata(BooleanBox.False, OnIsSelectedChanged));

		public static readonly DependencyProperty FilterProperty = DependencyProperty.Register(nameof(Filter), typeof(object), typeof(PpsSideBarPanelFilter), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnFilterChanged)));
		public static readonly DependencyProperty HeaderProperty = HeaderedContentControl.HeaderProperty.AddOwner(typeof(PpsSideBarPanelFilter), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnHeaderChanged)));
		public static readonly DependencyProperty HeaderTemplateProperty = HeaderedContentControl.HeaderTemplateProperty.AddOwner(typeof(PpsSideBarPanelFilter), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty HeaderTemplateSelectorProperty = HeaderedContentControl.HeaderTemplateSelectorProperty.AddOwner(typeof(PpsSideBarPanelFilter), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty HeaderStringFormatProperty = HeaderedContentControl.HeaderStringFormatProperty.AddOwner(typeof(PpsSideBarPanelFilter), new FrameworkPropertyMetadata(null));

		private static readonly DependencyPropertyKey hasHeaderPropertyKey = DependencyProperty.RegisterReadOnly(nameof(HasHeader), typeof(bool), typeof(PpsSideBarPanelFilter), new FrameworkPropertyMetadata(BooleanBox.False));
		public static readonly DependencyProperty HasHeaderProperty = hasHeaderPropertyKey.DependencyProperty;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		private static void OnIsSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var filter = (PpsSideBarPanelFilter)d;
			var value = BooleanBox.GetBool(e.NewValue);
			if (value)
				filter.OnSelected(new RoutedEventArgs(Selector.SelectedEvent, d));
			else
				filter.OnUnSelected(new RoutedEventArgs(Selector.UnselectedEvent, d));
		} // proc OnIsSelectedChanged

		private static void OnFilterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var filter = (PpsSideBarPanelFilter)d;
			if (filter.IsSelected)
				PpsSideBarItemsBase.UpdateParentSelectedContent(d);
		} // proc OnFilterChanged

		private static void OnHeaderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var filter = (PpsSideBarPanelFilter)d;
			filter.HasHeader = e.NewValue != null;
			filter.OnHeaderChanged(e.OldValue, e.NewValue);
		} // proc OnHeaderChanged

		/// <summary>Filter is unselected.</summary>
		/// <param name="e"></param>
		public virtual void OnSelected(RoutedEventArgs e)
			=> RaiseEvent(e);

		/// <summary>Filter is selected.</summary>
		/// <param name="e"></param>
		public virtual void OnUnSelected(RoutedEventArgs e)
			=> RaiseEvent(e);

		/// <summary></summary>
		/// <param name="oldHeader"></param>
		/// <param name="newHeader"></param>
		protected virtual void OnHeaderChanged(object oldHeader, object newHeader)
		{
			RemoveLogicalChild(oldHeader);
			AddLogicalChild(newHeader);
		} // proc OnHeaderChanged

		private bool SetSelected(bool setActive)
		{
			if (!PpsSideBarControl.CanChangeSetSelection(this, setActive))
				return false;

			IsSelected = setActive; // mark selected
			return true;
		} // proc SetSelected

		/// <summary></summary>
		/// <param name="e"></param>
		protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
		{
			if (e.Source == this)
			{
				Focus();
				if (SetSelected(!IsSelected))
				{
					Keyboard.ClearFocus();
					e.Handled = true;
				}
			}
			base.OnMouseLeftButtonDown(e);
		} // proc OnMouseLeftButtonDown

		/// <summary></summary>
		/// <param name="e"></param>
		protected override void OnKeyDown(KeyEventArgs e)
		{
			if (e.Source == this && IsKeyboardFocused && (e.Key == Key.Enter || e.Key == Key.Space) && SetSelected(!IsSelected))
				e.Handled = true;
			base.OnKeyDown(e);
		} // proc OnKeyDown

		/// <summary></summary>
		protected override System.Collections.IEnumerator LogicalChildren
			=> LogicalContentEnumerator.GetLogicalEnumerator(this, base.LogicalChildren, () => Header);

		/// <summary>Is the current filter selected</summary>
		public bool IsSelected { get => BooleanBox.GetBool(GetValue(IsSelectedProperty)); set => SetValue(IsSelectedProperty, BooleanBox.GetObject(value)); }
		/// <summary>Header of the filter.</summary>
		public object Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }
		/// <summary></summary>
		public DataTemplate HeaderTemplate { get => (DataTemplate)GetValue(HeaderTemplateProperty); set => SetValue(HeaderTemplateProperty, value); }
		/// <summary></summary>
		public DataTemplateSelector HeaderTemplateSelector { get => (DataTemplateSelector)GetValue(HeaderTemplateSelectorProperty); set => SetValue(HeaderTemplateSelectorProperty, value); }
		/// <summary></summary>
		public string HeaderStringFormat { get => (string)GetValue(HeaderStringFormatProperty); set => SetValue(HeaderStringFormatProperty, value); }
		/// <summary></summary>
		public bool HasHeader { get => BooleanBox.GetBool(GetValue(HasHeaderProperty)); set => SetValue(hasHeaderPropertyKey, BooleanBox.GetObject(value)); }
		/// <summary>Filter object.</summary>
		public object Filter { get => GetValue(FilterProperty); set => SetValue(FilterProperty, value); }
		/// <summary>IndentationLevel</summary>
		public int IndentationLevel => PpsSideBarControl.GetIndentLevel(this);

		static PpsSideBarPanelFilter()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsSideBarPanelFilter), new FrameworkPropertyMetadata(typeof(PpsSideBarPanelFilter)));
		}
	} // class PpsSideBarPanelFilter

	#endregion
}
