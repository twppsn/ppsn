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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace TecWare.PPSn.Controls
{
	public class PpsStackSection : HeaderedContentControl
	{
		public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(object), typeof(PpsStackSection), new PropertyMetadata("title"));
		public object Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

		public static readonly DependencyProperty SubTitleProperty = DependencyProperty.Register(nameof(SubTitle), typeof(object), typeof(PpsStackSection), new PropertyMetadata("subt"));
		public object SubTitle { get => GetValue(SubTitleProperty); set => SetValue(SubTitleProperty, value); }

		public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(PpsStackSection));
		public bool IsOpen { get => BooleanBox.GetBool(GetValue(IsOpenProperty)); set => SetValue(IsOpenProperty, value); }
	}
}
