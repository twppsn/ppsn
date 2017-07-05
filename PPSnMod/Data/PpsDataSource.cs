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
using System.Threading.Tasks;
using System.Xml.Linq;
using TecWare.DE.Server;
using TecWare.DE.Stuff;

namespace TecWare.PPSn.Server.Data
{
	public abstract class PpsDataSource : DEConfigItem
	{
		private readonly PpsApplication application;

		public PpsDataSource(IServiceProvider sp, string name)
			: base(sp, name)
		{
			this.application = sp.GetService<PpsApplication>(true);
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

		public virtual PpsDataSynchronization CreateSynchronizationSession(IPpsPrivateDataContext privateUserData, DateTime lastSynchronization)
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
		public virtual IPpsColumnDescription GetColumnDescription(string columnName, bool throwException = false)
		{
			return null;
		} // func GetColumnDescription

		public PpsDataSetServerDefinition CreateDataSetDefinition(string dataSetName, XElement config, DateTime configurationStamp)
			=> new PpsDataSetServerDefinition(this, dataSetName, config, configurationStamp);

		public virtual PpsDataTableServerDefinition CreateTableDefinition(PpsDataSetServerDefinition dataset, string tableName, XElement config)
			=> new PpsDataTableServerDefinition(dataset, tableName, config);

		public abstract string Type { get; }

		/// <summary>Application object.</summary>
		public PpsApplication Application => application;
	} // class PpsDataSource
}
