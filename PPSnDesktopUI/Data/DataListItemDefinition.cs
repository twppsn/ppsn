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
using System.Windows;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Stuff;

namespace TecWare.PPSn.Data
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsDataListItemDefinition : PpsEnvironmentDefinition
	{
		private static readonly XName xnTemplates = "templates";
		private static readonly XName xnTemplate = "template";

		#region -- class TemplateItem -----------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class TemplateItem : IComparable<TemplateItem>
		{
			private Func<object, bool> condition; // condiition if the template is for this item
																						//private LuaChunk templateFunctions;		// Definition of functions for the item
																						//private string updateItemDataSource;	// uri, for a command that will update the item properties
			private DataTemplate template;

			public TemplateItem(PpsDataListItemDefinition owner, XElement xCur, ref int priority)
			{
				var templateKey = xCur.GetAttribute("datatemplate", String.Empty);

				this.condition = null;
				this.template = String.IsNullOrEmpty(templateKey) ? null : owner.Environment.FindResource<DataTemplate>(templateKey);

				this.Priority = priority = xCur.GetAttribute("priority", priority + 1);
			} // ctor

			public int CompareTo(TemplateItem other) => Priority - other.Priority;

			/// <summary>Runs the condition, if the template is accepted.</summary>
			/// <param name="item"></param>
			/// <returns></returns>
			public bool SelectTemplate(dynamic item) => condition == null ? template != null : condition(item);

			public int Priority { get; }
			public DataTemplate Template => template;
		} // class TemplateItem

		#endregion

		private List<TemplateItem> templates = new List<TemplateItem>();

		internal PpsDataListItemDefinition(PpsEnvironment environment, string key)
			: base(environment, key)
		{
		} // ctor

		/// <summary></summary>
		/// <param name="xCur"></param>
		public void AppendTemplate(XElement xCur)
		{
			var priority = templates.Count;
			var template = new TemplateItem(this, xCur, ref priority);

			// insert the item in order of the priority
			var index = templates.BinarySearch(template);
			if (index < 0)
				templates.Insert(~index, template);
			else
				templates.Insert(index, template);
		} // proc AppendTemplate

		/// <summary></summary>
		/// <param name="item"></param>
		/// <returns></returns>
		public DataTemplate FindTemplate(dynamic item, bool updateItem = true)
		{
			var template = templates.FirstOrDefault(c => c.SelectTemplate(item));
			if (template != null)
			{
				// update the template
				if (updateItem)
				{
				}

				// Return the template
				return template.Template;
			}
			else
				return null;
		} // func FindTemplate
	} // class PpsDataListItemType
}
