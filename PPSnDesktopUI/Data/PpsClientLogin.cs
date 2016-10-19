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
using System.ComponentModel;
using System.Net;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace TecWare.PPSn.Data
{
	#region -- class PpsClientLogin -----------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsClientLogin : IDisposable
	{
		private readonly string target;
		private readonly string realm;
		private readonly bool showErrorMessage;

		private NativeMethods.CREDENTIAL credential = new NativeMethods.CREDENTIAL();
		private IntPtr password = IntPtr.Zero;
		private int passwordLength = 0;

		private bool doSave;		//
		private bool isLoaded;	// is the credential blob loaded

		#region -- Ctor/Dtor --------------------------------------------------------------

		internal PpsClientLogin(string target, string realm, bool showErrorMessage)
		{
			this.target = target;
			this.realm = realm;
			this.showErrorMessage = showErrorMessage;

			IntPtr pCred;
			if (NativeMethods.CredRead(target, NativeMethods.CredentialType.Generic, 0, out pCred))
			{
				credential = Marshal.PtrToStructure<NativeMethods.CREDENTIAL>(pCred);

				if (credential.CredentialBlob != IntPtr.Zero) // copy the password in our one memory area
				{
					password = Marshal.AllocCoTaskMem(credential.CredentialBlobSize);
					passwordLength = (credential.CredentialBlobSize >> 1) - 1;
					UnsafeNativeMethods.CopyMemory(password, credential.CredentialBlob, credential.CredentialBlobSize);
				}

				NativeMethods.CredFree(pCred);
				isLoaded = true;
				doSave = true;
			}
			else
			{
				isLoaded = false;
				doSave = false;
			}
		} // ctor

		~PpsClientLogin()
		{
			Dispose(false);
		} // dtor

		public void Dispose()
		{
			GC.SuppressFinalize(this);
			Dispose(true);
		} // proc Dispose

		private void Dispose(bool disposing)
		{
			SecureFreeCoTaskMem(ref password, passwordLength);
		} // proc Dispose

		/// <summary>Commit the login-cache</summary>
		public void Commit()
		{
			if (doSave && !String.IsNullOrEmpty(credential.UserName))
			{
				credential.Flags = 0;
				credential.Type = NativeMethods.CredentialType.Generic;
				credential.TargetName = target;

				credential.CredentialBlob = password;
				credential.CredentialBlobSize = (passwordLength << 1) + 2;

				if (!isLoaded)
				{
					credential.Comment = null;
					credential.Persist = 2; // Local Machine
					credential.AttributeCount = 0;
					credential.Attributes = IntPtr.Zero;
				}
				else if (credential.Persist < 2)
					credential.Persist = 2;

				if (!NativeMethods.CredWrite(ref credential, 0))
					throw new Win32Exception();
			}
			else if (isLoaded)
			{
				if (!NativeMethods.CredDelete(target, NativeMethods.CredentialType.Generic, 0))
					throw new Win32Exception();
			}
		} // proc Commit

		#endregion

		#region -- ShowWindowsLogin -------------------------------------------------------

		/// <summary>Show the default windows login dialog.</summary>
		/// <param name="hwndParent"></param>
		/// <returns></returns>
		public bool ShowWindowsLogin(IntPtr hwndParent)
		{
			int hr;
			var dwAuthPackage = (uint)0;
			var inCredBuffer = IntPtr.Zero;
			var inCredBufferSize = 0;
			var outCredBuffer = IntPtr.Zero;
			var outCredBufferSize = 0;

			var newPasswordLength = NativeMethods.CREDUI_MAX_PASSWORD_LENGTH;
			var newPassword = Marshal.AllocCoTaskMem(newPasswordLength);

			try
			{
				// pack the arguments
				if (isLoaded)
				{
					if (!NativeMethods.CredPackAuthenticationBuffer(4 /*CRED_PACK_GENERIC_CREDENTIALS*/, credential.UserName, password, inCredBuffer, ref inCredBufferSize))
					{
						hr = Marshal.GetLastWin32Error();
						if (hr == 122)
						{
							inCredBuffer = Marshal.AllocCoTaskMem((int)inCredBufferSize);
							if (!NativeMethods.CredPackAuthenticationBuffer(4, credential.UserName, password, inCredBuffer, ref inCredBufferSize))
								throw new Win32Exception();
						}
						else
							throw new Win32Exception(hr);
					}
				}

				// properties of the dialog
				var authentifactionError = showErrorMessage ? 1326 /* LogonFailure */ : 0;
				var messageText = target;
				if (messageText != null)
				{
					int iPos = messageText.IndexOf(':');
					if (iPos != -1)
						messageText = messageText.Substring(iPos + 1);
				}
				var info = new NativeMethods.CREDUI_INFO();
				info.cbSize = Marshal.SizeOf(typeof(NativeMethods.CREDUI_INFO));
				info.hwndParent = hwndParent;
				info.pszMessageText = String.Format("{0} ({1}).", messageText, realm);
				info.pszCaptionText = "Anmeldung";
				info.hbmBanner = IntPtr.Zero;

				// show the dialog
				var flags = NativeMethods.CredUIFlags.Generic | NativeMethods.CredUIFlags.CheckBox;
				hr = NativeMethods.CredUIPromptForWindowsCredentials(ref info, authentifactionError, ref dwAuthPackage, inCredBuffer, inCredBufferSize, out outCredBuffer, out outCredBufferSize, ref doSave, flags);
				if (hr != 0)
				{
					if (hr == 1223) // ERROR_CANCELLED
						return false;
					else
						throw new Win32Exception(hr);
				}

				// unpack the result
				var userName = new StringBuilder(NativeMethods.CREDUI_MAX_USERNAME_LENGTH);
				var domainName = new StringBuilder(NativeMethods.CREDUI_MAX_USERNAME_LENGTH);
				var userNameLength = userName.Capacity;
				var domainNameLength = domainName.Capacity;

				if (!NativeMethods.CredUnPackAuthenticationBuffer(0, outCredBuffer, outCredBufferSize, userName, ref userNameLength, domainName, ref domainNameLength, newPassword, ref newPasswordLength))
					throw new Win32Exception();

				// set the user name
				if (domainName.Length > 0)
					credential.UserName = domainName.ToString() + "\\" + userName.ToString();
				else
					credential.UserName = userName.ToString();

				// release the old password
				var oldPassword = password;
				var oldPasswordLength = passwordLength;

				if (newPasswordLength == 0)
				{
					password = IntPtr.Zero;
					passwordLength = 0;
				}
				else
				{
					password = newPassword;
					passwordLength = newPasswordLength - 1;
				}

				// set the new one
				newPassword = oldPassword;
				newPasswordLength = oldPasswordLength;

				return true;
			}
			finally
			{
				SecureFreeCoTaskMem(ref newPassword, newPasswordLength);
				SecureFreeCoTaskMem(ref inCredBuffer, inCredBufferSize);
				SecureFreeCoTaskMem(ref outCredBuffer, outCredBufferSize);
			}
		} // func ShowWindowsLogin

		private void SecureFreeCoTaskMem(ref IntPtr p, int length)
		{
			if (p != IntPtr.Zero)
			{
				UnsafeNativeMethods.ZeroMemory(p, length);
				Marshal.FreeCoTaskMem(p);
				p = IntPtr.Zero;
			}
		} // func SecureFreeCoTaskMem

		#endregion

		#region -- SetPassword, GetPassword, GetCredentials -------------------------------

		/// <summary>Set a new password.</summary>
		/// <param name="newPassword"></param>
		public void SetPassword(SecureString newPassword)
		{
			if (password != IntPtr.Zero)
				Marshal.ZeroFreeCoTaskMemUnicode(password);

			if (newPassword.Length > 0)
			{
				password = Marshal.SecureStringToCoTaskMemUnicode(newPassword);
				passwordLength = newPassword.Length;
			}
			else
			{
				password = IntPtr.Zero;
				passwordLength = 0;
			}
		} // proc SetPassword

		/// <summary>Get the stored passowrd.</summary>
		/// <returns></returns>
		public unsafe SecureString GetPassword()
			=> password != IntPtr.Zero ?
				new SecureString((char*)password.ToPointer(), passwordLength) :
				null;

		/// <summary>Get the credentilas, as <c>NetworkCredential</c>.</summary>
		/// <returns></returns>
		public ICredentials GetCredentials()
		{
			if (String.IsNullOrEmpty(credential.UserName))
				return null;

			// Parse den Namen
			var userName = new StringBuilder(NativeMethods.CREDUI_MAX_USERNAME_LENGTH);
			var domainName = new StringBuilder(NativeMethods.CREDUI_MAX_USERNAME_LENGTH);
			var hr = NativeMethods.CredUIParseUserName(credential.UserName, userName, userName.Capacity, domainName, domainName.Capacity);
			if (hr == 1315)
			{
				userName.Clear();
				userName.Append(credential.UserName);
				domainName.Clear();
			}
			else if (hr != 0)
				throw new Win32Exception();

			// Gib die Credentials zurück
			return new NetworkCredential(userName.ToString(), GetPassword(), domainName.ToString());
		} // func GetCredentials

		#endregion

		/// <summary>Key for the windows key ring</summary>
		public string Target => target;
		/// <summary>Realm from the web-server.</summary>
		public string Realm => realm;
		/// <summary>Show the windows login error message (LoginFailure).</summary>
		public bool ShowErrorMessage => showErrorMessage;

		/// <summary>User name</summary>
		public string UserName { get { return credential.UserName; } set { credential.UserName = value; } }
		/// <summary>Is a password set.</summary>
		public bool HasPassword => credential.CredentialBlob != IntPtr.Zero;

		/// <summary>State of the save check box.</summary>
		public bool Save { get { return doSave; } set { doSave = value; } }
		/// <summary>Is the data loaded.</summary>
		public bool IsLoaded => isLoaded;
	} // class PpsClientLogin

	#endregion
}
