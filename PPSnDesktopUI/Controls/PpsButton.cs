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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using TecWare.PPSn.UI;

namespace TecWare.PPSn.Controls
{
	#region -- enum PpsButtonDisplayType ----------------------------------------------

	/// <summary></summary>
	public enum PpsButtonDisplayType
	{
		/// <summary>Show as filled Rectangle with content and optional image.</summary>
		Standard,
		/// <summary>Show with transparent Background.</summary>
		Transparent,
		/// <summary></summary>
		Circled
	} // enum PpsButtonDisplayType

	#endregion

	#region -- class PpsButton --------------------------------------------------------

	/// <summary></summary>
	public class PpsButton : Button
	{
		#region -- TouchEvents --------------------------------------------------------

		protected override void OnTouchDown(TouchEventArgs e)
		{
			if (e.TouchDevice.Capture(this))
			{
				IsPressed = true;
				e.Handled = true;
			}
			base.OnTouchDown(e);
		} // proc OnTouchDown

		protected override void OnTouchMove(TouchEventArgs e)
		{
			if (e.TouchDevice.Captured == this)
			{
				IsPressed = IsHitTest(e.GetTouchPoint(this).Position);
				e.Handled = true;
			}
			base.OnTouchMove(e);
		} // proc OnTouchMove

		protected override void OnTouchUp(TouchEventArgs e)
		{
			if (e.TouchDevice.Captured == this)
			{
				ReleaseTouchCapture(e.TouchDevice);
				IsPressed = false;

				if (IsHitTest(e.GetTouchPoint(this).Position))
					RaiseOnClick();

				e.Handled = true;
			}
			base.OnTouchUp(e);
		} // proc OnTouchUp

		private async void RaiseOnClick()
		{
			// Zeit für die Button-Animation
			await System.Threading.Tasks.Task.Delay(300);
			OnClick();
		} // proc RaiseOnClick

		private bool IsHitTest(Point point)
		{
			var hitTestResult = VisualTreeHelper.HitTest(this, point);
			return hitTestResult != null && hitTestResult.VisualHit != null;
		} // func IsHitTest

		#endregion

		#region -- DisplayMode - Property ---------------------------------------------

		/// <summary>The type of representation</summary>
		public static readonly DependencyProperty DisplayModeProperty = DependencyProperty.Register(nameof(DisplayMode), typeof(PpsButtonDisplayType), typeof(PpsButton), new FrameworkPropertyMetadata(PpsButtonDisplayType.Standard, new PropertyChangedCallback(DisplayModeChanged)));

		/// <summary>The property defines the type of presentation</summary>
		public PpsButtonDisplayType DisplayMode { get => (PpsButtonDisplayType)GetValue(DisplayModeProperty); set => SetValue(DisplayModeProperty, value); }

		private static void DisplayModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var newValue = (PpsButtonDisplayType)e.NewValue;
			d.SetValue(isTransparentPropertyKey, BooleanBox.GetObject(newValue == PpsButtonDisplayType.Transparent));
			d.SetValue(borderRadiusPropertyKey, newValue == PpsButtonDisplayType.Circled ? ((PpsButton)d).Width / 2.00 : 0.00);
		}

		#endregion

		#region -- GeometryName - Property --------------------------------------------

		/// <summary>The name of the resource</summary>
		public static readonly DependencyProperty GeometryNameProperty = PpsGeometryImage.GeometryNameProperty.AddOwner(typeof(PpsButton));

		/// <summary>The property defines the resource to be loaded.</summary>
		public string GeometryName { get => (string)GetValue(GeometryNameProperty); set => SetValue(GeometryNameProperty, value); }

		#endregion

		#region -- GeometrySize - Property --------------------------------------------

