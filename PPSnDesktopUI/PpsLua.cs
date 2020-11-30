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
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Neo.IronLua;
using TecWare.PPSn.UI;

namespace TecWare.PPSn
{
	#region -- interface IPpsLuaShell -------------------------------------------------

	/// <summary>Shell service of the scripting interface.</summary>
	public interface IPpsLuaShell
	{
		/// <summary>Compile a text to a code chunk.</summary>
		/// <param name="code">Chunk source.</param>
		/// <param name="source">Source location for the debug information.</param>
		/// <param name="throwException">If the compile fails, should be raised a exception.</param>
		/// <param name="arguments">Argument definition for the chunk.</param>
		/// <returns></returns>
		Task<LuaChunk> CompileAsync(TextReader code, string source, bool throwException, params KeyValuePair<string, Type>[] arguments);

		/// <summary>Global environment.</summary>
		LuaTable Global { get; }
		/// <summary>Lua engine.</summary>
		Lua Lua { get; }
	} // interface IPpsLuaShell

	#endregion

	#region -- interface IPpsLuaCodeBehind --------------------------------------------

	public interface IPpsLuaCodeBehind : IPpsXamlCode
	{
		void OnControlCreated(FrameworkElement control, LuaTable arguments);
	} // interface IPpsLuaCodeBehind

	#endregion
}
