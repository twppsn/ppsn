using System;
using System.ComponentModel;
using System.Reflection;
using System.Windows;

namespace TecWare.PPSn
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();


			SetContent(typeof(Tests.DataListBoxPanel));
		}

		public void SetContent(Type textType)
		{
			var name = textType.GetCustomAttribute<DisplayNameAttribute>();
			Title = name?.DisplayName ?? textType.Name;
			testPane.Content = Activator.CreateInstance(textType);
		}
	} // class MainWindow
}
