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
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using TecWare.DE.Stuff;

namespace TecWare.PPSn.UI
{
	#region -- enum ExceptionShowFlags ------------------------------------------------

	/// <summary>Propose of the exception.</summary>
	[Flags]
	public enum PpsExceptionShowFlags
	{
		/// <summary>No hint</summary>
		None = 0,
		/// <summary>Start the shutdown of the application.</summary>
		Shutown = 1,
		/// <summary>Do not show any user message.</summary>
		Background = 4,
		/// <summary>Classify the exception as warning</summary>
		Warning = 8
	} // enum ExceptionShowFlags

	#endregion

	#region -- enum PpsImage ----------------------------------------------------------

	/// <summary>Image collection.</summary>
	public enum PpsImage
	{
		/// <summary>No image.</summary>
		None,
		/// <summary>Information</summary>
		Information,
		/// <summary>Warning</summary>
		Warning,
		/// <summary>Error</summary>
		Error,
		/// <summary>Question</summary>
		Question
	} // enum PpsImage

	#endregion

	#region -- class HsvColor ---------------------------------------------------------

	/// <summary>Hue, Saturation, Light color</summary>
	[DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
	public struct HsvColor : IComparable, IComparable<HsvColor>
	{
		private readonly byte a;
		private readonly short h;
		private readonly byte s;
		private readonly byte v;

		#region -- Ctor/Dtor ----------------------------------------------------------

		private HsvColor(byte a, short h, byte s, byte v)
		{
			this.a = a;
			this.h = h;
			this.s = s;
			this.v = v;
		} // ctor

		private string GetDebuggerDisplay()
			=> $"a={a};h={h};s={s};v={v}";

		#endregion

		#region -- Equals, Compare ----------------------------------------------------

		/// <summary>Get the int representation and build get the hashcode</summary>
		/// <returns></returns>
		public override int GetHashCode()
			=> ToInt32().GetHashCode();

		/// <summary></summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		public override bool Equals(object obj)
			=> obj is HsvColor hsl && CompareTo(hsl) == 0;

		/// <summary></summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		public int CompareTo(object obj)
			=> obj is HsvColor hsl ? CompareTo(hsl) : -1;

		/// <summary></summary>
		/// <param name="other"></param>
		/// <returns></returns>
		public int CompareTo(HsvColor other)
		{
			var r = h.CompareTo(other.h);
			if (r == 0)
				r = s.CompareTo(other.s);
			if (r == 0)
				r = v.CompareTo(other.v);
			return r;
		} // func CompareTo

		#endregion

		#region -- ToInt32, ToColor ---------------------------------------------------

		/// <summary>Create a 32bit value for the hsv.</summary>
		/// <returns></returns>
		public int ToInt32()
			=> unchecked((int)((uint)a << 24 | (uint)(h & 0x1FF) << 15 | (uint)(s & 0x7F) << 8 | v));

		/// <summary>Convert to rgb-color</summary>
		/// <param name="r"></param>
		/// <param name="g"></param>
		/// <param name="b"></param>
		public void ToRgb(out byte r, out byte g, out byte b)
		{
			if (s == 0)
			{
				r = g = b = v;
			}
			else
			{
				var i = Math.DivRem(h, 60, out var fraction);
				var p = Convert.ToByte(v * (100 - s) / 100);
				var q = Convert.ToByte(v * (6000 - s * fraction) / 6000);
				var t = Convert.ToByte(v * (6000 - s * (60 - fraction)) / 6000);

				switch (i)
				{
					case 1:
						r = q;
						g = v;
						b = p;
						break;
					case 2:
						r = p;
						g = v;
						b = t;
						break;
					case 3:
						r = p;
						g = q;
						b = v;
						break;
					case 4:
						r = t;
						g = p;
						b = v;
						break;
					case 5:
						r = v;
						g = p;
						b = q;
						break;
					default: // 0
						r = v;
						g = t;
						b = p;
						break;
				}
			}
		} // proc ToArgb

		/// <summary>Convert to argb-color</summary>
		/// <returns></returns>
		public int ToArgb()
		{
			ToRgb(out var r, out var g, out var b);
			return unchecked((a << 24) | (r << 16) | (g << 8) | b);
		} // func ToColor

		#endregion

		/// <summary>Alpha value 0..255</summary>
		public byte Alpha => a;
		/// <summary>Color 0..360</summary>
		public short Hue => h;
		/// <summary>Saturation 0..100</summary>
		public byte Saturation => s;
		/// <summary>Light/Brightness (0 is dark, 255 is light)</summary>
		public byte Light => v;

		#region -- FromColor, FromAhsv ------------------------------------------------

		/// <summary>Create a HsvColor</summary>
		/// <param name="h">Color 0..360</param>
		/// <param name="s">Saturation 0..100</param>
		/// <param name="v">Light/Brightness (0 is dark, 255 is light)</param>
		/// <returns>Hsv color</returns>
		public static HsvColor FromHsv(short h, byte s, byte v)
			=> FromAhsv(Byte.MaxValue, h, s, v);

		/// <summary>Create a HsvColor</summary>
		/// <param name="a">Alpha value 0..255</param>
		/// <param name="h">Color 0..360</param>
		/// <param name="s">Saturation 0..100</param>
		/// <param name="v">Light/Brightness (0 is dark, 255 is light)</param>
		/// <returns>Hsv color</returns>
		public static HsvColor FromAhsv(byte a, short h, byte s, byte v)
			=> new HsvColor(a, h > 360 ? (short)360 : h, s > 100 ? (byte)100 : s, v);

		/// <summary>Create hsv-color from int.</summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static HsvColor FromInt32(int value)
		{
			unchecked {
				return FromAhsv(
					(byte)(value >> 24),
					(short)((value >> 15) & 0x1FF),
					(byte)((value >> 8) & 0x7F),
					(byte)value
				);
			}
		} // func FromInt32

		/// <summary>Convert from argb to <c>HsvColor</c>.</summary>
		/// <param name="a">alpha</param>
		/// <param name="r">red</param>
		/// <param name="g">green</param>
		/// <param name="b">blue</param>
		/// <returns></returns>
		public static HsvColor FromArgb(byte a, byte r, byte g, byte b)
		{
			int h;
			int s;
			int v;

			var max = Math.Max(Math.Max(r, g), b);
			var min = Math.Min(Math.Min(r, g), b);

			if (max == min)
				h = 0;
			else
			{
				var diff = max - min;

				if (max == r)
					h = 0 + (g - b) * 60 / diff;
				else if (max == g)
					h = 120 + (b - r) * 60 / diff;
				else
					h = 240 + (r - g) * 60 / diff;

				if (h < 0)
					h += 360;
				if (h > 360)
					h -= 360;
			}

			s = max == 0 ? 0 : (max - min) * 100 / max;

			v = max;

			return FromAhsv(a, (short)h, (byte)s, (byte)v);
		} // func FromArgbCore

		/// <summary>Convert from argb to <c>HsvColor</c>.</summary>
		/// <param name="a">alpha</param>
		/// <param name="r">red</param>
		/// <param name="g">green</param>
		/// <param name="b">blue</param>
		/// <returns></returns>
		public static HsvColor FromArgb(int a, int r, int g, int b)
			=> unchecked(FromArgb((byte)a, (byte)r, (byte)g, (byte)b));

		/// <summary>Convert from rgb to <c>HsvColor</c>.</summary>
		/// <param name="r">red</param>
		/// <param name="g">green</param>
		/// <param name="b">blue</param>
		/// <returns></returns>
		public static HsvColor FromArgb(int r, int g, int b)
			=> FromArgb(255, r, g, b);

		/// <summary>Convert from argb to <c>HsvColor</c>.</summary>
		/// <param name="argb"></param>
		/// <returns></returns>
		public static HsvColor FromArgb(int argb)
			=> FromArgb(argb >> 24, argb >> 16, argb>>8, argb);

		#endregion

		#region -- operator - overload ------------------------------------------------

		/// <summary></summary>
		/// <param name="left"></param>
		/// <param name="right"></param>
		/// <returns></returns>
		public static bool operator ==(HsvColor left, HsvColor right)
			=> left.Equals(right);

		/// <summary></summary>
		/// <param name="left"></param>
		/// <param name="right"></param>
		/// <returns></returns>
		public static bool operator !=(HsvColor left, HsvColor right)
			=> !(left == right);

		/// <summary></summary>
		/// <param name="left"></param>
		/// <param name="right"></param>
		/// <returns></returns>
		public static bool operator <(HsvColor left, HsvColor right)
			=> left.CompareTo(right) < 0;

		/// <summary></summary>
		/// <param name="left"></param>
		/// <param name="right"></param>
		/// <returns></returns>
		public static bool operator <=(HsvColor left, HsvColor right)
			=> left.CompareTo(right) <= 0;

		/// <summary></summary>
		/// <param name="left"></param>
		/// <param name="right"></param>
		/// <returns></returns>
		public static bool operator >(HsvColor left, HsvColor right)
			=> left.CompareTo(right) > 0;

		/// <summary></summary>
		/// <param name="left"></param>
		/// <param name="right"></param>
		/// <returns></returns>
		public static bool operator >=(HsvColor left, HsvColor right)
			=> left.CompareTo(right) >= 0;

		#endregion
	} // struct HsvColor

	#endregion

	#region -- interface IPpsUIService ------------------------------------------------

	/// <summary>UI helper</summary>
	public interface IPpsUIService
	{
		/// <summary></summary>
		/// <param name="flags"></param>
		/// <param name="exception"></param>
		/// <param name="alternativeMessage"></param>
		void ShowException(PpsExceptionShowFlags flags, Exception exception, string alternativeMessage = null);

		/// <summary>Display a simple messagebox</summary>
		/// <param name="text"></param>
		/// <param name="image"></param>
		/// <param name="buttons"></param>
		/// <returns></returns>
		int MsgBox(string text, PpsImage image = PpsImage.Information, params string[] buttons);

		/// <summary>Run this action in ui-thread.</summary>
		/// <param name="action"></param>
		Task RunUI(Action action);

		/// <summary>Run this action in ui-thread.</summary>
		/// <param name="action"></param>
		Task<T> RunUI<T>(Func<T> action);

		/// <summary>Predefined buttons</summary>
		string[] Ok { get; }
		/// <summary>Predefined buttons</summary>
		string[] YesNo { get; }
		/// <summary>Predfined buttons</summary>
		string[] OkCancel { get; }
	} // interface IPpsUIService

	#endregion

	#region -- interface PpsUI --------------------------------------------------------

	public static partial class PpsUI
	{
		/// <summary>Show a exception.</summary>
		/// <param name="ui"></param>
		/// <param name="exception"></param>
		/// <param name="alternativeMessage"></param>
		public static void ShowException(this IPpsUIService ui, Exception exception, string alternativeMessage = null)
			=> ui.ShowException(PpsExceptionShowFlags.None, exception, alternativeMessage);

		/// <summary>Show a exception.</summary>
		/// <param name="sp"></param>
		/// <param name="exception"></param>
		/// <param name="alternativeMessage"></param>
		public static void ShowException(this IServiceProvider sp, Exception exception, string alternativeMessage = null)
			=> ShowException(sp.GetService<IPpsUIService>(true), exception, alternativeMessage);

		/// <summary>Show a exception.</summary>
		/// <param name="ui"></param>
		/// <param name="background"></param>
		/// <param name="exception"></param>
		/// <param name="alternativeMessage"></param>
		/// <returns></returns>
		public static Task ShowExceptionAsync(this IPpsUIService ui, bool background, Exception exception, string alternativeMessage = null)
			=> ui.RunUI(() => ui.ShowException(background ? PpsExceptionShowFlags.Background : PpsExceptionShowFlags.None, exception, alternativeMessage));

		/// <summary>Show a exception.</summary>
		/// <param name="sp"></param>
		/// <param name="background"></param>
		/// <param name="exception"></param>
		/// <param name="alternativeMessage"></param>
		/// <returns></returns>
		public static Task ShowExceptionAsync(this IServiceProvider sp, bool background, Exception exception, string alternativeMessage = null)
			=> ShowExceptionAsync(sp.GetService<IPpsUIService>(true), background, exception, alternativeMessage);

		/// <summary>Run a task with progress information.</summary>
		/// <param name="sp"></param>
		/// <param name="taskText"></param>
		/// <param name="action"></param>
		/// <returns></returns>
		public static async Task RunTaskAsync(this IServiceProvider sp, string taskText, Func<IPpsProgress,Task> action)
		{
			using (var p = sp.CreateProgress(true, taskText))
				await action(p);
		} // proc RunTaskAsync

		#region -- HsvColor - converter -----------------------------------------------

		/// <summary><see cref="Color"/> to <see cref="HsvColor"/></summary>
		/// <param name="color"></param>
		/// <returns></returns>
		public static HsvColor ToHsvColor(this Color color)
			=> HsvColor.FromArgb(color.ToArgb());

		/// <summary><see cref="HsvColor"/> to <see cref="Color"/></summary>
		/// <param name="color"></param>
		/// <returns></returns>
		public static Color ToDrawingColor(this HsvColor color)
			=> Color.FromArgb(color.ToArgb());

		#endregion
	} // class PpsUI

	#endregion
}
