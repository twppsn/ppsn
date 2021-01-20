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
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using TecWare.PPSn.UI;
using static TecWare.PPSn.NativeMethods;

namespace TecWare.PPSn.Controls
{
	#region -- enum PpsVirtualKeyboardLayout ------------------------------------------

	public enum PpsVirtualKeyboardLayout
	{
		Alpha,
		Symbols,
		NumberPositive,
		NumberNegative
	} // enum PpsVirtualKeyboardLayout

	#endregion

	#region -- enum PpsVirtualKeyboardButtonName --------------------------------------

	public enum PpsVirtualKeyName
	{
		None,
		Back,
		Return,
		Shift,
		Delete,
		Clear,

		CapsLock,

		Symbols,
		Space,
		Left,
		Right,
		Pos1,
		End
	} // enum PpsVirtualKeyboardButtonName

	#endregion

	#region -- enum PpsKeySend --------------------------------------------------------

	[Flags]
	internal enum PpsKeySend
	{
		None = 0,
		Up = 1,
		Down = 2,
		Both = 3
	} // enum PpsKeySend

	#endregion

	#region -- enum PpsKeyModifiers ---------------------------------------------------

	[Flags]
	internal enum PpsKeyModifiers : int
	{
		None = 0,
		Alt = 1,
		Control = 2,
		Shift = 4,
		Win = 8,
	} // enum PpsKeyModifiers

	#endregion

	#region -- struct PpsKey ----------------------------------------------------------

	internal struct PpsKey
	{
		public PpsKeyModifiers Modifiers;
		public ushort VirtualKey;

		public bool IsEmpty => VirtualKey == 0;

		public static bool IsKey(char c)
		{
			var scan = VkKeyScan(c);
			return scan > 0 && scan < 0xFFFF;
		} // func IsKey

		public static PpsKey Get(char c)
		{
			TryGet(c, out var k);
			return k;
		} // func Get

		public static bool TryGet(char c, out PpsKey key)
		{
			var scan = VkKeyScan(c);
			if (scan > 0 && scan < 0xFFFF)
			{
				// update modifiers
				var modifiers = PpsKeyModifiers.None;
				if ((scan & 0x100) == 0x100)
					modifiers |= PpsKeyModifiers.Shift;
				if ((scan & 0x200) == 0x200)
					modifiers |= PpsKeyModifiers.Control;
				if ((scan & 0x400) == 0x400)
					modifiers |= PpsKeyModifiers.Alt;

				key = new PpsKey { Modifiers = modifiers, VirtualKey = (ushort)(scan & 0xFF) };
				return true;
			}
			else
			{
				key = Empty;
				return false;
			}
		} // func TryGet

		public static PpsKey Get(ushort vk)
			=> new PpsKey { Modifiers = PpsKeyModifiers.None, VirtualKey = vk };

		public static PpsKey Get(PpsKeyModifiers modifiers, ushort vk)
			=> new PpsKey { Modifiers = modifiers, VirtualKey = vk };

		public static PpsKey Empty { get; } = new PpsKey();
	} // struct PpsKey

	#endregion

	#region -- class PpsVirtualKeyboard -----------------------------------------------

	public class PpsVirtualKeyboard : Control
	{
		#region -- class KeyButton ----------------------------------------------------

		private sealed class KeyButton
		{
			private readonly PpsVirtualKeyboard vk;
			private readonly ButtonBase button;
			private readonly PpsVirtualKeyName buttonName;
			private readonly string key;
			private readonly string keyShifted;

			public KeyButton(PpsVirtualKeyboard vk, ButtonBase button)
			{
				this.vk = vk ?? throw new ArgumentNullException(nameof(vk));
				this.button = button ?? throw new ArgumentNullException(nameof(button));

				// get optional button name
				buttonName = GetKeyName(button);

				// get keystrokes
				key = GetKey(button);
				keyShifted = GetKeyShifted(button);

				// attach to mode
				button.AddHandler(MouseLeftButtonDownEvent, new MouseButtonEventHandler(Button_MouseLeftButtonDown), true);
				button.AddHandler(MouseLeftButtonUpEvent, new MouseButtonEventHandler(Button_MouseLeftButtonUp), true);
				button.AddHandler(TouchDownEvent, new EventHandler<TouchEventArgs>(Button_TouchDown), true);
				button.AddHandler(TouchUpEvent, new EventHandler<TouchEventArgs>(Button_TouchUp), true);
			} // ctor

