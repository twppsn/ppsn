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
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using TecWare.DE.Stuff;
using TecWare.PPSn.UI;

namespace TecWare.PPSn.Controls
{
	#region -- class PpsPopup ---------------------------------------------------------

	/// <summary></summary>
	public class PpsPopup : Popup
	{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty PreserveFocusProperty = DependencyProperty.Register(nameof(PreserveFocus), typeof(bool), typeof(PpsPopup), new FrameworkPropertyMetadata(BooleanBox.False));
		public static readonly DependencyProperty RouteEventsProperty = DependencyProperty.Register(nameof(RouteEvents), typeof(bool), typeof(PpsPopup), new FrameworkPropertyMetadata(BooleanBox.False));
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		/// <summary></summary>
		public event CancelEventHandler Opening;
		/// <summary></summary>
		public event CancelEventHandler Closing;

		/// <summary></summary>
		public PpsPopup()
		{
			CommandBindings.Add(
				new CommandBinding(PpsControlCommands.ClosePopupCommand,
					(sender, e) =>
					{
						((Popup)sender).IsOpen = false;
					}
				)
			);
		} // ctor

		/// <summary></summary>
		/// <returns></returns>
		protected override DependencyObject GetUIParentCore()
		{
			return RouteEvents
				? base.GetUIParentCore()
				: null; // do not use PlacementTarget or Logical Tree for event routing
		} // func GetUIParentCore

		private static object OnIsOpenCoerceValue(DependencyObject d, object baseValue)
			=> ((PpsPopup)d).OnIsCoerceValue((bool)baseValue);

		private bool OnIsCoerceValue(bool baseValue)
		{
			var ev = new CancelEventArgs(false);
			if (baseValue)
			{
				Opening?.Invoke(this, ev);
				return !ev.Cancel;
			}
			else
			{
				Closing?.Invoke(this, ev);
				return ev.Cancel;
			}
		} // proc OnIsCoerceValue

		private IInputElement focusedElement = null;

		/// <summary></summary>
		/// <param name="e"></param>
		protected override void OnOpened(EventArgs e)
		{
			focusedElement = PreserveFocus ? Keyboard.FocusedElement : null;

			if (Child != null && !Child.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)))
			{
				Child.Focusable = true;
				FocusManager.SetFocusedElement(this, Child);
				Keyboard.Focus(Child);
			}
			base.OnOpened(e);
		} // proc OnOpend

		/// <summary></summary>
		/// <param name="e"></param>
		protected override void OnClosed(EventArgs e)
		{
			if (focusedElement != null)
				Keyboard.Focus(focusedElement);
			base.OnClosed(e);
		} // proc OnClosed

		/// <summary></summary>
		public bool PreserveFocus { get => BooleanBox.GetBool(GetValue(PreserveFocusProperty)); set => SetValue(PreserveFocusProperty, BooleanBox.GetObject(value)); }
		/// <summary></summary>
		public bool RouteEvents { get => BooleanBox.GetBool(GetValue(RouteEventsProperty)); set => SetValue(RouteEventsProperty, BooleanBox.GetObject(value)); }

		static PpsPopup()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsPopup), new FrameworkPropertyMetadata(typeof(PpsPopup)));
			IsOpenProperty.OverrideMetadata(typeof(PpsPopup), new FrameworkPropertyMetadata(null, new CoerceValueCallback(OnIsOpenCoerceValue)));
		} // sctor
	} // class PpsPopup

	#endregion

	#region -- class PpsPopupContent --------------------------------------------------

	/// <summary>Popup panel layout.</summary>
	public class PpsPopupContent : HeaderedContentControl
	{
		/// <summary>The name of the resource</summary>
		public static readonly DependencyProperty GeometryNameProperty = PpsGeometryImage.GeometryNameProperty.AddOwner(typeof(PpsPopupContent));

		/// <summary>The property defines the resource to be loaded.</summary>
		public string GeometryName { get => (string)GetValue(GeometryNameProperty); set => SetValue(GeometryNameProperty, value); }

#if DEBUG
		/// <summary></summary>
		/// <param name="e"></param>
		protected override void OnKeyUp(KeyEventArgs e)
		{
			if (e.Key == Key.F9)
				PpsWpfShell.PrintLogicalTreeToConsole(Keyboard.FocusedElement as DependencyObject);
			else if (e.Key == Key.F8)
				PpsWpfShell.PrintVisualTreeToConsole(Keyboard.FocusedElement as DependencyObject);
			else if (e.Key == Key.F7)
				PpsWpfShell.PrintEventTreeToConsole(Keyboard.FocusedElement as DependencyObject);
			base.OnKeyUp(e);
		} // proc OnKeyUp
#endif

		static PpsPopupContent()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsPopupContent), new FrameworkPropertyMetadata(typeof(PpsPopupContent)));
		} // sctor
	} // class PpsPopupContent

	#endregion

	#region -- class PpsPopupBehavior ----------------------------------------------------

	/// <summary></summary>
	[Obsolete]
	public static class PpsPopupBehavior
	{
		public static bool GetForceFocus(UIElement element)
			=> (bool)element.GetValue(ForceFocusProperty);

		public static void SetForceFocus(UIElement element, bool value)
			=> element.SetValue(ForceFocusProperty, value);

		public static readonly DependencyProperty ForceFocusProperty =
			DependencyProperty.RegisterAttached("ForceFocus", typeof(bool), typeof(PpsPopupBehavior), new UIPropertyMetadata(false, OnForceFocusPropertyChanged));

		public static void OnForceFocusPropertyChanged(DependencyObject depObj, DependencyPropertyChangedEventArgs args)
		{
			if (depObj is Popup popup)
				popup.Opened += (sender, e) => OnOpened(popup);
		} // proc PropertyChangedCallback

		private static void OnOpened(Popup popup)
		{
			if (!popup.Child.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)))
			{
				popup.Child.Focusable = true;
				FocusManager.SetFocusedElement(popup, popup.Child);
				Keyboard.Focus(popup.Child);
			}
		} // proc OnOpened

	} // class PpsPopupBehavior

	#endregion
}