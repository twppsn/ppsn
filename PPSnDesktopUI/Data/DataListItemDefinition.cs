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
using System.Windows.Markup;
using System.Xml;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Stuff;
using TecWare.PPSn.Stuff;
using TecWare.PPSn.UI;

namespace TecWare.PPSn.Data
{
	/////////////////////////////////////////////////////////////////////////////////
	///// <summary></summary>
	//public class PpsDataListItemDefinition : PpsEnvironmentDefinition
	//{
	//	#region -- class TemplateItem -----------------------------------------------------

	//	///////////////////////////////////////////////////////////////////////////////
	//	/// <summary></summary>
	//	private sealed class TemplateItem : IComparable<TemplateItem>
	//	{
	//		private readonly DataTemplate template;			// template of the row
	//		private readonly Func<object, bool> condition;	// condition if the template is for this item
	//		private readonly int priority;					// select order
	//		private readonly string onlineViewId;			// view, that returns extended data for the row

	//		public TemplateItem(int priority, Func<object, bool> condition, string onlineViewId, DataTemplate template)
	//		{
	//			this.priority = priority;
	//			this.condition = condition;
	//			this.onlineViewId = onlineViewId;
	//			this.template = template ?? throw new ArgumentNullException(nameof(template));

	//		} // ctor

	//		public int CompareTo(TemplateItem other)
	//			=> Priority - other.Priority;

	//		/// <summary>Runs the condition, if the template is accepted.</summary>
	//		/// <param name="item"></param>
	//		/// <returns></returns>
	//		public bool SelectTemplate(dynamic item)
	//		{
	//			if (condition == null)
	//				return template != null;
	//			else
	//				return condition(item);
	//		} // func SelectTemplate

	//		public string OnlineViewId => onlineViewId;
	//		public int Priority => priority;
	//		public DataTemplate Template => template;
	//	} // class TemplateItem

	//	#endregion

	//	private readonly List<TemplateItem> templates = new List<TemplateItem>();

	//	internal PpsDataListItemDefinition(PpsEnvironment environment, string key)
	//		: base(environment, key)
	//	{
	//	} // ctor

	//	private async Task<Func<object, bool>> ReadConditionAsync(XmlReader xml)
	//	{
	//		var condition = await Environment.CompileLambdaAsync<Func<object, bool>>(xml, true, "Item");
	//		await xml.ReadEndElementAsync();
	//		return condition;
	//	} // func ReadConditionAsync

	//	/// <summary></summary>
	//	/// <param name="xml"></param>
	//	/// <param name="priority"></param>
	//	/// <returns></returns>
	//	public async Task<int> AppendTemplateAsync(XmlReader xml, int priority)
	//	{
	//		// get base attributes
	//		priority = xml.GetAttribute("priority", priority + 1);
	//		var onlineViewId = xml.GetAttribute("viewId", String.Empty);

	//		// read start element
	//		await xml.ReadAsync();

	//		// read optional condition
	//		var condition =
	//			await xml.ReadOptionalStartElementAsync(StuffUI.xnCondition)
	//				? await ReadConditionAsync(xml)
	//				: null;

	//		// read template
	//		var template = await PpsXamlParser.LoadAsync<DataTemplate>(xml.ReadElementAsSubTree());

	//		var templateItem = new TemplateItem(priority, condition, onlineViewId, template);

	//		// insert the item in order of the priority
	//		var index = templates.BinarySearch(templateItem);
	//		if (index < 0)
	//			templates.Insert(~index, templateItem);
	//		else
	//			templates.Insert(index, templateItem);

	//		return priority;
	//	} // proc AppendTemplate

	//	/// <summary></summary>
	//	/// <param name="item"></param>
	//	/// <returns></returns>
	//	public DataTemplate FindTemplate(dynamic item)
	//		=> templates.FirstOrDefault(c => c.SelectTemplate(item))?.Template;
	//} // class PpsDataListItemType
}