			public void UpdateButton()
			{
				if (!vk.UpdateButtonContent(this))
				{
					if (vk.IsShifted && keyShifted != null)
						button.Content = keyShifted;
					else
						button.Content = key;
				}
			} // proc UpdateButton

			private void StartSendKey()
			{
				var k = vk.GetButtonKey(this); // get hard binded button for the key

				if (!k.HasValue) // get default binding for the button
				{
					var keyString = vk.IsShifted ? (keyShifted ?? key) : key;
					if (keyString == null || keyString.Length > 1 || !PpsKey.TryGet(keyString[0], out var singleKey))
						vk.SendKeyString(this, keyString); // execute key on mouse down only
					else
						k = singleKey;
				}

				if (k.HasValue)
				{
					if (k.Value.IsEmpty)
						vk.SendKeyString(this, null);
					else
						vk.StartSendKey(k.Value);
				}
				else
					vk.StopSendKey();
			} // proc StartSendKey

			private void Button_TouchDown(object sender, TouchEventArgs e)
				=> StartSendKey();

			private void Button_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
			{
				if (e.StylusDevice == null) // is not generate from an touch event
					StartSendKey();
			} // event Button_MouseLeftButtonDown

			private void Button_TouchUp(object sender, TouchEventArgs e)
				=> vk.StopSendKey();
			
			private void Button_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
			{
				if (e.StylusDevice == null) // is not generate from an touch event
					vk.StopSendKey();
			} // event Button_MouseLeftButtonUp

			public PpsVirtualKeyName Name => buttonName;
			public ButtonBase Button => button;

			public string Key => key;
			public string KeyShifted => keyShifted;

			public bool IsValid => buttonName != PpsVirtualKeyName.None || key != null;
		} // class KeyButton

		#endregion

		private readonly RoutedEventHandler onControlGotFocusDelegate;
		private readonly RoutedEventHandler onControlLostFocusDelegate;

		private KeyButton[] buttons = null;
		private UIElement controlAttached = null;

		private readonly DispatcherTimer keyDownTimer;
		private readonly TimeSpan keyboardDelay;
		private readonly TimeSpan keyboardSpeed;
		private PpsKey? currentKey = null;

		#region -- Ctor/Dtor ----------------------------------------------------------

		public PpsVirtualKeyboard()
		{
			onControlGotFocusDelegate = new RoutedEventHandler(OnControlGotFocus);
			onControlLostFocusDelegate = new RoutedEventHandler(OnControlLostFocus);

			// timer
			keyboardDelay = TimeSpan.FromMilliseconds(SystemParameters.KeyboardDelay * 250);
			keyboardSpeed = TimeSpan.FromMilliseconds((32 - SystemParameters.KeyboardSpeed) * 32);
			keyDownTimer = new DispatcherTimer(keyboardDelay, DispatcherPriority.Normal, KeyDownTimer_Tick, Dispatcher);
		} // ctor

		public override void OnApplyTemplate()
		{
			base.OnApplyTemplate();

			buttons = PpsWpfShell.GetVisualChildren<ButtonBase>(this)
				.Select(c => new KeyButton(this, c))
				.Where(c => c.IsValid).ToArray();

			OnKeyboardChanged();
		} // proc OnApplyTemplate

		#endregion

		#region -- Attach - focus -----------------------------------------------------

		public void Attach(UIElement control)
		{
			if (controlAttached != null)
			{
				controlAttached.RemoveHandler(FocusManager.GotFocusEvent, onControlGotFocusDelegate);
				controlAttached.RemoveHandler(FocusManager.LostFocusEvent, onControlLostFocusDelegate);
			}

			controlAttached = control;

			if (controlAttached != null)
			{
				controlAttached.AddHandler(FocusManager.GotFocusEvent, onControlGotFocusDelegate, true);
				controlAttached.AddHandler(FocusManager.LostFocusEvent, onControlLostFocusDelegate, true);
			}
		} // proc Attach

