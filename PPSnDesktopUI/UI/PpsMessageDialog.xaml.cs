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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using TecWare.DE.Data;
using TecWare.PPSn.Controls;

namespace TecWare.PPSn.UI
{
	internal partial class PpsMessageDialog : PpsWindow
	{
		private PpsMessageDialogButton[] dialogButtons = null;
		private bool isDetailedVisible = false;
		private int buttonIndex = -1;

		#region -- Ctor/Dtor ----------------------------------------------------------

		public PpsMessageDialog()
		{
			InitializeComponent();

			AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(Button_Click));

			buttonBar.ItemContainerGenerator.StatusChanged += ItemContainerGenerator_StatusChanged;
		} // ctor

		public PpsMessageDialog(params string[] buttons)
			: this()
		{
			SetButtons(buttons);
		} // ctor

		public void SetButtons(string[] buttonInfo)
		{
			if (buttonInfo == null && buttonInfo.Length == 0)
				buttonInfo = OkButton;

			dialogButtons = new PpsMessageDialogButton[buttonInfo.Length];
			for (var i = 0; i < dialogButtons.Length; i++)
				dialogButtons[i] = new PpsMessageDialogButton(this, i, buttonInfo[i]);
			SetValue(buttonsPropertyKey, dialogButtons);
		} // proc SetButtons

		private void SetDetailMode(bool isDetailMode)
		{
			if (isDetailedVisible != isDetailMode)
			{
				isDetailedVisible = isDetailMode;
				if (dialogButtons != null)
					Array.ForEach(dialogButtons, c => c.OnVisibleChanged());
			}
		} // proc SetDetailMode

		protected override void OnPreviewKeyDown(KeyEventArgs e)
		{
			if (Keyboard.IsKeyDown(Key.LeftCtrl) && Keyboard.IsKeyDown(Key.LeftAlt))
				SetDetailMode(true);
			base.OnPreviewKeyDown(e);
		} // proc OnPreviewKeyDown

		private void ItemContainerGenerator_StatusChanged(object sender, EventArgs e)
		{
			if (buttonBar.ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
				UpdateFocus();
		} // event ItemContainerGenerator_StatusChanged

		private void UpdateFocus()
		{
			if (dialogButtons != null && buttonIndex >= 0 && buttonIndex < dialogButtons.Length
				&& buttonBar.ItemContainerGenerator.ContainerFromItem(dialogButtons[buttonIndex]) is IInputElement ie)
			{
				if (ie.Focusable && !ie.IsKeyboardFocusWithin)
					FocusManager.SetFocusedElement(this, ie);
			}
		} // proc UpdateFocus

		private void Button_Click(object sender, RoutedEventArgs e)
		{
			if (e.OriginalSource is ButtonBase b && b.CommandParameter is int idx)
			{
				ButtonIndex = idx;
				DialogResult = true;
			}
		} // event Button_Click

		public bool IsDetailedVisible => isDetailedVisible;

		#endregion

		#region -- Buttons - property -------------------------------------------------

		private readonly static DependencyPropertyKey buttonsPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Buttons), typeof(IReadOnlyList<PpsMessageDialogButton>), typeof(PpsMessageDialog), new FrameworkPropertyMetadata(null));
		public readonly static DependencyProperty ButtonsProperty = buttonsPropertyKey.DependencyProperty;

		public IReadOnlyList<PpsMessageDialogButton> Buttons => (IReadOnlyList<PpsMessageDialogButton>)GetValue(ButtonsProperty);

		#endregion

		#region -- Image - property ---------------------------------------------------

		public readonly static DependencyProperty ImageNameProperty = DependencyProperty.Register(nameof(ImageName), typeof(string), typeof(PpsMessageDialog), new FrameworkPropertyMetadata(null));

		public string ImageName { get => (string)GetValue(ImageNameProperty); set => SetValue(ImageNameProperty, value); }

		#endregion

		#region -- Message - property -------------------------------------------------

		public readonly static DependencyProperty MessageProperty = DependencyProperty.Register(nameof(Message), typeof(object), typeof(PpsMessageDialog), new FrameworkPropertyMetadata(null));

		public object Message { get => GetValue(MessageProperty); set => SetValue(MessageProperty, value); }

		#endregion

		#region -- class MessageTemplateSelectorImplementation ------------------------

		private sealed class MessageTemplateSelectorImplementation : DataTemplateSelector
		{
			public override DataTemplate SelectTemplate(object item, DependencyObject container)
			{
				DataTemplate template = null;
				if (item != null)
					template = container.FindResource<DataTemplate>(item is string ? (object)"simpleText" : item.GetType());
				if (template == null)
					template = base.SelectTemplate(item, container);
				return template;
			} // func SelectTemplate
		} // class MessageTemplateSelectorImplementation

		#endregion

		public int ButtonIndex
		{
			get => buttonIndex;
			set 
			{
				if (buttonIndex != value)
				{
					buttonIndex = value;
					UpdateFocus();
				}
			}
		} // prop ButtonIndex

		public static DataTemplateSelector MessageTemplateSelector { get; } = new MessageTemplateSelectorImplementation();

		public static string[] OkButton { get; } = new string[] { "Ok" };
		public static string[] YesNoButtons { get; } = new string[] { "Ja", "Nein" };
		public static string[] OkCancelButtons { get; } = new string[] { "Ok", "Abbrechen" };
	} // class PpsMessageDialog

	#region -- class PpsMessageDialogButton -------------------------------------------

	internal sealed class PpsMessageDialogButton : INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler PropertyChanged;

		private readonly PpsMessageDialog owner;
		private readonly int index;
		private readonly string title;
		private readonly bool isDetailed;

		public PpsMessageDialogButton(PpsMessageDialog owner, int index, string title)
		{
			this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
			this.index = index;
			this.title = title ?? throw new ArgumentNullException(nameof(title));

			isDetailed = this.title[0] == '.';
			if (isDetailed)
				this.title = this.title.Substring(1);
		} // ctor

		public void OnVisibleChanged()
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Visibility)));

		public int Index => index;
		public string Title => title;
		public Visibility Visibility => isDetailed && !owner.IsDetailedVisible ? Visibility.Collapsed : Visibility.Visible;
	} // class PpsMessageDialogButton

	#endregion
}
