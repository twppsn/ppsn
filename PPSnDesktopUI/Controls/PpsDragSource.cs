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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace TecWare.PPSn.UI
{
	#region -- class PpsDragSource ----------------------------------------------------

	/// <summary>Implements drag and drop helper for all controls</summary>
	public abstract class PpsDragSource
	{
		private readonly UIElement element;

		private bool isCleaned = false;
		private object currentDraggedItem = null;
		private Rect currentDragStartArea;

		protected PpsDragSource(UIElement element)
		{
			this.element = element ?? throw new ArgumentNullException(nameof(element));

			PpsDragDropBehaviour.SetDragSource(element, this);

			element.PreviewMouseLeftButtonDown += Element_PreviewMouseLeftButtonDown;
			element.PreviewMouseLeftButtonUp += Element_PreviewMouseLeftButtonUp;
			element.MouseMove += Element_MouseMove;
		} // ctor

		public void CleanUp()
		{
			if (isCleaned)
				return;
			isCleaned = true;

			PpsDragDropBehaviour.SetDragSource(element, null);

			element.PreviewMouseLeftButtonDown -= Element_PreviewMouseLeftButtonDown;
			element.PreviewMouseLeftButtonUp -= Element_PreviewMouseLeftButtonUp;
		} // proc CleanUp

		protected virtual object GetDragItem(object sender, MouseEventArgs e)
			=> null;

		protected virtual bool TryGetDragItem(object item, out object dragItem, out DragDropEffects allowedEffects)
		{
			dragItem = item;
			allowedEffects = DragDropEffects.All;
			return dragItem != null;
		} // func TryGetDragItem

		protected virtual void OnStartDragDropOperation(object item, MouseEventArgs e)
		{
			if (TryGetDragItem(item, out var dragItem, out var allowedEffects))
				DragDrop.DoDragDrop(element, dragItem, allowedEffects);
		} // proc OnStartDragDropOperation

		private void Element_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			if (currentDraggedItem != null)
				return;

			var pt = e.GetPosition(element);
			var sz = element.RenderSize;
			if (pt.X < 0 || pt.Y < 0 || pt.X > sz.Width || pt.Y > sz.Height)
				return;

			currentDraggedItem = GetDragItem(sender, e);
			var dx = SystemParameters.MinimumHorizontalDragDistance;
			var dy = SystemParameters.MinimumVerticalDragDistance;
			currentDragStartArea = new Rect(pt.X - dx, pt.Y - dy, dx * 2, dy * 2);
		} // event Element_PreviewMouseLeftButtonDown

		private void Element_MouseMove(object sender, MouseEventArgs e)
		{
			if (currentDraggedItem == null)
				return;
			if (e.LeftButton == MouseButtonState.Released)
			{
				currentDraggedItem = null;
				return;
			}

			var pt = e.GetPosition(element);
			if (!currentDragStartArea.Contains(pt))
			{
				OnStartDragDropOperation(currentDraggedItem, e);
				currentDraggedItem = null;
			}
		} // event Element_MouseMove

		private void Element_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			currentDraggedItem = null;
		} // event Element_PreviewMouseLeftButtonUp

		public UIElement Element => element;
	} // class PpsDragSource

	#endregion

	#region -- enum PpsDragEventType --------------------------------------------------

	public enum PpsDragEventType
	{
		Enter,
		Move,
		Leave,
		Drop
	} // enum PpsDragEventType

	#endregion

	#region -- class PpsDropTarget ----------------------------------------------------

	public delegate void PpsDragEventDelegate(PpsDragEventType eventType, DragEventArgs e);

	public abstract class PpsDropTarget
	{
		private readonly UIElement element;

		protected PpsDropTarget(UIElement element)
		{
			this.element = element ?? throw new ArgumentNullException(nameof(element));

			// activate drop
			element.AllowDrop = true;

			element.DragEnter += Element_DragEnter;
			element.DragOver += Element_DragOver;
			element.DragLeave += Element_DragLeave;
			element.Drop += Element_Drop;
		} // ctor

		public void CleanUp()
		{
			element.AllowDrop = false;
			element.DragEnter -= Element_DragEnter;
			element.DragOver -= Element_DragOver;
			element.DragLeave -= Element_DragLeave;
			element.Drop -= Element_Drop;
		} // proc CleanUp

		protected abstract void OnDragEvent(PpsDragEventType eventType, DragEventArgs e);

		private void Element_DragEnter(object sender, DragEventArgs e)
		{
			OnDragEvent(PpsDragEventType.Enter, e);
			e.Handled = true;
		} // event Element_DragEnter

		private void Element_DragOver(object sender, DragEventArgs e)
		{
			OnDragEvent(PpsDragEventType.Move, e);
			e.Handled = true;
		} // event Element_DragOver

		private void Element_DragLeave(object sender, DragEventArgs e)
		{
			OnDragEvent(PpsDragEventType.Leave, e);
			e.Handled = true;
		} // event Element_DragLeave

		private void Element_Drop(object sender, DragEventArgs e)
		{
			OnDragEvent(PpsDragEventType.Drop, e);
			e.Handled = true;
		} // event Element_Drop

		public UIElement Element => element;

		#region -- class PpsGenericDropTarget -----------------------------------------

		private sealed class PpsGenericDropTarget : PpsDropTarget
		{
			private PpsDragEventDelegate dragEventHandler;

			public PpsGenericDropTarget(UIElement element, PpsDragEventDelegate dragEventHandler)
				: base(element)
			{
				this.dragEventHandler = dragEventHandler ?? throw new ArgumentNullException(nameof(dragEventHandler));
			}

			protected override void OnDragEvent(PpsDragEventType eventType, DragEventArgs e)
				=> dragEventHandler(eventType, e);
		} // class PpsGenericDropTarget

		#endregion

		public static PpsDropTarget Create(UIElement element, PpsDragEventDelegate dragEventHandler)
			=> new PpsGenericDropTarget(element, dragEventHandler);
	} // class PpsDropTarget

	#endregion

	#region -- class PpsDragDropBehaviour ---------------------------------------------

	/// <summary>Start drag and drop on every control.</summary>
	public static class PpsDragDropBehaviour
	{
		#region -- class ItemsControlDragDrop -----------------------------------------

		private sealed class ItemsControlDragDrop : PpsDragSource
		{
			public ItemsControlDragDrop(ItemsControl element) 
				: base(element)
			{
			} // ctor

			protected override object GetDragItem(object sender, MouseEventArgs e)
			{
				var itemsControl = ItemsControl;
				if (itemsControl.InputHitTest(e.GetPosition(itemsControl)) is UIElement item)
				{
					while (item != null)
					{
						if (item == Element || item is ButtonBase) // no drag and drop for button
							return null;
						else if (item is ListBoxItem || item is ContentControl)
							return item;

						item = item.GetVisualParent() as UIElement;
					}
					return null;
				}
				else
					return null;
			} // func GetDragItem

			protected override bool TryGetDragItem(object item, out object dragItem, out DragDropEffects allowedEffects)
			{
				dragItem = ItemsControl.ItemContainerGenerator.ItemFromContainer((DependencyObject)item);
				allowedEffects = DragDropEffects.All;
				return dragItem != null;
			} // func TryGetDragItem

			public ItemsControl ItemsControl => (ItemsControl)Element;
		} // class ItemsControlDragDrop

		#endregion

		public static readonly DependencyProperty AllowDragProperty = DependencyProperty.RegisterAttached("AllowDrag", typeof(bool), typeof(PpsDragDropBehaviour), new FrameworkPropertyMetadata(BooleanBox.False, new PropertyChangedCallback(OnAllowDragChanged)));

		private static readonly DependencyPropertyKey dragSourcePropertyKey = DependencyProperty.RegisterAttachedReadOnly("DragSource", typeof(PpsDragSource), typeof(PpsDragDropBehaviour), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnDragControllerChanged)));
		public static readonly DependencyProperty DragSourceProperty = dragSourcePropertyKey.DependencyProperty;

		private static PpsDragSource CreateDragController(UIElement element)
		{
			if (element is ItemsControl ic)
				return new ItemsControlDragDrop(ic);
			else
				return null;
		} // func CreateDragController

		private static void OnDragControllerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			if (e.OldValue is PpsDragSource dragController)
				dragController.CleanUp();
		} // proc OnDragControllerChanged

		private static void OnAllowDragChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			if (BooleanBox.GetBool(e.NewValue) && d is UIElement element)
				CreateDragController(element);
			else
				d.SetValue(dragSourcePropertyKey, null);
		} // proc OnAllowDragChanged

		public static bool GetAllowDrag(DependencyObject d)
			=> BooleanBox.GetBool(d.GetValue(AllowDragProperty));

		public static PpsDragSource GetDragSource(DependencyObject d)
			=> (PpsDragSource)d.GetValue(DragSourceProperty);

		internal static void SetDragSource(DependencyObject d, PpsDragSource dragSource)
			=> d.SetValue(dragSourcePropertyKey, dragSource);

		public static void SetAllowDrag(DependencyObject d, bool value)
			=> d.SetValue(AllowDragProperty, BooleanBox.GetObject(value));
	} // class PpsDragDropBehaviour

	#endregion
}