		private void OnControlGotFocus(object sender, RoutedEventArgs e)
		{
			if (e.OriginalSource is PpsTextBox ppsText)
			{
				switch (ppsText.InputType)
				{
					case PpsTextBoxInputType.Decimal:
					case PpsTextBoxInputType.Integer:
						Show(PpsVirtualKeyboardLayout.NumberPositive);
						break;
					case PpsTextBoxInputType.DecimalNegative:
					case PpsTextBoxInputType.IntegerNegative:
						Show(PpsVirtualKeyboardLayout.NumberNegative);
						break;
					default:
						Show(PpsVirtualKeyboardLayout.Alpha);
						break;
				}
			}
			else if (e.OriginalSource is TextBoxBase)
			{
				Show(PpsVirtualKeyboardLayout.Alpha);
			}
		} // event OnControlGotFocus

		private void OnControlLostFocus(object sender, RoutedEventArgs e)
			=> Hide();

		#endregion

		#region -- Keyboard - layout --------------------------------------------------

		private void OnKeyboardChanged()
		{
			if (buttons == null)
				return;

			foreach (var b in buttons)
				b.UpdateButton();
		} // proc OnKeyboardChanged

		private bool UpdateButtonContent(KeyButton keyButton)
		{
			switch (keyButton.Name)
			{
				// using images
				case PpsVirtualKeyName.Return:
				case PpsVirtualKeyName.CapsLock:
				case PpsVirtualKeyName.Shift:
				case PpsVirtualKeyName.Back:
				case PpsVirtualKeyName.Left:
				case PpsVirtualKeyName.Right:
				case PpsVirtualKeyName.Space:
					return true;
				case PpsVirtualKeyName.Delete:
					keyButton.Button.Content = "Del";
					return true;
				case PpsVirtualKeyName.Clear:
					keyButton.Button.Content = "Clear";
					return true;
				case PpsVirtualKeyName.Pos1:
					keyButton.Button.Content = "Pos1";
					return true;
				case PpsVirtualKeyName.End:
					keyButton.Button.Content = "End";
					return true;
				case PpsVirtualKeyName.Symbols:
					keyButton.Button.Content = Layout == PpsVirtualKeyboardLayout.Alpha ? "&123" : "abc";
					return true;
				default:
					return false;
			}
		} // func UpdateButtonContent

		private PpsKey? GetButtonKey(KeyButton keyButton)
		{
			switch (keyButton.Name)
			{
				case PpsVirtualKeyName.Back:
					return PpsKey.Get(0x08);
				case PpsVirtualKeyName.Delete:
					return PpsKey.Get(0x2E);
				case PpsVirtualKeyName.Space:
					return PpsKey.Get(0x20);
				case PpsVirtualKeyName.Left:
					return PpsKey.Get(0x25);
				case PpsVirtualKeyName.Right:
					return PpsKey.Get(0x27);

				case PpsVirtualKeyName.Shift:
				case PpsVirtualKeyName.CapsLock:
				case PpsVirtualKeyName.Clear:
				case PpsVirtualKeyName.Return:
				case PpsVirtualKeyName.Symbols:
				case PpsVirtualKeyName.Pos1:
				case PpsVirtualKeyName.End:
					return PpsKey.Empty;

				default:
					return null;
			}
		} // func GetButtonKey

		public void Show(PpsVirtualKeyboardLayout layout)
		{
			SetValue(layoutPropertyKey, layout);
			Visibility = Visibility.Visible;
		} // proc Show

		public void Hide()
			=> Visibility = Visibility.Collapsed;

		#endregion

		#region -- Send Key -----------------------------------------------------------

