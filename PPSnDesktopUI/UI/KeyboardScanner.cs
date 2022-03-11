#region -- copyright --
//
// Licensed to the Apache Software Foundation (ASF) under one
// or more contributor license agreements.  See the NOTICE file
// distributed with this work for additional information
// regarding copyright ownership.  The ASF licenses this file
// to you under the Apache License, Version 2.0 (the
// "License"); you may not use this file except in compliance
// with the License.  You may obtain a copy of the License at
// 
//   http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing,
// software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
// KIND, either express or implied.  See the License for the
// specific language governing permissions and limitations
// under the License.
//
#endregion
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using TecWare.DE.Stuff;
using TecWare.PPSn.Interop;

using static TecWare.PPSn.NativeMethods;

namespace TecWare.PPSn.UI
{
	#region -- class KeyboardScanner --------------------------------------------------

	/// <summary>Listener for Keyboard based barcode scanners.</summary>
	public sealed class KeyboardScanner : Win32Window, IEnumerable<KeyboardScanner.Device>, INotifyPropertyChanged, INotifyCollectionChanged
	{
		#region -- class DeviceEventArgs ----------------------------------------------

		/// <summary>Event arguments, that are device related.</summary>
		public class DeviceEventArgs : EventArgs
		{
			/// <summary></summary>
			/// <param name="dev"></param>
			public DeviceEventArgs(Device dev)
				=> Device = dev ?? throw new ArgumentNullException(nameof(dev));

			/// <summary>Device</summary>
			public Device Device { get; }
		} // class DeviceEventArgs

		#endregion

		#region -- class Config -------------------------------------------------------

		/// <summary>Delegate to return configuration for a unknown device.</summary>
		/// <param name="vid"></param>
		/// <param name="pid"></param>
		/// <returns></returns>
		public delegate Config DeviceConfigFactoryDelegate(uint vid, uint pid);

		/// <summary>Device configuration</summary>
		public sealed class Config
		{
			private readonly uint vid;
			private readonly uint pid;
			private readonly string displayName;
			private readonly string keyboardLayout;

			private int refCount = 0;
			//private bool keyboardlayoutLoaded = false;
			private IntPtr hKeyboardLayoutHandle = IntPtr.Zero;

			#region -- Ctor/Dtor ------------------------------------------------------

			internal Config(uint vid, uint pid, string displayName, string keyboardLayout)
			{
				this.vid = vid;
				this.pid = pid;
				this.displayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
				this.keyboardLayout = keyboardLayout ?? throw new ArgumentNullException(nameof(keyboardLayout));
			} // ctor

			/// <summary></summary>
			/// <returns></returns>
			public override string ToString()
				=> $"0x{vid:X4}&0x{pid:X4} - {displayName}";

			/// <summary>Compare the device id's.</summary>
			/// <param name="vid">Vendor id of the device.</param>
			/// <param name="pid">Product id of the device.</param>
			/// <returns></returns>
			public bool IsEqual(uint vid, uint pid)
				=> this.vid == vid && this.pid == pid;

			internal void LoadConfig()
			{
				if (refCount == 0)
				{
					//	InitKeyboardLayout();
					hKeyboardLayoutHandle = GetKeyboardLayout(0);
				}
				refCount++;
			} // proc LoadConfig

			internal void UnloadConfig()
			{
				refCount--;
				//if (refCount == 0)
				//	DoneKeyboardLayout();
			} // proc UnloadConfig

			#endregion

			#region -- KeyboardLayout -------------------------------------------------

			//private void InitKeyboardLayout()
			//{
			//	if (String.IsNullOrEmpty(keyboardLayout) || keyboardLayout == "00000000") // no value, use active keyboard layout
			//	{
			//		Debug.Print("[KeyboardScanner] KeyboardLayout use (Standard)");
			//		hKeyboardLayoutHandle = GetKeyboardLayout(0);
			//		keyboardlayoutLoaded = false;
			//	}
			//	else // load layout
			//	{
			//		hKeyboardLayoutHandle = LoadKeyboardLayout(keyboardLayout, KLF_NOTELLSHELL);
			//		Debug.Print("[KeyboardScanner] KeyboardLayout load 0x{0:X8}", hKeyboardLayoutHandle.ToInt32());
			//		if (hKeyboardLayoutHandle == IntPtr.Zero)
			//		{
			//			hKeyboardLayoutHandle = GetKeyboardLayout(0);
			//			keyboardlayoutLoaded = false;
			//			Debug.Print("[KeyboardScanner] Load of {0} failed.", keyboardLayout);
			//		}
			//		else
			//		keyboardlayoutLoaded = true;
			//	}
			//} // proc InitKeyboardLayout

