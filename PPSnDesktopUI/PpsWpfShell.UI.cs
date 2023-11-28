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
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Neo.IronLua;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;
using TecWare.PPSn.UI;

namespace TecWare.PPSn
{
	#region -- class PpsColorThemeInfo ------------------------------------------------

	/// <summary>Interface for the pane enumeration</summary>
	public interface IPpsColorThemeInfo
	{
		/// <summary>Apply the theme.</summary>
		/// <returns></returns>
		void Apply();

		/// <summary>Name of the theme.</summary>
		string Name { get; }
		/// <summary>DisplayName</summary>
		string DisplayName { get; }
	} // class IPpsColorThemeInfo

	#endregion

	#region -- interface IPpsWpfResources ---------------------------------------------

	/// <summary>Resource helper</summary>
	public interface IPpsWpfResources
	{
		/// <summary>Append external resources.</summary>
		/// <param name="uri"></param>
		/// <returns></returns>
		ResourceDictionary AppendResourceDictionary(Uri uri);

		/// <summary>Try to locate a resource</summary>
		/// <typeparam name="TKEY"></typeparam>
		/// <typeparam name="T"></typeparam>
		/// <param name="predicate"></param>
		/// <returns></returns>
		IEnumerable<T> FindResourceByKey<TKEY, T>(Predicate<TKEY> predicate = null)
			where TKEY : ResourceKey;

		/// <summary>Try to locate a resource</summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="resourceKey"></param>
		/// <returns></returns>
		T FindResource<T>(object resourceKey)
			where T : class;

		/// <summary>Enumearte all themes</summary>
		/// <returns></returns>
		IEnumerable<IPpsColorThemeInfo> GetThemes();

		/// <summary>Return all main resources</summary>
		ResourceDictionary Resources { get; }
		/// <summary>Return a template selector for wpf resources</summary>
		DataTemplateSelector DefaultDataTemplateSelector { get; }

		/// <summary>Change the current theme</summary>
		PpsColorTheme CurrentTheme { get; set; }
	} // interface IPpsWpfResources

	#endregion

	#region -- class PpsWpfShell ------------------------------------------------------

	public static partial class PpsWpfShell
	{
		#region -- class DefaultStaticDataTemplateSelector ----------------------------

		private sealed class DefaultStaticDataTemplateSelector : DataTemplateSelector
		{
			public DefaultStaticDataTemplateSelector()
			{
			} // ctor

			public override DataTemplate SelectTemplate(object item, DependencyObject container)
				=> FindTemplate(container, item);
		} // class DefaultStaticDataTemplateSelector

		#endregion

