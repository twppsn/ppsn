using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using TecWare.PPSn.UI;

namespace TecWare.PPSn.Controls
{
	#region -- class PpsTabControl ----------------------------------------------------

	/// <summary></summary>
	[TemplatePart(Name = "PART_SelectionMarker", Type = typeof(Rectangle))]
	public class PpsTabControl : TabControl
	{
		#region -- ItemSpacing - Property ---------------------------------------------

		public static readonly DependencyProperty ItemSpacingProperty = DependencyProperty.RegisterAttached(nameof(ItemSpacing), typeof(double), typeof(PpsTabControl), new FrameworkPropertyMetadata(32.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));
		public double ItemSpacing { get => (double)GetValue(ItemSpacingProperty); set => SetValue(ItemSpacingProperty, value); }

		#endregion

		private Rectangle selectionMarker;

		/// <summary></summary>
		public override void OnApplyTemplate()
		{
			base.OnApplyTemplate();

			if (GetTemplateChild("PART_SelectionMarker") is Rectangle rc)
			{
				selectionMarker = rc;
				UpdateSelectionMarker(false);
			}
		} // proc OnApplyTemplate

		protected override void OnSelectionChanged(SelectionChangedEventArgs e)
		{
			base.OnSelectionChanged(e);
			UpdateSelectionMarker(true);
		} // proc OnSelectionChanged

		#region -- Mark selection -----------------------------------------------------------

		private bool TryGetTabItem(object value, out TabItem ti)
		{
			if (value is TabItem t)
			{
				ti = t;
				return true;
			}
			ti = ItemContainerGenerator.ContainerFromItem(value) as TabItem;
			return ti != null;
		} // func TryGetTabItem

		public void UpdateSelectionMarker(bool doAnimate)
		{
			// find tab
			if (selectionMarker == null || !TryGetTabItem(SelectedItem, out var tabItem))
				return;

			var currentWidth = selectionMarker.ActualWidth;
			var targetWidth = tabItem.ActualWidth - ItemSpacing;
			var currentMargin = selectionMarker.Margin;
			var targetMargin = new Thickness(tabItem.TransformToAncestor(this).Transform(new Point(0.0, 0.0)).X + ItemSpacing, currentMargin.Top, currentMargin.Right, currentMargin.Bottom);

			// position changed?
			if (currentWidth == targetWidth && currentMargin == targetMargin)
				return;

			if (targetWidth <= 0.001)
			{
				selectionMarker.Visibility = Visibility.Collapsed;
			}
			else
			{
				selectionMarker.Visibility = Visibility.Visible;

				if (doAnimate)
				{
					var duration = new Duration(TimeSpan.FromMilliseconds(150));
					var easingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut };

					selectionMarker.BeginAnimation(Shape.MarginProperty,
						new ThicknessAnimation
						{
							From = currentMargin,
							To = targetMargin,
							Duration = duration,
							FillBehavior = FillBehavior.HoldEnd,
							EasingFunction = easingFunction
						}
					);
					selectionMarker.BeginAnimation(Shape.WidthProperty,
						new DoubleAnimation
						{
							From = currentWidth,
							To = targetWidth,
							Duration = duration,
							FillBehavior = FillBehavior.HoldEnd,
							EasingFunction = easingFunction
						}
					);
				}
				else
				{
					selectionMarker.Margin = targetMargin;
					selectionMarker.Width = targetWidth;
				}
			}
		} // proc UpdateSelectionMarker

		#endregion

		protected override DependencyObject GetContainerForItemOverride()
			=> new PpsTabItem();

		protected override bool IsItemItsOwnContainerOverride(object item)
			=> item is PpsTabItem;