			//private void DoneKeyboardLayout()
			//{
			//	if (hKeyboardLayoutHandle == IntPtr.Zero)
			//		return;

			//	// KeyboardLayout should not be unloaded, if it is installed. (Sonst kann nichts mehr eingegeben werden ;) )
			//	// todo: use GetKeyboardLayoutList
			//	//foreach (InputLanguage il in InputLanguage.InstalledInputLanguages)
			//	//{
			//	//	if (il.Handle == KeyboardLayoutHandle)
			//	//	{
			//	//		Debug.Print("[KeyboardScanner] KeyboardLayout unset 0x{0:X8}", KeyboardLayoutHandle.ToInt32());
			//	//		KeyboardLayoutHandle = IntPtr.Zero;
			//	//		break;
			//	//	}
			//	//}

			//	// unload layout
			//	if (keyboardlayoutLoaded && hKeyboardLayoutHandle != IntPtr.Zero)
			//	{
			//		Debug.Print("[KeyboardScanner] KeyboardLayout unload 0x{0:X8}", hKeyboardLayoutHandle.ToInt32());
			//		UnloadKeyboardLayout(hKeyboardLayoutHandle);
			//	}

			//	hKeyboardLayoutHandle = IntPtr.Zero;
			//	keyboardlayoutLoaded = false;
			//} // proc DoneKeyboardLayout

			#endregion

			internal IntPtr KeyboardLayoutHandle
				=> hKeyboardLayoutHandle;

			/// <summary>Vendor id of the device.</summary>
			public uint Vid => vid;
			/// <summary>Product id of the device..</summary>
			public uint Pid => pid;

			/// <summary>Display name of the device.</summary>
			public string DisplayName => displayName;
			/// <summary>Default keyboard layout of the device.</summary>
			public string KeyboardLayout => keyboardLayout;
		} // class Config

		#endregion

		#region -- class ConfigEqualComparer ------------------------------------------

		private class ConfigEqualComparer : IEqualityComparer<Config>
		{
			private ConfigEqualComparer() { }

			public int GetHashCode(Config obj)
				=> unchecked((int)(obj.Vid ^ obj.Pid));

			public bool Equals(Config x, Config y)
				=> x.Vid == y.Vid && x.Pid == y.Pid;

			public static IEqualityComparer<Config> Default { get; } = new ConfigEqualComparer();
		} // class ConfigEqualComparer

		#endregion

		#region -- class Device -------------------------------------------------------

		/// <summary>Active and configurated keyboard device.</summary>
		public sealed class Device : IPpsBarcodeProvider
		{
			private readonly string deviceId;
			private readonly IntPtr hDevice;
			private readonly Config config;
			private readonly IDisposable providerToken;

			private readonly StringBuilder scannerBuffer = new StringBuilder();
			private int numInput = 0;
			private readonly byte[] statesScanner = new byte[256];
			private int lastAppendOperation = Environment.TickCount;

			#region -- Ctor/Dtor ------------------------------------------------------

			internal Device(IntPtr hDevice, string deviceId, Config config)
			{
				this.deviceId = deviceId;
				this.hDevice = hDevice;
				this.config = config ?? throw new ArgumentNullException(nameof(config));

				providerToken = PpsShell.Current.GetService<PpsBarcodeService>(false)?.RegisterProvider(this);

				config.LoadConfig();
			} // ctor

			internal void Unload()
			{
				config.UnloadConfig();
				providerToken.Dispose();
			} // proc Unload

			internal bool CheckHandle()
				=> TryExtractDeviceId(hDevice, out var deviceId) || this.deviceId != deviceId;

			#endregion

			#region -- HandleScannerCommand -------------------------------------------

			private static int GetNumPadValue(KeyCode wVKey)
			{
				switch (wVKey)
				{
					case KeyCode.NumPad0:
					case KeyCode.Insert:
						return 0;
					case KeyCode.NumPad1:
					case KeyCode.End:
						return 1;
					case KeyCode.NumPad2:
					case KeyCode.Down:
						return 2;
					case KeyCode.NumPad3:
					case KeyCode.PageDown:
						return 3;
					case KeyCode.NumPad4:
					case KeyCode.Left:
						return 4;
					case KeyCode.NumPad5:
					case KeyCode.Clear:
						return 5;
					case KeyCode.NumPad6:
					case KeyCode.Right:
						return 6;
					case KeyCode.NumPad7:
					case KeyCode.Home:
						return 7;
					case KeyCode.NumPad8:
					case KeyCode.Up:
						return 8;
					case KeyCode.NumPad9:
					case KeyCode.PageUp:
						return 9;
					default:
						return -1;
				}
			} // func GetNumPadValue

