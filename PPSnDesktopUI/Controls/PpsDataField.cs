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
using System.Windows;
using TecWare.PPSn.UI;

namespace TecWare.PPSn.Controls
{
	#region -- class PpsDataField -----------------------------------------------------

	/// <summary>DataField emitter</summary>
	public class PpsDataField : UIElement, IPpsXamlEmitter, IPpsXamlDynamicProperties
	{
		/// <summary></summary>
		public static readonly DependencyProperty FieldSourceProperty = DependencyProperty.RegisterAttached(nameof(FieldSource), typeof(string), typeof(PpsDataField), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.NotDataBindable));
		/// <summary></summary>
		public static readonly DependencyProperty FieldBindingPathProperty = DependencyProperty.RegisterAttached(nameof(FieldBindingPath), typeof(string), typeof(PpsDataField), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.NotDataBindable));
		/// <summary></summary>
		public static readonly DependencyProperty FieldNameProperty = DependencyProperty.Register(nameof(FieldName), typeof(string), typeof(PpsDataField), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.NotDataBindable));

		private readonly Dictionary<string, object> properties = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

		System.Xaml.XamlReader IPpsXamlEmitter.CreateReader(IServiceProvider context)
		{
			return null;
		} // func CreateReader

		object IPpsXamlDynamicProperties.GetValue(string name)
			=> properties.TryGetValue(name, out var tmp) ? tmp : null;

		void IPpsXamlDynamicProperties.SetValue(string name, object value)
			=> properties[name] = value;

		/// <summary></summary>
		/// <param name="d"></param>
		/// <returns></returns>
		public static string GetFieldSource(DependencyObject d)
			=> (string)d.GetValue(FieldSourceProperty);

		/// <summary></summary>
		/// <param name="d"></param>
		/// <param name="value"></param>
		public static void SetFieldSource(DependencyObject d, string value)
			=> d.SetValue(FieldSourceProperty, value);

		/// <summary></summary>
		/// <param name="d"></param>
		/// <returns></returns>
		public static string GetFieldBindingPath(DependencyObject d)
			=> (string)d.GetValue(FieldSourceProperty);

		/// <summary></summary>
		/// <param name="d"></param>
		/// <param name="value"></param>
		public static void SetFieldBindingPath(DependencyObject d, string value)
			=> d.SetValue(FieldSourceProperty, value);

		/// <summary>Field source for the current level.</summary>
		public string FieldSource { get => (string)GetValue(FieldSourceProperty); set => SetValue(FieldSourceProperty, value); }
		/// <summary>Field binding path info.</summary>
		public string FieldBindingPath { get => (string)GetValue(FieldBindingPathProperty); set => SetValue(FieldBindingPathProperty, value); }

		/// <summary>Field name.</summary>
		public string FieldName { get => (string)GetValue(FieldNameProperty); set => SetValue(FieldNameProperty, value); }
	} // class PpsDataField

	#endregion
}
