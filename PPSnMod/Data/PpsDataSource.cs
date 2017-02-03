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
using System.Xml.Linq;
using TecWare.DE.Server;
using TecWare.DE.Data;
using TecWare.PPSn.Server.Data;

namespace TecWare.PPSn.Server.Data
{
	public abstract class PpsDataSource : DEConfigItem
	{
		public PpsDataSource(IServiceProvider sp, string name)
			: base(sp, name)
		{
		} // ctor

		//public virtual bool EnsureConnection(object context)
		//{
		//	return true;
		//}

		//public virtual bool IsConnectionRelatedException(object context, Exception e)
		//{
		//	return false;
		//}

		public abstract IPpsConnectionHandle CreateConnection(IPpsPrivateDataContext privateUserData, bool throwException = true);

		public virtual Task<IPpsSelectorToken> CreateSelectorTokenAsync(string name, XElement sourceDescription)
		{
			throw new NotImplementedException();
		} // func CreateSelectorTokenAsync

		public virtual PpsDataSynchronization CreateSynchronizationSession(IPpsPrivateDataContext privateUserData, long timeStamp, long syncId)
		{
			throw new NotImplementedException();
		} // func CreateSynchronizationSession

		public virtual PpsDataTransaction CreateTransaction(IPpsConnectionHandle connection)
		{
			throw new NotImplementedException();
		}// func CreateTransaction

		/// <summary>Returns a native column description.</summary>
		/// <param name="columnName"></param>
		/// <returns></returns>
		public virtual IPpsColumnDescription GetColumnDescription(string columnName)
		{
			return null;
		} // func GetColumnDescription

		public virtual PpsDataSetServerDefinition CreateDocumentDescription(IServiceProvider sp, string documentName, XElement config, DateTime configurationStamp)
			=> new PpsDataSetServerDefinition(sp, documentName, config, configurationStamp);

		public abstract string Type { get; }
	} // class PpsDataSource
}