			internal bool HandleScannerCommand(WinMsg dwMsg, ushort wMakeCode, out string barcode)
			{
				int tmp;

				barcode = null;

				// lookup the vkey from scancode
				IntPtr hkl;
				lock (this)
					hkl = config.KeyboardLayoutHandle;

				var wVKey = (KeyCode)MapVirtualKeyEx(wMakeCode, 1, hkl); // MAPVK_VSC_TO_VK=1, MAPVK_VSC_TO_VK_EX=3

				//System.IO.File.AppendAllText("c:\\temp\\test.txt", String.Format("Msg=0x{0:X4}, Code=0x{1:X4}, VK={2}" + Environment.NewLine, dwMsg, wMakeCode, wVKey));

				// process key
				if (dwMsg == WinMsg.WM_KEYDOWN || dwMsg == WinMsg.WM_SYSKEYDOWN)
				{
					//Debug.Print("KeyDown:{0}", (Keys)wVKey);
					statesScanner[(byte)wVKey] |= 0x80;
					if (wVKey == KeyCode.Enter
						|| wVKey == KeyCode.LineFeed
						|| ((statesScanner[(byte)KeyCode.ControlKey] & 0x80) != 0 && wVKey == KeyCode.M))
					{
						if (scannerBuffer.Length > 0)
						{
							// Jump over possible prefixes
							var start = 0;
							while (start < scannerBuffer.Length && scannerBuffer[start] < (char)32)
								start++;
							barcode = scannerBuffer.ToString(start, scannerBuffer.Length - start);

							//System.IO.File.AppendAllText("c:\\temp\\test.txt", "BarCode=" + sBarCode + Environment.NewLine);
							Debug.Print("BarCode: {0}", barcode);
							scannerBuffer.Length = 0;
							return true;
						}
					}
					else if (wVKey == KeyCode.Menu)
						numInput = 0;
					else if ((statesScanner[(byte)KeyCode.Menu] & 0x80) != 0
							&& (tmp = GetNumPadValue(wVKey)) >= 0)
						numInput = numInput * 10 + tmp;
					else
					{
						var sb = new StringBuilder(64);
						var ret = ToUnicodeEx((uint)wVKey, (uint)wMakeCode, statesScanner, sb, 64, 0, hkl);
						if (ret > 0)
							scannerBuffer.Append(sb.ToString());
					}
				}
				else if (dwMsg == WinMsg.WM_KEYUP || dwMsg == WinMsg.WM_SYSKEYUP)
				{
					//Debug.Print("KeyUp:{0}", (Keys)wVKey);
					if (wVKey == KeyCode.Menu && numInput > 0)
					{
						try
						{
							scannerBuffer.Append(Convert.ToChar(numInput));
						}
						catch (Exception)
						{
						}
						numInput = 0;
					}

					statesScanner[(byte)wVKey] &= 0x7F;
				}
				//System.IO.File.AppendAllText("c:\\temp\\test.txt", "   Buf=" + sbScannerBuffer.ToString() + Environment.NewLine);
				return false;
			} // proc HandleScannerCommand

			#endregion

			#region -- HandleScannerPacket --------------------------------------------

			internal bool HandleScannerPacket(string code, bool endMark, out string barcode)
			{
				lastAppendOperation = Environment.TickCount;

				if (endMark && scannerBuffer.Length == 0) // code kann direkt weiter gegeben werden
				{
					barcode = code;
					return true;
				}

				// verknüpfe die einzelnen Buffer
				if (code.Length >= 3 && scannerBuffer.Length > 0
					&& scannerBuffer[0] == code[0]
					&& scannerBuffer[1] == code[1]
					&& scannerBuffer[2] == code[2])
					scannerBuffer.Append(code, 3, code.Length - 3); // wenn mehrerer blöcke folgen sind die ersten 3 Zeichen gleich?
				else if(code.Length > 0)
					scannerBuffer.Append(code);

				if (endMark)
				{
					barcode = scannerBuffer.ToString();
					scannerBuffer.Length = 0;
					return true;
				}
				else
				{
					barcode = null;
					return false;
				}
			} // func HandleScannerPacket

