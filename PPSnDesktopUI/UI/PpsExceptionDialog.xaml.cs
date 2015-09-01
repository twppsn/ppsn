using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace TecWare.PPSn.UI
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	internal partial class PpsExceptionDialog : Window
	{
		public readonly static DependencyProperty MessageTypeProperty = DependencyProperty.Register(nameof(MessageType), typeof(PpsTraceItemType), typeof(PpsExceptionDialog));
		public readonly static DependencyProperty MessageTextProperty = DependencyProperty.Register(nameof(MessageText), typeof(object), typeof(PpsExceptionDialog));
		public readonly static DependencyProperty SkipVisibleProperty = DependencyProperty.Register(nameof(SkipVisible), typeof(bool), typeof(PpsExceptionDialog));
		public readonly static DependencyProperty SkipCheckedProperty = DependencyProperty.Register(nameof(SkipChecked), typeof(bool), typeof(PpsExceptionDialog));
		public readonly static DependencyProperty DetailsVisibleProperty = DependencyProperty.Register(nameof(DetailsVisible), typeof(bool), typeof(PpsExceptionDialog));

		public PpsExceptionDialog()
		{
			InitializeComponent();

			CommandBindings.Add(new CommandBinding(
				ApplicationCommands.Close,
				(sender, e) => Close()
			));

			CommandBindings.Add(new CommandBinding(
				ApplicationCommands.Properties,
				(sender, e) =>
				{
					DialogResult = true;
					Close();
				}
			));

			DetailsVisible = false;

			DataContext = this;
		} // ctor

		protected override void OnPreviewKeyDown(KeyEventArgs e)
		{
			if (!DetailsVisible && Keyboard.IsKeyDown(Key.LeftCtrl) && Keyboard.IsKeyDown(Key.LeftAlt))
				DetailsVisible = true;
			base.OnPreviewKeyDown(e);
		} // proc OnPreviewKeyDown
		
		public PpsTraceItemType MessageType
		{
			get { return (PpsTraceItemType)GetValue(MessageTypeProperty); }
			set { SetValue(MessageTypeProperty, value); }
		} // prop MessageType

		public object MessageText
		{
			get { return GetValue(MessageTextProperty); }
			set { SetValue(MessageTextProperty, value); }
		} // prop MessageText

		public bool SkipVisible
		{
			get { return (bool)GetValue(SkipVisibleProperty); }
			set { SetValue(SkipVisibleProperty, value); }
		} // prop MessageText

		public bool SkipChecked
		{
			get { return (bool)GetValue(SkipCheckedProperty); }
			set { SetValue(SkipCheckedProperty, value); }
		} // prop MessageText

		public bool DetailsVisible
		{
			get { return (bool)GetValue(DetailsVisibleProperty); }
			set { SetValue(DetailsVisibleProperty, value); }
		} // prop MessageText
	} // class ExceptionDialog
}