		/// <summary>The width and height of the image</summary>
		public static readonly DependencyProperty GeometrySizeProperty = DependencyProperty.RegisterAttached(nameof(GeometrySize), typeof(double), typeof(PpsButton), new FrameworkPropertyMetadata(24.0, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

		/// <summary>The property defines the width and height of the image</summary>
		public double GeometrySize { get => (double)GetValue(GeometrySizeProperty); set => SetValue(GeometrySizeProperty, value); }

		/// <summary>The width and height of the image</summary>
		//public static readonly DependencyProperty GeometrySizeProperty = DependencyProperty.RegisterAttached(
		//	"GeometrySize",
		//	typeof(double),
		//	typeof(PpsButton),
		//	new FrameworkPropertyMetadata(8.0, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure)
		//);
		//public static void SetGeometrySize(DependencyObject element, double value)
		//	=> element.SetValue(GeometrySizeProperty, value);
		//public static double GetGeometrySize(DependencyObject element)
		//	=> (double)element.GetValue(GeometrySizeProperty);

		#endregion

		#region -- ImageOpacity - Property --------------------------------------------

		/// <summary>The Opacity of the image</summary>
		public static readonly DependencyProperty ImageOpacityProperty = DependencyProperty.Register(nameof(ImageOpacity), typeof(double), typeof(PpsButton), new FrameworkPropertyMetadata(0.65));

		/// <summary>The property defines the Opacity of the image</summary>
		public double ImageOpacity { get => (double)GetValue(ImageOpacityProperty); set => SetValue(ImageOpacityProperty, value); }

		#endregion

		#region -- IsTransparent - Property -------------------------------------------

		private static readonly DependencyPropertyKey isTransparentPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsTransparent), typeof(bool), typeof(PpsButton), new FrameworkPropertyMetadata(BooleanBox.False));
		/// <summary>The property defines if element will be displayed like button</summary>
		public static readonly DependencyProperty IsTransparentProperty = isTransparentPropertyKey.DependencyProperty;

		/// <summary>Display retangular Background</summary>
		public bool IsTransparent => BooleanBox.GetBool(GetValue(IsTransparentProperty));

		#endregion

		#region -- BorderRadius - Property --------------------------------------------

		private static readonly DependencyPropertyKey borderRadiusPropertyKey = DependencyProperty.RegisterReadOnly(nameof(BorderRadius), typeof(double), typeof(PpsButton), new FrameworkPropertyMetadata(0.00));
		/// <summary></summary>
		public static readonly DependencyProperty BorderRadiusProperty = borderRadiusPropertyKey.DependencyProperty;
		/// <summary></summary>
		public double BorderRadius => (double)GetValue(BorderRadiusProperty);

		#endregion