			public bool HandleTimerCommand(out string barcode)
			{
				if (scannerBuffer.Length == 0)
				{
					barcode = String.Empty;
					return true;
				}
				else if (unchecked(Environment.TickCount - lastAppendOperation) < 350)
				{
					barcode = null;
					return false;
				}
				else
				{
					if (!HandleScannerPacket(String.Empty, true, out barcode))
						barcode = String.Empty;
					return true;
				}
			} // proc HandleTimerCommand

			#endregion

			string IPpsBarcodeProvider.Description => config.DisplayName;
			string IPpsBarcodeProvider.Type => "Keyboard";

			internal IntPtr Handle => hDevice;

			/// <summary>Device id</summary>
			public string DeviceId => deviceId;
			/// <summary>Name of the device.</summary>
			public string DeviceName => config.DisplayName;
		} // class Device

		#endregion

		/// <summary></summary>
		public event PropertyChangedEventHandler PropertyChanged;
		/// <summary></summary>
		public event NotifyCollectionChangedEventHandler CollectionChanged;

		private static WindowClass keyboardScannerClass = null;
		private readonly List<Device> devices = new List<Device>();

		private string lastSeenDevice = null;
		private bool isActive = false;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary>Create keyboard scanner.</summary>
		private KeyboardScanner()
		{
			CreateWindow(
				caption: "KeyboardScanner Sink",
				style: WS_POPUP
			);

			// register WM_INPUT
			var rid = new RAWINPUTDEVICE[2];

			rid[0].wUsagePage = 1;  // Generic Input Devices Desktop
			rid[0].wUsage = 6;      // Keyboard
			rid[0].hwndTarget = Handle;
			rid[0].dwFlags = 0;

			rid[1].wUsagePage = 0x8C; // Bar Code Scanner page (POS)
			rid[1].wUsage = 2;        // Bar Code Scanner
			rid[1].hwndTarget = Handle;
			rid[1].dwFlags = 0;
			
			if (!RegisterRawInputDevices(rid, rid.Length, Marshal.SizeOf(typeof(RAWINPUTDEVICE))))
				throw new Win32Exception();

			Debug.Print("[KeyboardScanner] CreateHandle");
			RefreshDevices();
		} // ctor

		/// <summary></summary>
		/// <returns></returns>
		protected override WindowClass CreateWindowClass()
		{
			if (keyboardScannerClass != null)
				throw new InvalidOperationException();

			keyboardScannerClass = new WindowClass(nameof(KeyboardScanner));
			return keyboardScannerClass;
		} // func CreateWindowClass

		/// <summary></summary>
		/// <param name="windowClass"></param>
		protected override void DestroyWindowClass(WindowClass windowClass)
		{
			if (windowClass != keyboardScannerClass)
				throw new InvalidOperationException();
			keyboardScannerClass.Dispose();
			keyboardScannerClass = null;
		} // proc DestroyWindowClass

		#endregion

		#region -- WndProc, ProcessInput ----------------------------------------------

		private void RefreshDevices()
		{
			// check for gone devices
			for (var i = devices.Count - 1; i >= 0; i--)
			{
				var dev = devices[i];
				if (!dev.CheckHandle())
				{
					dev.Unload();
					devices.RemoveAt(i);
					OnLostDevice(dev);
				}
			}

			// check for new devies
			var numDevices = (uint)0;
			var structSize = (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICELIST));
			if (GetRawInputDeviceList(IntPtr.Zero, ref numDevices, structSize) != 0)
				return;

			var pData = Marshal.AllocHGlobal((int)(structSize * numDevices));
			try
			{
				if (GetRawInputDeviceList(pData, ref numDevices, structSize) < 0)
					return;

				var offset = pData.ToInt64();
				while (numDevices-- > 0)
				{
					var data = (RAWINPUTDEVICELIST)Marshal.PtrToStructure(new IntPtr(offset), typeof(RAWINPUTDEVICELIST));
					offset += Marshal.SizeOf(typeof(RAWINPUTDEVICELIST));
					if (data.dwType == RIM_TYPEKEYBOARD)
						TryGetDevice(data.hDevice, false, null, out var dev);
					else if(data.dwType == RIM_TYPEHID)
					{
						GetDeviceInfo(data.hDevice, out var usagePage, out var usage);
						if (usage == 2 && usagePage == 0x8C)
							TryGetDevice(data.hDevice, false, CreateGenericConfigForPOSdevices, out var dev);
					}
				}
			}
			finally
			{
				Marshal.FreeHGlobal(pData);
			}
		