		#region -- FindResource, LoadResource -----------------------------------------

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
		/// <typeparam name="TKEY">Key of the resource.</typeparam>
		/// <typeparam name="T">Type of the resource.</typeparam>
		/// <param name="resourceDictionary"></param>
		/// <param name="predicate"></param>
		/// <returns></returns>
		public static IEnumerable<(TKEY key, T resource)> FindResourceAndKeyByKey<TKEY, T>(this ResourceDictionary resourceDictionary, Predicate<TKEY> predicate = null)
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
						yield return (key, v);
					}
				}

				current = dictionaryStack.Count > 0 ? dictionaryStack.Pop() : null;
			}
		} // func FindResourceAndKeyByKey

		public static IEnumerable<T> FindResourceByKey<TKEY, T>(this ResourceDictionary resourceDictionary, Predicate<TKEY> predicate = null)
			where TKEY : ResourceKey
			=> FindResourceAndKeyByKey<TKEY, T>(resourceDictionary, predicate).Select(c => c.resource);

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

		public static bool TryFindResource<T>(this IPpsShell shell, object resourceKey, out T resource)
			where T : class
		{
			resource = FindResource<T>(shell, resourceKey);
			return resource != null;
		} // func TryFindResource

		private static bool TryFindResource(FrameworkElement fe, object resourceKey, out object resource)
		{
			resource = fe.TryFindResource(resourceKey);
			return resource != null;
		} // func TryFindResource

		private static bool TryFindResource(FrameworkContentElement fce, object resourceKey, out object resource)
		{
			resource = fce.TryFindResource(resourceKey);
			return resource != null;
		} // func TryFindResource

		public static T FindResource<T>(this DependencyObject d, object resourceKey)
		{
			if (d is FrameworkElement fe && TryFindResource(fe, resourceKey, out var resource))
				return (T)resource;
			else if (d is FrameworkContentElement fce && TryFindResource(fce, resourceKey, out resource))
				return (T)resource;
			else
				return default;
		} // func FindResource

		public static bool TryFindResource<T>(this DependencyObject d, object resourceKey, out T resource)
			where T : class
		{
			resource = FindResource<T>(d, resourceKey);
			return resource != null;
		} // func TryFindResource

		public static DataTemplate FindTemplate(this DependencyObject container, object item)
		{
			// find by type
			if (item is LuaTable t)
			{
				var key = t.GetMemberValue("Typ");
				if (key != null && TryFindResource<DataTemplate>(container, key, out var resource))
					return resource;
			}
			else if (item is PpsLiveDataRow) // && TryFindResource<DataTemplate>(container, new ComponentResourceKey(item.GetType(), item.GetType()), out var resource))
			{
				var resource = (DataTemplate)Application.Current.FindResource(item.GetType()) ;
				return resource;
			}
			
			return null;
		} // func FindTemplate

		/// <summary>Load a resource dictionary from a xaml-source.</summary>
		/// <param name="type"></param>
		/// <param name="relativePath"></param>
		/// <returns></returns>
		public static ResourceDictionary LoadResources(Type type, string relativePath)
			=> new ResourceDictionary { Source = GetResourceUri(type, relativePath) };

		/// <summary>Create a application resource uri for the resource from an assembly.</summary>
		/// <param name="assembly"></param>
		/// <param name="relativePath"></param>
		/// <returns></returns>
		public static Uri GetResourceUri(Assembly assembly, string relativePath)
			=> new Uri($"pack://application:,,,/{assembly.GetName().Name};component/" + relativePath);

		/// <summary>Create a application resource uri for the resource from an assembly.</summary>
		/// <param name="type"></param>
		/// <param name="relativePath"></param>
		/// <returns></returns>
		public static Uri GetResourceUri(Type type, string relativePath)
			=> GetResourceUri(type.Assembly, relativePath);

		/// <summary>Create a application resource uri for the resource from an assembly. And also checks for existens.</summary>
		/// <param name="assembly"></param>
		/// <param name="relativePath"></param>
		/// <param name="uri"></param>
		/// <returns></returns>
		public static bool TryGetResourceUri(Assembly assembly, string relativePath, out Uri uri)
		{
			uri = null;

			var resName = Uri.EscapeUriString(relativePath.ToLowerInvariant());
			if (resName.EndsWith(".xaml"))
				resName = resName.Substring(0, resName.Length - 4) + "baml";

			using (var src = assembly.GetManifestResourceStream(assembly.GetName().Name + ".g.resources"))
			{
				if (src != null)
				{
					using (var res = new ResourceReader(src))
					{
						// search for themes/styles
						if (res.Cast<DictionaryEntry>().Select(c => c.Key).OfType<string>().FirstOrDefault(c => c == resName) != null)
							uri = GetResourceUri(assembly, relativePath);
					}
				}
			}

			return uri != null;
		} // func TryGetResourceUri

		#endregion

		#region -- Media/Drawing Color ------------------------------------------------

		public static System.Drawing.Color ToDrawingColor(this Color color)
			=> System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);

		public static Color ToMediaColor(this System.Drawing.Color color)
			=> Color.FromArgb(color.A, color.R, color.G, color.B);

		#endregion

		#region -- ToGeometryName -----------------------------------------------------

		public static string ToGeometryName(this PpsImage image)
		{
			switch (image)
			{
				case PpsImage.Information:
				case PpsImage.Question:
					return "informationPathGeometry";
				case PpsImage.Error:
					return "exceptionPathGeometry";
				case PpsImage.Warning:
					return "warningPathGeometry";
				default:
					return null;
			}
		} // proc ToGeometryName

		#endregion

		#region -- ToTitle ------------------------------------------------------------

		public static string ToTitle(this PpsImage image)
		{
			switch (image)
			{
				case PpsImage.Information:
					return "Information";
				case PpsImage.Question:
					return "Frage";
				case PpsImage.Error:
					return "Fehler";
				case PpsImage.Warning:
					return "Warnung";
				default:
					return null;
			}
		} // proc ToTitle

		#endregion

		#region -- GetWindowRect ------------------------------------------------------

		/// <summary></summary>
		/// <param name="w"></param>
		/// <returns></returns>
		public static Rect GetWindowRect(this Window w)
		{
			var wi = new WINDOWINFO();
			NativeMethods.GetWindowInfo(new WindowInteropHelper(w).Handle, wi);
			var source = PresentationSource.FromVisual(w);
			var pts = new Point[]
			{
				new Point(wi.rcWindow.Left, wi.rcWindow.Top),
				new Point(wi.rcWindow.Right, wi.rcWindow.Bottom)
			};

			source.CompositionTarget.TransformFromDevice.Transform(pts);

			return new Rect(pts[0], pts[1]);
		} // func GetWindowRect

		#endregion

		/// <summary>Template selection, that redirects to the GetDataTemplate function.</summary>
		public static DataTemplateSelector DataTemplateSelector => new DefaultStaticDataTemplateSelector();
	} // class PpsWpfShell
	

	#endregion
}
