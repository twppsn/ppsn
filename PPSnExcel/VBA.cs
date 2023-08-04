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
using System.Runtime.InteropServices;
using TecWare.PPSn;
using Excel = Microsoft.Office.Interop.Excel;

namespace PPSnExcel
{
	#region -- interface IPpsnFunctions -----------------------------------------------

	[ComVisible(true)]
	public interface IPpsnFunctions
	{
		bool Refresh(dynamic listObject, dynamic refreshLayout);
	} // interface IPpsnFunctions

	#endregion

	#region -- class PpsnFunctions  ---------------------------------------------------

	[ComVisible(true)]
	[ClassInterface(ClassInterfaceType.None)]
	public class PpsnFunctions : IPpsnFunctions
	{
		public bool Refresh(dynamic listObject, dynamic refreshLayout)
		{
			if (listObject is Excel.ListObject list)
			{
				Globals.ThisAddIn.RefreshTableAsync(null, list, refreshLayout is bool l && l).Await();
				return true;
			}
			else
				return false;
		} // func Refresh
	} // class PpsnFunctions

	#endregion
}
/*Sub CallVSTOMethod()
    Dim addIn As COMAddIn
    Dim automationObject As Object
    Set addIn = Application.COMAddIns("ExcelImportData")
    Set automationObject = addIn.Object
    automationObject.ImportData
End Sub
*/