			IsActive = devices.Count > 0;
		} // proc RefreshDevices

		private bool TryCreateDevice(IntPtr hDevice, bool updateLastSeenDevice, DeviceConfigFactoryDelegate configFactory, out Device dev)
		{
			// create new device from the known devices
			if (TryExtractDeviceId(hDevice, out var deviceId)
				&& TryParseVIDandPID(deviceId, out var vid, out var pid))
			{
				if (TryFindKnownDevice(vid, pid, configFactory, out var deviceConfig))
				{
					dev = new Device(hDevice, deviceId, deviceConfig);
					devices.Add(dev);
					OnNewDevice(dev);
					IsActive = true;

					if (updateLastSeenDevice)
						LastSeenDevice = dev.DeviceName;

					return true;
				}

				if (updateLastSeenDevice)
					LastSeenDevice = CreateGenericDeviceName(vid, pid);
			}

			dev = null;
			return false;
		} // func CreateDevice

		private bool TryGetDevice(IntPtr hDevice, bool updateLastSeenDevice, DeviceConfigFactoryDelegate configFactory, out Device dev)
		{
			// find already registered device
			for (var i = 0; i < devices.Count; i++)
			{
				if ((dev = devices[i]).Handle == hDevice)
				{
					if (updateLastSeenDevice)
						LastSeenDevice = dev.DeviceName;
					return true;
				}
			}

			// create new device from the known devices
			return TryCreateDevice(hDevice, updateLastSeenDevice, configFactory, out dev);
		} // func CheckDeviceId

		private static bool TryDecodeBarcodeData(bool prefixDetection, byte[] bCode, out int offset, out int count, out bool endMark)
		{
			if (bCode.Length < 2)
			{
				offset = 0;
				count = 0;
				endMark = false;
				return false;
			}

			offset = bCode[0]; // byte 1 scheint offset
			count = bCode[1]; // byte 2 scheint länge -3


			if (bCode[offset] == ']') // skip AIM ID
				offset += 3; // count bleibt gleich
			else
				count += 3;

			if (prefixDetection)
			{
				// check for endmark -> 01 58 1E 61 6E 2F 2F 6E 04 -> seems to be packet in 01 ?? 0x04 frame
				if (bCode[offset + count - 1] == 0x04) // End Of Transmission
				{
					// find SOH Start Of Heading
					for (var i = offset + count - 2; i >= offset; i--)
					{
						if (bCode[i] == 0x01)
						{
							endMark = true;
							count = i - offset;
							return true;
						}
					}
				}

				endMark = false;
			}
			else
				endMark = true;
			return count > 0;
		} // func TryDecodeBarcodeData

