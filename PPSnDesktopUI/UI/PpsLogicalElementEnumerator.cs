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
using System.Collections;
using System.Windows;

namespace TecWare.PPSn.UI
{
	#region -- class PpsLogicalContentEnumerator --------------------------------------

	/// <summary>Helper for Logical-Tree.</summary>
	public class PpsLogicalContentEnumerator : IEnumerator
	{
		private int state = -1;
		private readonly IEnumerator baseItems; // base enumerator
		private readonly object content;
		private readonly Func<object> getContent;

		private PpsLogicalContentEnumerator(IEnumerator baseItems, Func<object> getContent)
		{
			this.baseItems = baseItems;
			content = getContent();
			this.getContent = getContent;
		} // ctor

		private object GetContent()
		{
			if (content != getContent())
				throw new InvalidOperationException();
			return content;
		} // func GetContent

		public object Current => state <= 0 ? GetContent() : baseItems?.Current;

		public bool MoveNext()
		{
			if (++state <= 0)
				return true;
			else if (state > 0)
				return baseItems?.MoveNext() ?? false;
			return false;
		} // func MoveNext

		public void Reset()
		{
			state = -1;
			baseItems?.Reset();
		} // proc Reset

		public static IEnumerator GetLogicalEnumerator(DependencyObject d, IEnumerator logicalChildren, Func<object> getContent)
		{
			var content = getContent();
			if (content != null)
			{
				var templatedParent = d is FrameworkElement fe
					? fe.TemplatedParent
					: (d is FrameworkContentElement fce ? fce.TemplatedParent : null);

				if (templatedParent != null)
				{
					if (content is DependencyObject obj)
					{
						var p = LogicalTreeHelper.GetParent(obj);
						if (p != null && p != d)
							return logicalChildren;
					}
				}
				return new PpsLogicalContentEnumerator(logicalChildren, getContent);
			}
			return logicalChildren;
		} // func GetLogicalEnumerator
	} // class PpsLogicalElementEnumerator

	#endregion

}
