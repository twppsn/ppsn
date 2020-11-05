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
using System.Linq;

namespace TecWare.PPSn.Data
{
	//[SQLiteFunction(FuncType = FunctionType.Scalar, Name = "ulower", Arguments = 1)]
	//internal class SQLiteUnicodeLower : SQLiteFunction
	//{
	//	public override object Invoke(object[] args)
	//	{
	//		return args[0].ToString().ToLower();
	//	} // func Invoke
	//} // class SQLiteUnicodeLower

	//[SQLiteFunction(FuncType = FunctionType.Scalar, Name = "uconcat", Arguments = -1)]
	//internal class SQLiteFullTextConcat : SQLiteFunction
	//{
	//	public override object Invoke(object[] args)
	//	{
	//		return String.Join(" ", args.Where(c => c != null && c != DBNull.Value).Select(c => c.ToString())).ToLower();
	//	} // func Invoke
	//} // class SQLiteUnicodeLower
}
