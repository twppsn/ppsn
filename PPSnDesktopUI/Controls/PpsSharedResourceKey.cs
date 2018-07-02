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
using System.Reflection;
using System.Windows;

namespace TecWare.PPSn.Controls
{
	#region -- class PpsSharedResource ------------------------------------------------

	/// <summary></summary>
	public sealed class PpsSharedResourceKey : ResourceKey
	{
		private readonly object resourceId;

		/// <summary></summary>
		/// <param name="resourceId"></param>
		private PpsSharedResourceKey(object resourceId)
		{
			this.resourceId = resourceId ?? throw new ArgumentNullException(nameof(resourceId));
		} // ctor

		/// <summary></summary>
		/// <returns></returns>
		public override int GetHashCode()
			=> resourceId.GetHashCode();

		/// <summary></summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		public override bool Equals(object obj)
			=> obj is PpsSharedResourceKey res
				? Equals(res.resourceId == resourceId)
				: false;

		/// <summary></summary>
		/// <returns></returns>
		public override string ToString()
			=> "PpsResouce:" + resourceId.ToString();

		/// <summary></summary>
		/// <param name="serviceProvider"></param>
		/// <returns></returns>
		public override object ProvideValue(IServiceProvider serviceProvider)
			=> this;

		/// <summary></summary>
		public object ResourceId => resourceId;
		/// <summary></summary>
		public override Assembly Assembly => typeof(PpsSharedResourceKey).Assembly;

		/// <summary>Style for ClearButton in TextBox, CommboBox...</summary>
		public static readonly ResourceKey PpsClearContentButtonStyle = new PpsSharedResourceKey(nameof(PpsClearContentButtonStyle));

		/// <summary>Style for round Button with image and (optional)text</summary>
		public static readonly ResourceKey PpsMandatoryFieldInfoStyle = new PpsSharedResourceKey(nameof(PpsMandatoryFieldInfoStyle));
	} // class PpsSharedResourceKey

	#endregion
}
