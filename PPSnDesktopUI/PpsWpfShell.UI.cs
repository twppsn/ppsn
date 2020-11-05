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
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using TecWare.DE.Stuff;

namespace TecWare.PPSn
{
	#region -- interface IPpsWpfResources ---------------------------------------------

	public interface IPpsWpfResources
	{
		IEnumerable<T> FindResourceByKey<TKEY, T>(Predicate<TKEY> predicate = null)
			where TKEY : ResourceKey;

		T FindResource<T>(object resourceKey)
			where T : class;
	} // interface IPpsWpfResources

	#endregion

	#region -- class PpsWpfShell ------------------------------------------------------

	public static partial class PpsWpfShell
	{
		#region -- FindResource -------------------------------------------------------

		#region -- class FindResourceStackItem ----------------------------------------

		private sealed class FindResourceStackItem
		{
			private readonly ResourceDictionary resourceDictionary;
			private int mergedIndex;

			public FindResourceStackItem(ResourceDictionary resourceDictionary)
			{
				this.resourceDictionary = resourceDictionary ?? throw new ArgumentNullException(nameof(resourceDictionary));

				mergedIndex = resourceDictionary.MergedDictionaries.Count - 1;
			} // ctor

			public bool TryGetNextDictionary(out FindResourceStackItem stackItem)
			{
				if (mergedIndex >= 0)
				{
					stackItem = new FindResourceStackItem(resourceDictionary.MergedDictionaries[mergedIndex--]);
					return true;
				}
				else
				{
					stackItem = null;
					return false;
				}
			} // func TryGetNextDictionary

			public ResourceDictionary Resources => resourceDictionary;
		} // class FindResourceStackItem

		#endregion

		/// <summary>Find resource by key in the resource dictionary</summary>
		/// <typeparam name="TKEY"></typeparam>
		/// <typeparam name="T"></typeparam>
		/// <param name="resourceDictionary"></param>
		/// <param name="predicate"></param>
		/// <returns></returns>
		public static IEnumerable<T> FindResourceByKey<TKEY, T>(this ResourceDictionary resourceDictionary, Predicate<TKEY> predicate = null)
			where TKEY : ResourceKey
		{
			var dictionaryStack = new Stack<FindResourceStackItem>();
			var current = new FindResourceStackItem(resourceDictionary);

			var returnedKeys = new List<TKEY>();

			while (current != null)
			{
				// enumerate merged resources
				while (current.TryGetNextDictionary(out var stackItem))
				{
					dictionaryStack.Push(current);
					current = stackItem;
				}

				// enumerate resource keys
				foreach (var key in current.Resources.Keys.OfType<TKEY>())
				{
					if (!returnedKeys.Contains(key) && (predicate == null || predicate(key)) && current.Resources[key] is T v)
					{
						returnedKeys.Add(key);
						yield return v;
					}
				}
				
				current = dictionaryStack.Count > 0 ? dictionaryStack.Pop() : null;
			}
		} // func FindResourceByKey

		/// <summary>Find resource by key in the main resource dictionary</summary>
		/// <typeparam name="TKEY"></typeparam>
		/// <typeparam name="T"></typeparam>
		/// <param name="predicate"></param>
		/// <returns></returns>
		public static IEnumerable<T> FindResourceByKey<TKEY, T>(this IPpsShell shell, Predicate<TKEY> predicate = null)
			where TKEY : ResourceKey
			=> shell.GetService<IPpsWpfResources>(true).FindResourceByKey<TKEY, T>(predicate);

		/// <summary>Find a global resource.</summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="resourceKey"></param>
		/// <returns></returns>
		public static T FindResource<T>(this IPpsShell shell, object resourceKey)
			where T : class
			=> shell.GetService<IPpsWpfResources>(true).FindResource<T>(resourceKey);

		#endregion
	} // class PpsWpfShell

	#endregion
}