		private unsafe void ProcessInput(IntPtr lParam)
		{
			uint cbSize = 0;

			// get the size of the payload
			if (GetRawInputData(lParam, RID_INPUT, IntPtr.Zero, ref cbSize, Marshal.SizeOf(typeof(RAWINPUTHEADER))) == UInt32.MaxValue)
				return;

			// read the raw input event data
			var pBuf = Marshal.AllocHGlobal((int)cbSize);
			try
			{
				if (GetRawInputData(lParam, RID_INPUT, pBuf, ref cbSize, Marshal.SizeOf(typeof(RAWINPUTHEADER))) == UInt32.MaxValue)
					throw new Win32Exception();

				var header = (RAWINPUTHEADER)Marshal.PtrToStructure(pBuf, typeof(RAWINPUTHEADER)); // Konvertiere
				if (header.dwType == RIM_TYPEKEYBOARD && header.hDevice != IntPtr.Zero)
				{
					//Debug.Print("WM_INPUT: Device=0x{0:X8}, Message=0x{1:X4}, VKey=0x{2:X2}, MakeCode=0x{3:X}, Flags={4}, Extra={5}", raw.header.hDevice.ToInt32(), raw.keyboard.Message, raw.keyboard.VKey, raw.keyboard.MakeCode, raw.keyboard.Flags, raw.keyboard.ExtraInformation);
					if (TryGetDevice(header.hDevice, true, null, out var dev))
					{
						var buf = (byte*)pBuf.ToPointer();
						buf += Marshal.SizeOf(typeof(RAWINPUTHEADER));
						var keyboard = (RAWKEYBOARD)Marshal.PtrToStructure(new IntPtr(buf), typeof(RAWKEYBOARD));

						// process key code to a string
						if (dev.HandleScannerCommand((WinMsg)keyboard.Message, keyboard.MakeCode, out var barcode))
						{
							ClearMessageLoop(); // clear all generated keydown messages
							OnBarcode(dev, barcode);
						}
						else
							ClearMessageLoop(); // clear all generated keydown messages
					}
				}
				else if (header.dwType == RIM_TYPEHID && header.hDevice != IntPtr.Zero)
				{
					if (TryGetDevice(header.hDevice, true, CreateGenericConfigForPOSdevices, out var dev))
					{
						// read header
						var buf = (byte*)pBuf.ToPointer();
						buf += Marshal.SizeOf(typeof(RAWINPUTHEADER));

						// read hid header
						var hid = (RAWHID)Marshal.PtrToStructure(new IntPtr(buf), typeof(RAWHID));
						buf += Marshal.SizeOf(typeof(RAWHID));

						// parse packets
						var bCode = new byte[hid.dwSizeHid];
						var timeoutDetection = TimeoutDetection > 100;
						var resetTimer = false;
						for (var i = 0; i < hid.dwCount; i++)
						{
							Marshal.Copy(new IntPtr(buf), bCode, 0, bCode.Length);

							//using (var dst = new FileStream("c:\\temp\\t.dat", FileMode.OpenOrCreate))
							//{
							//	dst.Position = dst.Length;
							//	dst.Write(bCode, 0, bCode.Length);
							//}

							if (TryDecodeBarcodeData(PrefixDetection, bCode, out var offset, out var count, out var endMark)
								&& dev.HandleScannerPacket(Encoding.Default.GetString(bCode, offset, count), !timeoutDetection && endMark, out var barcode))
							{
								OnBarcode(dev, barcode);
							}
							else if (timeoutDetection)
								resetTimer = true;

							buf += hid.dwSizeHid;
						}

						if (resetTimer)
							ResetTimer(TimeoutDetection);
						else
							KillTimer();
					}
				}

				// call other raw input procedures
				DefRawInputProc(lParam, 1, Marshal.SizeOf(typeof(RAWINPUTHEADER)));
			}
			catch (Exception e)
			{
				Debug.Print(e.GetMessageString());
			}
			finally
			{
				Marshal.FreeHGlobal(pBuf);
			}
		} // proc ProcessInput

		private void ResetTimer(int timeout)
			=> SetTimer(Handle, new IntPtr(1), (uint)timeout, IntPtr.Zero);


		private void KillTimer()
			=> NativeMethods.KillTimer(Handle, new IntPtr(1));