		static PpsButton()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsButton), new FrameworkPropertyMetadata(typeof(PpsButton)));
		} // sctor
	} // class PpsButton

	#endregion

	#region -- enum PpsSplitButtonMode ------------------------------------------------

	/// <summary></summary>
	public enum PpsSplitButtonType
	{
		/// <summary>Only a drop down button.</summary>
		Dropdown,
		/// <summary>Drop and command button.</summary>
		SplitButton
	} // enum SplitButtonType

	#endregion

	#region -- class PpsSplitButton ---------------------------------------------------

	/// <summary></summary>
	[TemplatePart(Name = "PART_SplitButton", Type = typeof(ButtonBase))]
	public class PpsSplitButton : PpsButton
	{
		/// <summary></summary>
		protected const string PartSplitButton = "PART_SplitButton";

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty ModeProperty = DependencyProperty.Register(nameof(Mode), typeof(PpsSplitButtonType), typeof(PpsSplitButton), new FrameworkPropertyMetadata(PpsSplitButtonType.SplitButton));
		public static readonly DependencyProperty PopupProperty = DependencyProperty.Register(nameof(Popup), typeof(Popup), typeof(PpsSplitButton), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnPopupChanged)));
		public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(PpsSplitButton), new FrameworkPropertyMetadata(false, OnIsOpenChanged));
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		public PpsSplitButton()
		{
		} // ctor

		/// <summary></summary>
		public override void OnApplyTemplate()
		{
			base.OnApplyTemplate();

			if (GetTemplateChild(PartSplitButton) is ButtonBase button)
				button.Click += (sender, e) => OnDropdown();
		} // proc OnApplyTemplate

		#endregion

		#region -- OnDropDown ---------------------------------------------------------

		private void OnPopupClosed(object sender, EventArgs e)
		{
			var popup = (Popup)sender;
			popup.Closed -= OnPopupClosed;
			if (popup == Popup)
				IsOpen = false;
		} // proc OnPopupClosed

		private void OnContextMenuClosed(object sender, RoutedEventArgs e)
		{
			var ctx = (ContextMenu)sender;
			ctx.Closed -= OnContextMenuClosed;
			if (ctx == ContextMenu)
				IsOpen = false;
		} // proc OnContextMenuClosed

		private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsSplitButton)d).OnIsOpenChanged((bool)e.NewValue, (bool)e.OldValue);

		/// <summary></summary>
		/// <param name="newValue"></param>
		/// <param name="oldValue"></param>
		protected virtual void OnIsOpenChanged(bool newValue, bool oldValue)
		{
			if (Popup != null)
			{
				if (newValue)
					Popup.Closed += OnPopupClosed;
				Popup.IsOpen = newValue;
			}
			else if (ContextMenu != null)
			{
				if (newValue)
					ContextMenu.Closed += OnContextMenuClosed;
				ContextMenu.IsOpen = newValue;
			}
		} // OnIsOpenChanged

		/// <summary>Route to DropDown button</summary>
		protected override void OnClick()
		{
			if (Mode == PpsSplitButtonType.Dropdown)
				OnDropdown();
			else
				base.OnClick();
		} // proc OnClick

		/// <summary>Activate Dropdown</summary>
		protected virtual void OnDropdown()
		{
			if (IsOpen)
				IsOpen = false;
			else if (Popup != null)
			{
				Popup.PlacementTarget = this;
				IsOpen = true;
			}
			else if (ContextMenu != null)
			{
				ContextMenu.PlacementTarget = this;
				IsOpen = true;
			}
		} // proc OnDropdown

		#endregion

		private static void OnPopupChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsSplitButton)d).OnPopupChanged((Popup)e.NewValue, (Popup)e.OldValue);

		private void OnPopupChanged(Popup newValue, Popup oldValue)
		{
			RemoveLogicalChild(oldValue);
			if (newValue?.Parent == null)
				AddLogicalChild(newValue);
		} // proc OnPopupChanged

		/// <summary></summary>
		protected override IEnumerator LogicalChildren
			=> Popup == null || Popup.Parent != this
				? base.LogicalChildren
				: PpsLogicalContentEnumerator.GetLogicalEnumerator(this, base.LogicalChildren, () => Popup);

		/// <summary></summary>
		public bool IsOpen { get { return (bool)GetValue(IsOpenProperty); } set { SetValue(IsOpenProperty, value); } }
		/// <summary></summary>
		public Popup Popup { get { return (Popup)GetValue(PopupProperty); } set { SetValue(PopupProperty, value); } }
		/// <summary></summary>
		public PpsSplitButtonType Mode { get { return (PpsSplitButtonType)GetValue(ModeProperty); } set { SetValue(ModeProperty, value); } }

		// -- Static --------------------------------------------------------------

		static PpsSplitButton()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsSplitButton), new FrameworkPropertyMetadata(typeof(PpsSplitButton)));
		} // ctor
	} // class SplitButton

	#endregion

	#region -- class PpsToggleButton --------------------------------------------------

	/// <summary></summary>
	public class PpsToggleButton : PpsButton
	{
		#region -- IsChecked - Property -----------------------------------------------

		/// <summary></summary>
		public static readonly DependencyProperty IsCheckedProperty = DependencyProperty.Register(nameof(IsChecked), typeof(bool), typeof(PpsToggleButton), new FrameworkPropertyMetadata(BooleanBox.False, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

		/// <summary></summary>
		public bool IsChecked { get => BooleanBox.GetBool(GetValue(IsCheckedProperty)); set => SetValue(IsCheckedProperty, BooleanBox.GetObject(value)); }

		#endregion

		protected override void OnClick()
		{
			OnToggle();
			base.OnClick();
		} // proc OnClick

		protected internal virtual void OnToggle()
		{
			var isChecked = IsChecked;
			if (isChecked)
				SetCurrentValue(IsCheckedProperty, BooleanBox.GetObject(false));
			else // false or null
				SetCurrentValue(IsCheckedProperty, BooleanBox.GetObject(true));
		} // proc OnToggle

		static PpsToggleButton()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsToggleButton), new FrameworkPropertyMetadata(typeof(PpsToggleButton)));
		} // ctor static
	} // class PpsToggleButton

	#endregion

	#region -- class PpsCheckBox ------------------------------------------------------

	/// <summary></summary>
	public class PpsCheckBox : CheckBox
	{
		static PpsCheckBox()
		{
			//DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsCheckBox), new FrameworkPropertyMetadata(typeof(PpsCheckBox)));
		}
	} // class PpsCheckBox

	#endregion
}
