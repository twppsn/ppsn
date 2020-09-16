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
using System.Windows.Controls;
using Neo.IronLua;

namespace TecWare.PPSn.UI
{
	/// <summary>Base class for window pane hosts.</summary>
	internal abstract class PpsPaneHost : Control, IPpsWindowPaneHost
	{
		private readonly IPpsWindowPaneManager paneManager;
		private readonly IPpsWindowPane pane;

		protected PpsPaneHost(IPpsWindowPaneManager paneManager, Type paneType)
		{
			this.paneManager = paneManager ?? throw new ArgumentNullException(nameof(paneManager));

			// create the pane
			pane = PpsWindowPaneHelper.CreateEmptyPane(paneManager, this, paneType);
			pane.PropertyChanged += Pane_PropertyChanged;

			// initialize properties
			SetValue(titlePropertyKey, pane.Title);
			SetValue(subTitlePropertyKey, pane.SubTitle);
			SetValue(imagePropertyKey, pane.Image);
			SetControl(pane.Control);

			SetValue(panePropertyKey, pane);
		} // ctor

		#region -- Pane ---------------------------------------------------------------

		/// <summary>Load the pane content</summary>
		/// <param name="arguments"></param>
		/// <returns></returns>
		public Task LoadPaneAsync(LuaTable arguments)
			=> pane.LoadAsync(arguments);

		/// <summary>Unload pane content.</summary>
		/// <param name="commit"></param>
		/// <returns></returns>
		public Task<bool> UnloadPaneAsync(bool? commit = null)
			=> pane.UnloadAsync(commit);

		/// <summary>Close this pane.</summary>
		/// <returns></returns>
		public abstract Task<bool> ClosePaneAsync();

		private void Pane_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			switch(e.PropertyName)
			{
				case nameof(IPpsWindowPane.Title):
					SetValue(titlePropertyKey, pane.Title);
					break;
				case nameof(IPpsWindowPane.SubTitle):
					SetValue(subTitlePropertyKey, pane.SubTitle);
					break;
				case nameof(IPpsWindowPane.Image):
					SetValue(imagePropertyKey, pane.Image);
					break;
				case nameof(IPpsWindowPane.Control):
					SetControl(pane.Control);
					break;
			}
		} // event Pane_PropertyChanged

		#endregion

		#region -- Title - property ---------------------------------------------------

		private static readonly DependencyPropertyKey titlePropertyKey = DependencyProperty.RegisterReadOnly(nameof(Title), typeof(string), typeof(PpsPaneHost), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty TitleProperty = titlePropertyKey.DependencyProperty;

		/// <summary>Current title of the pane.</summary>
		public string Title => (string)GetValue(TitleProperty);

		#endregion

		#region -- SubTitle - property ------------------------------------------------

		private static readonly DependencyPropertyKey subTitlePropertyKey = DependencyProperty.RegisterReadOnly(nameof(SubTitle), typeof(string), typeof(PpsPaneHost), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty SubTitleProperty = subTitlePropertyKey.DependencyProperty;

		/// <summary>Current subtitle of the pane.</summary>
		public string SubTitle => (string)GetValue(SubTitleProperty);

		#endregion

		#region -- Image - property ---------------------------------------------------

		private static readonly DependencyPropertyKey imagePropertyKey = DependencyProperty.RegisterReadOnly(nameof(Image), typeof(object), typeof(PpsPaneHost), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty ImageProperty = imagePropertyKey.DependencyProperty;

		/// <summary>Image of the window pane.</summary>
		public object Image => GetValue(ImageProperty);

		#endregion

		#region -- Control - property -------------------------------------------------

		private static readonly DependencyPropertyKey controlPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Control), typeof(Control), typeof(PpsPaneHost), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty ControlProperty = controlPropertyKey.DependencyProperty;

		private void SetControl(object value)
		{
			// todo: convert to control

			SetValue(controlPropertyKey, (Control)value);
		} // proc SetControl

		public Control Control => (Control)GetValue(ControlProperty);

		#endregion

		#region -- Pane - property ----------------------------------------------------

		private static readonly DependencyPropertyKey panePropertyKey = DependencyProperty.RegisterReadOnly(nameof(Pane), typeof(IPpsWindowPane), typeof(PpsPaneHost), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty PaneProperty = panePropertyKey.DependencyProperty;
				
		public IPpsWindowPane Pane => (IPpsWindowPane)GetValue(PaneProperty);

		#endregion

		public abstract IPpsProgressFactory Progress { get; }
		public IPpsWindowPaneManager PaneManager => paneManager;
	} // class PpsPaneHost
}
