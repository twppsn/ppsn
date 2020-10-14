﻿#region -- copyright --
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
using TecWare.DE.Server;

namespace TecWare.PPSn.Server
{
	/// <summary>Defines a generic package for a ppsn application.</summary>
	public class PpsPackage : DEConfigLogItem
	{
		/// <summary></summary>
		/// <param name="sp"></param>
		/// <param name="name"></param>
		public PpsPackage(IServiceProvider sp, string name)
			: base(sp, name)
		{
		} // ctor
	} // class PpsPackage
}
