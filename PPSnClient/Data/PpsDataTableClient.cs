using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TecWare.PPSn.Data
{
	public class PpsDataTableClient : PpsDataTable
	{
		private PpsDataSetClient dataSetClient;						 // Eigentümer dieser Tabelle

		#region -- Ctor/Dtor --------------------------------------------------------------

		public PpsDataTableClient(PpsDataTableDefinition tableDefinition, PpsDataSet dataSet)
			: base(tableDefinition, dataSet)
		{
			dataSetClient = (PpsDataSetClient)dataSet;
		} // ctor

		#endregion
	}
}
