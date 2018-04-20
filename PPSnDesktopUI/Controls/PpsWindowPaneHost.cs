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
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using TecWare.PPSn.UI;

namespace TecWare.PPSn.Controls
{
	// idea: geplant für itemscontrol implementiert zusätzlich IPpsWindowPaneHost -> host sync progress?
	/// <summary>Host for panes to support Progress and Load.</summary>
	public class PpsWindowPaneItem : Control
	{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty CurrentPaneProperty = DependencyProperty.Register(nameof(CurrentPane), typeof(IPpsWindowPane), typeof(PpsWindowPaneItem), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, new PropertyChangedCallback(OnCurrentPaneChanged)));

		private static readonly DependencyPropertyKey titlePropertyKey = DependencyProperty.RegisterReadOnly(nameof(Title), typeof(string), typeof(PpsWindowPaneItem), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty TitleProperty = titlePropertyKey.DependencyProperty;
		private static readonly DependencyPropertyKey subTitlePropertyKey = DependencyProperty.RegisterReadOnly(nameof(SubTitle), typeof(string), typeof(PpsWindowPaneItem), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty SubTitleProperty = subTitlePropertyKey.DependencyProperty;
		private static readonly DependencyPropertyKey commandsPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Commands), typeof(PpsUICommandCollection), typeof(PpsWindowPaneItem), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty CommandsProperty = commandsPropertyKey.DependencyProperty;
		private static readonly DependencyPropertyKey controlPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Control), typeof(object), typeof(PpsWindowPaneItem), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty ControlProperty = controlPropertyKey.DependencyProperty;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		private static void OnCurrentPaneChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsWindowPaneItem)d).OnCurrentPaneChanged((IPpsWindowPane)e.NewValue, (IPpsWindowPane)e.OldValue);

		/// <summary></summary>
		/// <param name="newValue"></param>
		/// <param name="oldValue"></param>
		protected virtual void OnCurrentPaneChanged(IPpsWindowPane newValue, IPpsWindowPane oldValue)
		{
			if (oldValue != null)
				oldValue.PropertyChanged -= CurrentPanePropertyChanged;

			if (newValue != null)
			{
				newValue.PropertyChanged += CurrentPanePropertyChanged;

				SetValue(titlePropertyKey, CurrentPane.Title);
				SetValue(subTitlePropertyKey, CurrentPane.SubTitle);
				SetValue(commandsPropertyKey, CurrentPane.Commands);
				SetValue(controlPropertyKey, CurrentPane.Control);
			}
		} // proc OnCurrentPaneChanged

		private void CurrentPanePropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case nameof(IPpsWindowPane.Title):
					SetValue(titlePropertyKey, CurrentPane.Title);
					break;
				case nameof(IPpsWindowPane.SubTitle):
					SetValue(subTitlePropertyKey, CurrentPane.SubTitle);
					break;
				case nameof(IPpsWindowPane.Commands):
					SetValue(commandsPropertyKey, CurrentPane.Commands);
					break;
				case nameof(IPpsWindowPane.Control):
					SetValue(controlPropertyKey, CurrentPane.Control);
					break;
			}
		} // proc CurrentPanePropertyChanged

		/// <summary>Current pane</summary>
		public IPpsWindowPane CurrentPane { get => (IPpsWindowPane)GetValue(CurrentPaneProperty); set => SetValue(CurrentPaneProperty, value); }

		/// <summary>Current title of the pane.</summary>
		public string Title => (string)GetValue(TitleProperty);
		/// <summary>Current subtitle of the pane.</summary>
		public string SubTitle => (string)GetValue(TitleProperty);
		/// <summary>Current commands of the pane</summary>
		public PpsUICommandCollection Commands => (PpsUICommandCollection)GetValue(TitleProperty);
		/// <summary>Current control</summary>
		public object Control => GetValue(ControlProperty);
	} // class PpsWindowPaneItem
}
