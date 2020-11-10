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

namespace TecWare.PPSn.UI
{
	#region -- class BooleanBox -------------------------------------------------------

	/// <summary>Boolean box helper</summary>
	public static class BooleanBox
	{
		/// <summary>Get a object from a boolean value.</summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static object GetObject(bool value)
			=> value ? True : False;

		/// <summary>Get a object from a boolean value.</summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static object GetObject(bool? value)
			=> value.HasValue ? GetObject(value.Value) : null;

		/// <summary>Get the boolean value from an object.</summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static bool GetBool(object value)
			=> Equals(value, True);

		/// <summary>Get the nullable boolean value from an object.</summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static bool? GetBoolNullable(object value)
			=> value == null ? (bool?)null : Object.Equals(value, True);

		/// <summary>Object for <c>true</c>.</summary>
		public static object True { get; } = true;
		/// <summary>Object for <c>false</c>.</summary>
		public static object False { get; } = false;
	} // class BooleanBox

	#endregion
}