		private void SendKeyString(KeyButton button, string keyString)
		{
			switch (button.Name)
			{
				case PpsVirtualKeyName.Shift:
					SetShiftState(!IsShifted, false);
					break;
				case PpsVirtualKeyName.CapsLock:
					if (IsCapsLocked)
						SetShiftState(false, false);
					else
						SetShiftState(true, true);
					break;
				case PpsVirtualKeyName.Return:
					SendSingleKey(PpsKey.Get(0x0D));
					ResetShiftState();
					break;
				case PpsVirtualKeyName.Symbols:
					if (Layout == PpsVirtualKeyboardLayout.Alpha)
						SetValue(layoutPropertyKey, PpsVirtualKeyboardLayout.Symbols);
					else if(Layout == PpsVirtualKeyboardLayout.Symbols)
						SetValue(layoutPropertyKey, PpsVirtualKeyboardLayout.Alpha);
					UpdateButtonContent(button);
					break;
				case PpsVirtualKeyName.Pos1:
					SendSingleKey(PpsKey.Get(0x24));
					ResetShiftState();
					break;
				case PpsVirtualKeyName.End:
					SendSingleKey(PpsKey.Get(0x23));
					ResetShiftState();
					break;
				case PpsVirtualKeyName.Clear:
					{
						var g = new PpsKeyInputGenerator();
						g.AppendKey(PpsKey.Get(PpsKeyModifiers.Control, 0x41), PpsKeySend.Both);
						g.AppendKey(PpsKey.Get(0x2E), PpsKeySend.Both);
						g.Send();
					}
					break;
				case PpsVirtualKeyName.None:
					if (keyString != null)
					{
						var g = new PpsKeyInputGenerator();
						foreach (var c in keyString)
							g.Append(c);
						g.Send();
					}
					ResetShiftState();
					break;
			}
		} // proc SendKeyString

		private void SendSingleKey(PpsKey key)
		{
			StopSendKey();

			var g = new PpsKeyInputGenerator();
			g.AppendKey(key, PpsKeySend.Both);
			g.Send();
		} // proc SendSingle

		private void SendKeyDown()
		{
			var g = new PpsKeyInputGenerator();
			g.AppendKey(currentKey.Value, PpsKeySend.Down);
			g.Send();
		} // proc SendKeyDown

		private void StartSendKey(PpsKey key)
		{
			if (currentKey.HasValue)
				StopSendKey();

			currentKey = key;
			SendKeyDown();
			keyDownTimer.Interval = keyboardDelay;
			keyDownTimer.Start();
		} // proc StartSendKey

		private void StopSendKey()
		{
			keyDownTimer.Stop();
			if (currentKey.HasValue)
			{
				var g = new PpsKeyInputGenerator();
				g.AppendKey(currentKey.Value, PpsKeySend.Up);
				g.Send();
				currentKey = null;
				ResetShiftState();
			}
		} // proc StopSendKey

		private void KeyDownTimer_Tick(object sender, EventArgs e)
		{
			if (currentKey.HasValue)
			{
				SendKeyDown();
				if (keyDownTimer.Interval == keyboardDelay)
					keyDownTimer.Interval = keyboardSpeed;
			}
			else
				keyDownTimer.Stop();
		} // event KeyDownTimer_Tick

		#endregion

		#region -- IsShifted - property -----------------------------------------------

		private void SetShiftState(bool isShifted, bool locked)
		{
			SetValue(isShiftedPropertyKey, BooleanBox.GetObject(isShifted));
			SetValue(isCapsLockedPropertyKey, BooleanBox.GetObject(locked));
		} // proc SetShiftState

		private void ResetShiftState()
		{
			if (!IsCapsLocked)
				SetValue(isShiftedPropertyKey, BooleanBox.False);
		} // proc ResetShiftState

