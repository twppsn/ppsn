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
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using TecWare.PPSn.UI;

namespace TecWare.PPSn.Controls
{
	/// <summary>Win32 overlay panel for progress.</summary>
	public sealed class PpsProgressOverlay : HwndHost
	{
		#region -- class ProgressOverlaySource ----------------------------------------

		private sealed class ProgressOverlaySource : HwndSource
		{
			private const int WS_VISIBLE = 0x10000000;
			private const int WS_CHILD = 0x40000000;

			private readonly ContentControl progressControl;

			public ProgressOverlaySource(IntPtr parentWindow)
				: base(new HwndSourceParameters
				{
					WindowClassStyle = 0,
					WindowStyle = WS_CHILD | WS_VISIBLE,
					ExtendedWindowStyle = 0,
					PositionX = 0,
					PositionY = 0,
					Width = 0,
					Height = 0,
					WindowName = "ProgressOverlay",
					ParentWindow = parentWindow,
					UsesPerPixelTransparency = true
				})
			{
				progressControl = new ContentControl();
			} // ctor

			public void SetProgress(DataTemplate progressTemplate, PpsProgressStack progressStack)
			{
				progressControl.ContentTemplate = progressTemplate ?? throw new ArgumentNullException(nameof(progressTemplate));
				progressControl.Content = progressStack ?? throw new ArgumentNullException(nameof(progressStack));

				RootVisual = progressControl;

				NativeMethods.SetWindowPos(Handle,
					IntPtr.Zero, // HWND_TOP
					0, 0, 0, 0,
					SetWindowPosFlag.NoActivate | SetWindowPosFlag.NoMove | SetWindowPosFlag.NoOwnerZOrder | SetWindowPosFlag.NoSize
				);
			} // proc SetProgress

			public void ClearProgress()
			{
				progressControl.Content = null;
				RootVisual = null;
			} // proc ClearProgress
		} // class ProgressOverlaySource

		#endregion

		private ProgressOverlaySource progressOverlaySource = null;

		#region -- BuildWindow --------------------------------------------------------

		/// <inherited/>
		protected override HandleRef BuildWindowCore(HandleRef hwndParent)
		{
			progressOverlaySource = new ProgressOverlaySource(hwndParent.Handle);
			UpdateProgressOverlay();
			return new HandleRef(progressOverlaySource, progressOverlaySource.Handle);
		} // func BuildWindowCore

		/// <inherited/>
		protected override void DestroyWindowCore(HandleRef hwnd)
		{
			progressOverlaySource.Dispose();
			progressOverlaySource = null;
		} // proc DestroyWindowCore

		#endregion

		#region -- ProgressTemplate ---------------------------------------------------

		/// <summary></summary>
		public static readonly DependencyProperty ProgressTemplateProperty = DependencyProperty.Register(nameof(ProgressTemplate), typeof(DataTemplate), typeof(PpsProgressOverlay), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnProgressTemplateChanged)));

		private static void OnProgressTemplateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsProgressOverlay)d).UpdateProgressOverlay();

		/// <summary>Template for the progress view.</summary>
		public DataTemplate ProgressTemplate { get => (DataTemplate)GetValue(ProgressTemplateProperty); set => SetValue(ProgressTemplateProperty, value); }

		#endregion

		#region -- ProgressStack - property -------------------------------------------

		/// <summary></summary>
		public static readonly DependencyProperty ProgressStackProperty = DependencyProperty.Register(nameof(ProgressStack), typeof(PpsProgressStack), typeof(PpsProgressOverlay), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnProgressStackChanged)));

		private static void OnProgressStackChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsProgressOverlay)d).OnProgressStackChanged((PpsProgressStack)e.NewValue, (PpsProgressStack)e.OldValue);

		private void OnProgressStackChanged(PpsProgressStack newValue, PpsProgressStack oldValue)
		{
			if (oldValue != null)
				oldValue.PropertyChanged -= ProgressStack_PropertyChanged;

			if (newValue != null)
				newValue.PropertyChanged += ProgressStack_PropertyChanged;

			UpdateProgressOverlay();
		} // proc OnProgressStackChanged

		private void ProgressStack_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(PpsProgressStack.IsActive))
				UpdateProgressOverlay();
		} // event ProgressStack_PropertyChanged;

		private void UpdateProgressOverlay()
		{
			var progressStack = ProgressStack;
			var progressTemplate = ProgressTemplate;

			if (progressStack != null && progressTemplate != null)
			{
				if (progressStack.IsActive)
					ShowProgressOverlay(progressStack, progressTemplate);
				else
					HideProgressOverlay();
			}
			else
				HideProgressOverlay();
		} // proc UpdateProgressOverlay

		private void ShowProgressOverlay(PpsProgressStack progressStack, DataTemplate progressTemplate)
		{
			if (progressOverlaySource != null)
				progressOverlaySource.SetProgress(progressTemplate, progressStack);
			Visibility = Visibility.Visible;
		} // proc ShowProgressOverlay

		private void HideProgressOverlay()
		{
			if (progressOverlaySource != null)
				progressOverlaySource.ClearProgress();
			Visibility = Visibility.Collapsed;
		} // proc HideProgressOverlay

		/// <summary>Bind progress stack to this control.</summary>
		public PpsProgressStack ProgressStack { get => (PpsProgressStack)GetValue(ProgressStackProperty); set => SetValue(ProgressStackProperty, value); }

		#endregion

		static PpsProgressOverlay()
		{
			VisibilityProperty.OverrideMetadata(typeof(PpsProgressOverlay), new FrameworkPropertyMetadata(Visibility.Collapsed));
		} // sctor
	} // class PpsProgressOverlay
}
