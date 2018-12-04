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
using System.Reflection;
using System.Runtime.InteropServices;
using TecWare.DE.Server.Configuration;
using TecWare.PPSn.Server;

[assembly: AssemblyTitle("PPSnMod")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyCulture("")]

[assembly: ComVisible(false)]

[assembly: DEConfigurationSchema(typeof(PpsApplication), "Xsd.PPSn.xsd")]
[assembly: Guid("56f95a2c-abdb-49cf-91d9-e01d7e3a23a1")]