		private static readonly DependencyPropertyKey isShiftedPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsShifted), typeof(bool), typeof(PpsVirtualKeyboard), new FrameworkPropertyMetadata(BooleanBox.False, new PropertyChangedCallback(OnIsShiftedChanged)));

		private static void OnIsShiftedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsVirtualKeyboard)d).OnKeyboardChanged();

		private static readonly DependencyPropertyKey isCapsLockedPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsCapsLocked), typeof(bool), typeof(PpsVirtualKeyboard), new FrameworkPropertyMetadata(BooleanBox.False));
		public static readonly DependencyProperty IsShiftedProperty = isShiftedPropertyKey.DependencyProperty;
		public static readonly DependencyProperty IsCapsLockedProperty = isCapsLockedPropertyKey.DependencyProperty;

		public bool IsShifted => BooleanBox.GetBool(GetValue(IsShiftedProperty));
		public bool IsCapsLocked => BooleanBox.GetBool(GetValue(IsCapsLockedProperty));

		#endregion

		#region -- Layout - property --------------------------------------------------

		private static readonly DependencyPropertyKey layoutPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Layout), typeof(PpsVirtualKeyboardLayout), typeof(PpsVirtualKeyboard), new FrameworkPropertyMetadata(PpsVirtualKeyboardLayout.Alpha));
		public static readonly DependencyProperty LayoutProperty = layoutPropertyKey.DependencyProperty;

		public PpsVirtualKeyboardLayout Layout => (PpsVirtualKeyboardLayout)GetValue(LayoutProperty);

		#endregion

		#region -- ButtonName - property ----------------------------------------------

		public static readonly DependencyProperty KeyNameProperty = DependencyProperty.RegisterAttached("KeyName", typeof(PpsVirtualKeyName), typeof(PpsVirtualKeyboard), new FrameworkPropertyMetadata(PpsVirtualKeyName.None));

		public static void SetKeyName(DependencyObject dp, PpsVirtualKeyName value)
			=> dp.SetValue(KeyNameProperty, value);

		public static PpsVirtualKeyName GetKeyName(DependencyObject dp)
			=> (PpsVirtualKeyName)dp.GetValue(KeyNameProperty);

		#endregion

		#region -- Key - property -----------------------------------------------------

		public static readonly DependencyProperty KeyProperty = DependencyProperty.RegisterAttached("Key", typeof(string), typeof(PpsVirtualKeyboard), new FrameworkPropertyMetadata(null));

		public static void SetKey(DependencyObject dp, string value)
			=> dp.SetValue(KeyProperty, value);

		public static string GetKey(DependencyObject dp)
			=> (string)dp.GetValue(KeyProperty);

		#endregion

		#region -- KeyShifted - property ----------------------------------------------

		public static readonly DependencyProperty KeyShiftedProperty = DependencyProperty.RegisterAttached("KeyShifted", typeof(string), typeof(PpsVirtualKeyboard), new FrameworkPropertyMetadata(null));

		public static void SetKeyShifted(DependencyObject dp, string value)
			=> dp.SetValue(KeyShiftedProperty, value);

		public static string GetKeyShifted(DependencyObject dp)
			=> (string)dp.GetValue(KeyShiftedProperty);

		#endregion

		static PpsVirtualKeyboard()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsVirtualKeyboard), new FrameworkPropertyMetadata(typeof(PpsVirtualKeyboard)));
			VisibilityProperty.OverrideMetadata(typeof(PpsVirtualKeyboard), new FrameworkPropertyMetadata(Visibility.Collapsed));
		}
	} // class PpsVirtualKeyboard

	#endregion

	#region -- class PpsKeyInputGenerator ---------------------------------------------

	internal sealed class PpsKeyInputGenerator
	{
		private object[] inputs = new object[8];
		private int count = 0;

		private PpsKeyModifiers state = PpsKeyModifiers.None;
		private PpsKeyModifiers firstState = PpsKeyModifiers.None;

		public PpsKeyInputGenerator()
		{
		} // ctor

		private void Next()
		{
			if (++count >= inputs.Length)
			{
				var n = new object[inputs.Length * 2];
				Array.Copy(inputs, 0, n, 0, count);
				inputs = n;
			}
		} // proc Next

		private void AppendKeyInput(bool keyUp, ushort virtualKey = 0, ushort scanCode = 0, bool isExtended = false)
		{
			var flags = keyUp ? KEYEVENTF_KEYUP : 0;
			if (isExtended
				|| (virtualKey >= 33 && virtualKey <= 46) // PageUp/Next to Delete
				|| (virtualKey >= 91 && virtualKey <= 93)) // LWin to Apps
				flags |= KEYEVENTF_EXTENDEDKEY;

			if (scanCode != 0)
				flags |= KEYEVENTF_SCANCODE;

			inputs[count] = new KEYBDINPUT
			{
				wVk = virtualKey,
				wScan = scanCode,
				dwFlags = flags,
				time = 0,
				dwExtraInfo = IntPtr.Zero
			};

			Next();
		} // proc AppendKeyInput

		private void AppendModifiers(PpsKeyModifiers newState, PpsKeyModifiers currentState)
		{
			var diff = currentState ^ newState;

			void CheckState(PpsKeyModifiers test, ushort virtualKey)
			{
				if ((diff & test) == test)
					AppendKeyInput((currentState & test) == test, virtualKey);
			}

			CheckState(PpsKeyModifiers.Shift, 16);
			CheckState(PpsKeyModifiers.Control, 17);
			CheckState(PpsKeyModifiers.Alt, 18);
			CheckState(PpsKeyModifiers.Win, 91);
		} // proc AppendModifiers

		private void SetModifiers(PpsKeyModifiers newState)
		{
			if (count == 0)
				firstState = newState;
			else
				AppendModifiers(newState, state);
			state = newState;
		} // proc SetModifiers

		private static readonly ushort[] scanCodes = { 0x52, 0x4F, 0x50, 0x51, 0x4B, 0x4C, 0x4D, 0x47, 0x48, 0x49 };

		public PpsKeyInputGenerator Append(char c)
		{
			if (PpsKey.TryGet(c, out var key))
			{
				// set modifiers
				SetModifiers(key.Modifiers);

				// set key/down
				AppendKeyInput(false, key.VirtualKey);
				AppendKeyInput(true, key.VirtualKey);
			}
			else // generate alt+0000
			{
				var charIndex = ((int)c).ToString();

				AppendKeyInput(false, scanCode: 0x38);



				AppendKeyInput(false, scanCode: scanCodes[0]);
				AppendKeyInput(true, scanCode: scanCodes[0]);
				for (var i = 0; i < charIndex.Length; i++)
				{
					var scanCode = scanCodes[charIndex[i] - 0x30];
					AppendKeyInput(false, scanCode: scanCode);
					AppendKeyInput(true, scanCode: scanCode);
				}
	
				AppendKeyInput(true, scanCode: 0x38);
			}
			return this;
		} // proc Append

		public PpsKeyInputGenerator AppendKey(PpsKey key, PpsKeySend flag)
		{
			if ((flag & PpsKeySend.Down) == PpsKeySend.Down)
			{
				SetModifiers(key.Modifiers);
				AppendKeyInput(false, key.VirtualKey);
			}

			if ((flag & PpsKeySend.Up) == PpsKeySend.Up)
				AppendKeyInput(true, key.VirtualKey);

			return this;
		} // proc AppendKey

		public void Send()
		{
			if (count <= 0)
				return;

			var systemState = PpsKeyModifiers.None;
			var beforeList = new PpsKeyInputGenerator();

			if ((GetKeyState(20) & 1) != 0) // caps lock state is active
			{
				beforeList.AppendKeyInput(false, 20, isExtended: true);  // caps lock
				beforeList.AppendKeyInput(true, 20, isExtended: true);
				AppendKeyInput(false, 20, isExtended: true); // caps lock
				AppendKeyInput(true, 20, isExtended: true);
			}

			if ((GetKeyState(16) & 0x80) != 0)
				systemState |= PpsKeyModifiers.Shift;

			if ((GetKeyState(17) & 0x80) != 0)
				systemState |= PpsKeyModifiers.Control;
			if ((GetKeyState(18) & 0x80) != 0)
				systemState |= PpsKeyModifiers.Alt;
			if ((GetKeyState(91) & 0x80) != 0)
				systemState |= PpsKeyModifiers.Win;

			// toggle state to first
			beforeList.AppendModifiers(firstState, systemState);

			// clear modifiers to system state
			SetModifiers(systemState);

			if (beforeList.count > 0)
			{
				var n = new object[beforeList.count + count];
				var nCount = beforeList.count + count;
				Array.Copy(beforeList.inputs, 0, n, 0, beforeList.count);
				Array.Copy(inputs, 0, n, beforeList.count, count);
				inputs = n;
				count = nCount;
			}

			SendInput(inputs, count);
		} // proc Send


		public bool IsEmpty => count == 0;
	} // class PpsKeyInputGenerator

	#endregion
}
