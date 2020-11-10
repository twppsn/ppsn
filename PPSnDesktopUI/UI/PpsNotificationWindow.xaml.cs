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
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;

namespace TecWare.PPSn.UI
{
	internal partial class PpsNotificationWindow : Window
	{
		#region -- class TextImageContent ---------------------------------------------

		private sealed class TextImageContent
		{
			public TextImageContent(string text, string image)
			{
				Text = text;
				ImageName = image;
			} // ctor

			public string Text { get; }
			public string ImageName { get; }
		} // class TextImageContent

		#endregion

		private struct NoneResult { }

		private readonly Storyboard windowStoryboard;
		private readonly DataTemplate textTemplate;

		private object hoverObject = null;
		private TaskCompletionSource<NoneResult> storyboardEnd = null;

		public PpsNotificationWindow()
		{
			InitializeComponent();

			windowStoryboard = (Storyboard)FindResource(nameof(windowStoryboard));
			textTemplate = (DataTemplate)FindResource(nameof(textTemplate));
		} // ctor

		protected override void OnClosing(CancelEventArgs e)
		{
			base.OnClosing(e);
			e.Cancel = hoverObject != null;
		} // proc OnClosing

		protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
		{
			base.OnPropertyChanged(e);
			if (e.Property == ActualWidthProperty ||
				e.Property == ActualHeightProperty)
				RecalcPosition(true);
		} // proc OnPropertyChanged

		private Rect GetHoverArea()
		{
			if (hoverObject is Window w)
				return new Rect(w.Left, w.Top, w.ActualWidth, w.ActualHeight);
			else
				return SystemParameters.WorkArea;
		} // func GetHoverArea

		public void RecalcPosition(bool force = false)
		{
			if (force || IsVisible)
			{
				var rc = GetHoverArea();
				Left = rc.Left + (rc.Width - ActualWidth) / 2;
				Top = rc.Top + (rc.Height - ActualHeight) * 4 / 5;
			}
		} // proc RecalcPosition

		private void SetContent(object content, DataTemplate contentTemplate)
		{
			this.content.ContentTemplate = contentTemplate;
			this.content.Content = content;

			RecalcPosition(true);
		} // proc SetContent

		private void SetTextContent(string text, PpsImage image)
		{
			SetContent(
				new TextImageContent(text, image != PpsImage.None ? image.ToGeometryName() : null), 
				textTemplate
			);
		} // proc SetTextContent

		public Task ShowAsync(object hoverObject, object message, PpsImage image = PpsImage.None)
		{
			this.hoverObject = hoverObject;

			if (storyboardEnd != null)
				storyboardEnd.TrySetResult(new NoneResult());
			storyboardEnd = new TaskCompletionSource<NoneResult>();

			switch (message)
			{
				case string text:
					SetTextContent(text, image);
					break;
				case UIElement control:
					SetContent(control, null);
					break;
				case null:
					break;
				default:
					SetTextContent(message.ToString(), PpsImage.None);
					break;
			}

			if (Visibility == Visibility.Visible) // reset storyboard
			{
				windowStoryboard.Seek(TimeSpan.FromMilliseconds(500), TimeSeekOrigin.BeginTime);
			}
			else // start storyboard
			{
				Opacity = 0.0;
				Show();
				windowStoryboard.Begin();
			}

			return storyboardEnd.Task;
		} // proc Show

		private void WindowStoryboard_Completed(object sender, EventArgs e)
		{
			if (storyboardEnd != null)
			{
				storyboardEnd.TrySetResult(new NoneResult());
				storyboardEnd = null;
			}
			hoverObject = null;
			Close();
		} // event WindowStoryboard_Completed
	} // class PpsNotificationWindow
}