		static PpsTabControl()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsTabControl), new FrameworkPropertyMetadata(typeof(PpsTabControl)));
		} // sctor
	} // class PpsTabControl

	#endregion

	#region -- class PpsTabItem -------------------------------------------------------

	[TemplateVisualState(Name = "Pressed", GroupName = "PressedStates")]
	[TemplateVisualState(Name = "Unpressed", GroupName = "PressedStates")]
	[TemplatePart(Name = "PART_OuterBorder", Type = typeof(Border))]
	/// <summary></summary>
	public class PpsTabItem : TabItem
	{
		#region -- IsPressed - Property -----------------------------------------------

		private static readonly DependencyPropertyKey isPressedPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsPressed), typeof(bool), typeof(PpsTabItem), new FrameworkPropertyMetadata(BooleanBox.False, new PropertyChangedCallback(OnPressedStateChanged)));
		public static readonly DependencyProperty IsPressedProperty = isPressedPropertyKey.DependencyProperty;
		public bool IsPressed => BooleanBox.GetBool(GetValue(IsPressedProperty));

		#endregion

		#region -- GeometryName - Property --------------------------------------------

		public static readonly DependencyProperty GeometryNameProperty = PpsGeometryImage.GeometryNameProperty.AddOwner(typeof(PpsTabItem));
		public string GeometryName { get => (string)GetValue(GeometryNameProperty); set => SetValue(GeometryNameProperty, value); }

		#endregion

		private Border outerBorder;

		public PpsTabItem()
		{
			this.LayoutUpdated += PpsTabItem_LayoutUpdated;
		}

		private void PpsTabItem_LayoutUpdated(object sender, EventArgs e)
			=> this.GetVisualParent<PpsTabControl>()?.UpdateSelectionMarker(true);

		public override void OnApplyTemplate()
		{
			base.OnApplyTemplate();

			if (GetTemplateChild("PART_OuterBorder") is Border border)
				outerBorder = border;
		} // proc OnApplyTemplate

		#region -- PressedStateChanged ------------------------------------------------

		private static void OnPressedStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsTabItem)d).OnPressedStateChanged((bool)e.NewValue);

		private void OnPressedStateChanged(bool newValue)
			=> VisualStateManager.GoToState(this, newValue ? "Pressed" : "Unpressed", true);

		private void UpdatePressedState(bool isPressed)
			=> SetValue(isPressedPropertyKey, BooleanBox.GetObject(isPressed));

		#endregion

		private bool IsHitTest(Point point)
		{
			var hitTestResult = VisualTreeHelper.HitTest(outerBorder, point);
			return hitTestResult != null && hitTestResult.VisualHit != null;
		} // func IsHitTest

		#region -- MouseEvents --------------------------------------------------------

		// simuliere OnClick
		private void OnClick(MouseButtonEventArgs e)
			=> base.OnMouseLeftButtonDown(e);

		protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
		{
			if (e.OriginalSource == outerBorder)
			{
				Mouse.Capture(outerBorder, CaptureMode.SubTree);
				UpdatePressedState(true);
				// Handle on MouseLeftButtonUp
				return;
			}
			base.OnMouseLeftButtonDown(e);
		} // proc OnMouseLeftButtonDown

		protected override void OnMouseMove(MouseEventArgs e)
		{
			if (e.OriginalSource == outerBorder)
			{
				if (Mouse.Captured == outerBorder)
				{
					var isPressed = IsHitTest(e.GetPosition(outerBorder));
					UpdatePressedState(isPressed);
				}
			}
			base.OnMouseMove(e);
		}// proc OnMouseMove

		protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
		{
			if (e.OriginalSource == outerBorder)
			{
				Mouse.Capture(null);
				if (IsPressed)
				{
					UpdatePressedState(false);
					OnClick(e);
					e.Handled = true;
				}
			}
			base.OnMouseLeftButtonUp(e);
		} // proc OnMouseLeftButtonUp

		#endregion

		#region -- TouchEvents --------------------------------------------------------

		protected override void OnTouchDown(TouchEventArgs e)
		{
			if (e.OriginalSource == outerBorder)
			{
				if (e.TouchDevice.Capture(outerBorder))
					UpdatePressedState(true);
			}
			base.OnTouchDown(e);
		} // proc OnTouchDown

		protected override void OnTouchMove(TouchEventArgs e)
		{
			if (e.TouchDevice.Captured == outerBorder)
			{
				var isPressed = IsHitTest(e.GetTouchPoint(outerBorder).Position);
				UpdatePressedState(isPressed);
			}
			base.OnTouchMove(e);
		} // proc OnTouchMove

		protected override void OnTouchUp(TouchEventArgs e)
		{
			if (e.TouchDevice.Captured == outerBorder)
				ReleaseTouchCapture(e.TouchDevice);
			base.OnTouchUp(e);
		} // proc OnTouchUp

		#endregion

		static PpsTabItem()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsTabItem), new FrameworkPropertyMetadata(typeof(PpsTabItem)));
		} // sctor
	} // class PpsTabItem

	#endregion
}