		/// <summary>Listen for WM_INPUT and WM_DEVICECHANGE</summary>
		/// <param name="hwnd"></param>
		/// <param name="msg"></param>
		/// <param name="wParam"></param>
		/// <param name="lParam"></param>
		/// <returns></returns>
		protected override IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam)
		{
			switch ((WinMsg)msg)
			{
				case WinMsg.WM_INPUT:
					ProcessInput(lParam);
					break;
				case WinMsg.WM_DEVICECHANGE:
					if (wParam.ToInt32() == DBT_DEVICEARRIVAL
						|| wParam.ToInt32() == DBT_DEVICEREMOVECOMPLETE
						|| wParam.ToInt32() == DBT_DEVNODES_CHANGED)
					{
						RefreshDevices();
					}
					break;
				case WinMsg.WM_TIMER:
					var killTimer = true;
					foreach (var dev in devices)
					{
						if (!dev.HandleTimerCommand(out var barcode))
						{
							killTimer = false;
							if (barcode.Length > 0)
								OnBarcode(dev, barcode);
						}
					}
					if (killTimer)
						KillTimer(); 
					break;
			}

			return base.WndProc(hwnd, msg, wParam, lParam);
		} // func WndProc

		private static void ClearMessageLoop()
		{
			do
			{
			} while (PeekMessage(out _, IntPtr.Zero, (int)WinMsg.WM_KEYFIRST, (int)WinMsg.WM_KEYLAST, 1)); // PM_REMOVE
		} // proc ClearMessageLoop

		/// <summary>Get all active devices.</summary>
		/// <returns></returns>
		public IEnumerator<Device> GetEnumerator()
			=> devices.GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator()
			=> GetEnumerator();

		private void OnBarcode(Device dev, string barcode)
			=> PpsShell.GetService<PpsBarcodeService>(false)?.DispatchBarcode(dev, barcode);

		private void OnNewDevice(Device dev)
			=> CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, dev));

		private void OnLostDevice(Device dev)
			=> CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, dev));

		private void OnPropertyChanged(string propertyName)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		/// <summary>Last seen device.</summary>
		public string LastSeenDevice
		{
			get => lastSeenDevice;
			set
			{
				if (lastSeenDevice != value)
				{
					lastSeenDevice = value;
					OnPropertyChanged(nameof(LastSeenDevice));
				}
			}
		} // prop LastSeenDevice

		/// <summary>Is there at least one active device.</summary>
		public bool IsActive
		{
			get => isActive;
			private set
			{
				if (isActive != value)
				{
					isActive = value;
					OnPropertyChanged(nameof(IsActive));
				}
			}
		} // prop IsActive

		#endregion

		/// <summary>Global prefix detection for POS-Devices.</summary>
		public bool PrefixDetection { get; set; } = true;
		/// <summary>Global timeout detection for POS-Devices.</summary>
		public int TimeoutDetection { get; set; } = 300;

		#region -- Raw Input Helper ---------------------------------------------------

		private static bool TryExtractDeviceId(IntPtr hDevice, out string deviceId)
		{
			var strSize = 0u;
			if (GetRawInputDeviceInfo(hDevice, RIDI_DEVICENAME, IntPtr.Zero, ref strSize) < 0)
			{
				Debug.Print("GetRawInputDeviceInfo failed: {0}", Marshal.GetLastWin32Error());
				strSize = 0;
			}
				
			if (strSize == 0)
			{
				deviceId = null;
				return false;
			}

			var pStr = Marshal.AllocHGlobal((int)strSize);
			try
			{
				if (GetRawInputDeviceInfo(hDevice, RIDI_DEVICENAME, pStr, ref strSize) < 0)
				{
					deviceId = null;
					return false;
				}

				deviceId = Marshal.PtrToStringAnsi(pStr);
				return true;
			}
			finally
			{
				Marshal.FreeHGlobal(pStr);
			}
		} // proc TryExtractDeviceId

		private static readonly Regex regVID = new Regex(@"VID[_\&]([0-9a-f]{1,8})", RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.IgnoreCase);
		private static readonly Regex regPID = new Regex(@"PID[_\&]([0-9a-f]{1,8})", RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.IgnoreCase);

		private static bool TryParseVIDandPID(string deviceId, out uint vid, out uint pid)
		{
			int offset;

			vid = 0;
			pid = 0;

			if (String.IsNullOrEmpty(deviceId))
				return false;

			// {00001124-0000-1000-8000-00805f9b34fb}_LOCALMFG&nnnn
			const string serviceGuidString = "{00001124-0000-1000-8000-00805f9b34fb}_LOCALMFG&";
			offset = deviceId.IndexOf(serviceGuidString, StringComparison.OrdinalIgnoreCase);
			if (offset >= 0 && UInt32.TryParse(deviceId.Substring(offset + serviceGuidString.Length, 4), NumberStyles.HexNumber | NumberStyles.AllowHexSpecifier, null, out vid))
			{
				pid = 0;
				return true;
			}

			// normale variante
			var m = regVID.Match(deviceId);
			if (!m.Success)
				return false;
			vid = UInt32.Parse(m.Groups[1].Value, NumberStyles.HexNumber | NumberStyles.AllowHexSpecifier, null);

			m = regPID.Match(deviceId);
			if (!m.Success)
				return false;
			pid = UInt32.Parse(m.Groups[1].Value, NumberStyles.HexNumber | NumberStyles.AllowHexSpecifier, null);

			return true;
		} // func TryParseVIDandPID

		private static uint GetDeviceInfo(IntPtr hDevice, out ushort usagePage, out ushort usage)
		{
			var sz = (uint)Marshal.SizeOf(typeof(RID_DEVICE_INFO));
			var p = Marshal.AllocHGlobal((int)sz);
			try
			{
				Marshal.WriteInt32(p, (int)sz);
				if (GetRawInputDeviceInfo(hDevice, RIDI_DEVICEINFO, p, ref sz) < 0)
					throw new Win32Exception();

				var di = (RID_DEVICE_INFO)Marshal.PtrToStructure(p, typeof(RID_DEVICE_INFO));
				if (di.dwType == RIM_TYPEHID)
				{
					usagePage = di.hid.usUsagePage;
					usage = di.hid.usUsage;
					return RIM_TYPEHID;
				}
				else if (di.dwType == RIM_TYPEKEYBOARD)
				{
					usagePage = 1;
					usage = 6;
					return RIM_TYPEKEYBOARD;
				}
				else
				{

					usagePage = 0;
					usage = 0;
					return unchecked((uint)-1);
				}
			}
			finally
			{
				Marshal.FreeHGlobal(p);
			}
		} // proc GetDeviceInfo

		#endregion

		#region -- Well Known Scanner -------------------------------------------------

		private static readonly Config[] wellKnownScanner;

		private static string CreateGenericDeviceName(uint vid, uint pid)
		{
			return String.Format(
				pid > 0xFFFF || vid > 0xFFFF ? "PID=0x{0:X8},VID=0x{1:X8}" : "PID=0x{0:X4},VID=0x{1:X4}",
				pid, vid
			);
		} // func CreateGenericDeviceName

		private static Config CreateGenericConfigForPOSdevices(uint vid, uint pid)
			=> new Config(vid, pid, "Generic POS", null);

		/// <summary>Create a generic configuration for a keyboard device.</summary>
		/// <param name="vid"></param>
		/// <param name="pid"></param>
		/// <returns></returns>
		public static Config CreateGenericConfig(uint vid, uint pid)
			=> new Config(vid, pid, CreateGenericDeviceName(vid, pid), null);

		private static bool TryGetInt(string text, out uint value)
		{
			if (text.Length > 10)
				goto fail;

			if (text.Length >= 2 && (text[0] == '0' && (text[1] == 'X' || text[1] == 'x')))
			{
				if (UInt32.TryParse(text.Substring(2), NumberStyles.HexNumber, null, out value))
					return true;
			}
			else
			{
				if (UInt32.TryParse(text, out value))
					return true;
			}

			fail:
			value = 0;
			return false;
		} // func TryGetInt

		private static IEnumerable<Config> ParseScannerStream(Stream src)
		{
			if (src == null)
				yield break;

			try
			{
				using (var tr = new StreamReader(src, Encoding.UTF8, true))
				{
					string line;
					while ((line = tr.ReadLine()) != null)
					{
						if (line.Length == 0)
							continue;
						else if (line[0] == '#')
							continue;

						var columns = line.Split(';');
						if (columns.Length < 4
							|| !TryGetInt(columns[0], out var vid)
							|| !TryGetInt(columns[1], out var pid))
							continue;

						yield return new Config(vid, pid, columns[3], columns[2]);
					}
				}
			}
			finally
			{
				src.Dispose();
			}
		} // func ParseScannerFile

		private static Stream GetResourceKeyboardScannerFile()
			=> typeof(KeyboardScanner).Assembly.GetManifestResourceStream(typeof(KeyboardScanner), "KeyboardScanner.csv");

		private static Stream GetOptKeyboardScannerFile()
		{
			var fileInfo = new FileInfo(Path.Combine(Path.GetDirectoryName(typeof(KeyboardScanner).Assembly.Location), "KeyboardScanner.csv"));
			return fileInfo.Exists ? fileInfo.OpenRead() : null;
		} // func GetOptKeyboardScannerFile

		private static bool TryFindKnownDevice(uint vid, uint pid, DeviceConfigFactoryDelegate configFactory, out Config deviceConfig)
		{
			deviceConfig = Array.Find(wellKnownScanner, cur => cur.IsEqual(vid, pid))
				?? DeviceConfigFactory?.Invoke(vid, pid)
				?? configFactory?.Invoke(vid, pid);
			return deviceConfig != null;
		} // func TryFindKnownDevice

		/// <summary>Fallback for unknown device configuration.</summary>
		public static DeviceConfigFactoryDelegate DeviceConfigFactory { get; set; } = null;

		#endregion

		private static KeyboardScanner instance = null;

		static KeyboardScanner()
		{
			wellKnownScanner =
				ParseScannerStream(GetOptKeyboardScannerFile())
				.Union(ParseScannerStream(GetResourceKeyboardScannerFile()), ConfigEqualComparer.Default)
				.ToArray();
		} // sctor

		/// <summary>Create a keyboard scanner.</summary>
		public static void Init()
		{
			if (instance != null)
				return;

			Application.Current.Dispatcher.VerifyAccess();
			instance = new KeyboardScanner();
		} // proc Init

		/// <summary>Get the keyboard scanner instance.</summary>
		public static KeyboardScanner Default => instance;
	} // class KeyboardScanner

	#endregion
}
