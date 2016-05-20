using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace TecWare.PPSn
{
	internal static partial class NativeMethods
	{
		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
		public struct CREDUI_INFO
		{
			public int cbSize;
			public IntPtr hwndParent;
			public string pszMessageText;
			public string pszCaptionText;
			public IntPtr hbmBanner;
		} // struct CREDUI_INFO

		[StructLayout(LayoutKind.Sequential)]
		public struct CREDENTIAL
		{
			public int Flags;
			public NativeMethods.CredentialType Type;
			[MarshalAs(UnmanagedType.LPWStr)]
			public string TargetName;
			[MarshalAs(UnmanagedType.LPWStr)]
			public string Comment;
			public long LastWritten;
			public int CredentialBlobSize;
			public IntPtr CredentialBlob;
			public int Persist;
			public int AttributeCount;
			public IntPtr Attributes;
			[MarshalAs(UnmanagedType.LPWStr)]
			public string TargetAlias;
			[MarshalAs(UnmanagedType.LPWStr)]
			public string UserName;
		} // struct struct CREDENTIAL

		[Flags]
		public enum CredUIFlags : uint
		{
			Generic = 1,
			CheckBox = 2,
			AuthpackageOnly = 0x10,
			InCredOnly = 0x20,
			EnumerateAdmins = 0x100,
			EnumerateCurrentUser = 0x200,
			SecurePrompt = 0x1000,
			Pack32wow = 0x10000000
		} // enum CredUIwin

		public enum CredentialType : uint
		{
			None = 0,
			Generic = 1,
			DomainPassword = 2,
			DomainCertificate = 3,
			DomainVisiblePassword = 4
		} // enum CredentialType

		public const int CREDUI_MAX_USERNAME_LENGTH = 513;
		public const int CREDUI_MAX_PASSWORD_LENGTH = 256;
		public const int CREDUI_MAX_MESSAGE_LENGTH = 32767;
		public const int CREDUI_MAX_CAPTION_LENGTH = 128;

		private const string csAdvapi32 = "Advapi32.dll";
		private const string csCredUI = "credui.dll";

		[DllImport(csAdvapi32, CharSet = CharSet.Unicode, SetLastError = true)]
		internal static extern bool CredRead(string target, CredentialType type, int reservedFlag, out IntPtr CredentialPtr);

		[DllImport(csAdvapi32, CharSet = CharSet.Unicode, SetLastError = true)]
		internal static extern bool CredWrite([In] ref CREDENTIAL userCredential, [In] UInt32 flags);

		[DllImport(csAdvapi32, SetLastError = true)]
		internal static extern bool CredFree([In] IntPtr cred);

		[DllImport(csAdvapi32, CharSet = CharSet.Unicode)]
		internal static extern bool CredDelete(string target, CredentialType type, int flags);


		[DllImport(csCredUI, CharSet = CharSet.Unicode)]
		public static extern int CredUIPromptForWindowsCredentials(ref CREDUI_INFO pUiInfo, int dwAuthError, ref uint pulAuthPackage, IntPtr pvInAuthBuffer, int ulInAuthBufferSize, out IntPtr ppvOutAuthBuffer, out int pulOutAuthBufferSize, ref bool pfSave, CredUIFlags dwFlags);
		[DllImport(csCredUI, CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern bool CredPackAuthenticationBuffer(int dwFlags, string pszUserName, IntPtr pszPassword, IntPtr pPackedCredentials, ref int pcbPackedCredentials);
		[DllImport(csCredUI, CharSet = CharSet.Unicode)]
		public static extern bool CredUnPackAuthenticationBuffer(int dwFlags, IntPtr pAuthBuffer, int cbAuthBuffer, StringBuilder pszUserName, ref int pcchMaxUserName, StringBuilder pszDomainName, ref int pcchMaxDomainame, IntPtr pszPassword, ref int pcchMaxPassword);
		[DllImport(csCredUI, CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern int CredUIParseUserName(string pszUserName, StringBuilder pszUser, int ulUserMaxChars, StringBuilder pszDomain, int ulDomainMaxChars);
	} // NativeMethods
}
