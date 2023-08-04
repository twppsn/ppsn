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
using System.Text.RegularExpressions;
using TecWare.DE.Stuff;

namespace TecWare.PPSn.Core.Stuff
{
	#region -- class PpsJumpInfo ------------------------------------------------------

	/// <summary>Helper to parse jump infos.</summary>
	public sealed class PpsJumpInfo
	{
		private readonly string relativeUri;
		private readonly string[] parameters;

		private PpsJumpInfo(string relativeUri, string[] parameters)
		{
			this.relativeUri = relativeUri ?? throw new ArgumentNullException(nameof(relativeUri));
			this.parameters = parameters ?? Array.Empty<string>();
		} // ctor

		/// <summary>Create a full uri.</summary>
		/// <param name="properties"></param>
		/// <returns></returns>
		/// <exception cref="ArgumentNullException"></exception>
		public string CreateUri(IPropertyReadOnlyDictionary properties)
		{
			if (properties == null)
				throw new ArgumentNullException(nameof(properties));

			var values = new object[parameters.Length];
			for (var i = 0; i < parameters.Length; i++)
			{
				if (!properties.TryGetProperty(parameters[i], out values[i]))
					values[i] = null;
			}

			return String.Format(relativeUri, values);
		} // func CreateUri

		/// <summary>Raw uri.</summary>
		public string Uri => relativeUri;
		/// <summary>Names of the replacements.</summary>
		public IReadOnlyList<string> ParameterNames => parameters;

		private static readonly Regex parseReplacer = new Regex(@"\{(?<n>[\w\.]+)(?<a>:.*)?\}", RegexOptions.Compiled);

		/// <summary>Create a jump info.</summary>
		/// <param name="uri"></param>
		/// <param name="fieldName"></param>
		/// <returns></returns>
		/// <exception cref="ArgumentException"></exception>
		public static PpsJumpInfo Create(string uri, string fieldName = null)
		{
			var sb = new StringBuilder();
			var parameterList = new List<string>();

			var offset = 0;
			var parameterIndex = 0;
			foreach (var m in parseReplacer.Matches(uri).Cast<Match>())
			{
				// append part
				var index = m.Index;
				if (offset < index)
					sb.Append(uri.Substring(offset, index - offset));

				// parse parameter
				var paramterName = m.Groups["n"].Value;
				if (Int32.TryParse(paramterName, out var tmp))
				{
					if (tmp == 0)
					{
						var p = fieldName.LastIndexOf('.');
						parameterList.Add(p >= 0 ? fieldName.Substring(p + 1) : fieldName);
					}
					else
						throw new ArgumentException("Invalid parameter");
				}
				else
				{
					parameterList.Add(paramterName);
				}
				sb.Append('{').Append(parameterIndex++);
				if (m.Groups["a"].Length > 0)
					sb.Append(m.Groups["a"].Value);
				sb.Append('}');

				// next block
				offset = index + m.Length;
			}

			if (offset < uri.Length)
				sb.Append(uri.Substring(offset));

			return new PpsJumpInfo(sb.ToString(), parameterList.ToArray());
		} // func Create
	} // class PpsJumpInfo

	#endregion
}
