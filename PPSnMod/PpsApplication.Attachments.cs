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
using System.IO;
using TecWare.DE.Stuff;

namespace TecWare.PPSn.Server
{
	#region -- class PpsAttachmentAccess ------------------------------------------------

	/// <summary>Abstract class for attachment access over the api.</summary>
	public abstract class PpsAttachmentAccess : IDisposable
	{
		private bool isDisposed = false;

		#region -- Ctor/Dtor ------------------------------------------------------------

		/// <summary></summary>
		public PpsAttachmentAccess()
		{
		} // ctor

		//~PpsAttachmentAccess()
		//{
		//	Dispose(false);
		//} // dtor

		/// <summary></summary>
		public void Dispose()
		{
			//GC.SuppressFinalize(this);
			Dispose(true);
		}

		/// <summary></summary>
		/// <param name="disposing"></param>
		protected virtual void Dispose(bool disposing)
		{
			if (isDisposed)
				throw new ObjectDisposedException(nameof(PpsAttachmentAccess));

			isDisposed = true;
		} // proc Dipose

		#endregion

		/// <summary>Copy the data.</summary>
		/// <param name="dst"></param>
		public abstract void CopyTo(Stream dst);

		/// <summary>Get the data stream of the object.</summary>
		/// <returns></returns>
		public abstract Stream GetStream();
	} // class PpsAttachmentAccess

	#endregion

	#region -- class PpsAttachmentItem --------------------------------------------------

	/// <summary>Save a binary object on to the server.</summary>
	public sealed class PpsAttachmentItem : PpsObjectItem<PpsAttachmentAccess>
	{
		#region -- class PpsStreamAttachmentAccess --------------------------------------

		private sealed class PpsStreamAttachmentAccess : PpsAttachmentAccess
		{
			private readonly Stream stream;
			private MemoryStream mStream;

			public PpsStreamAttachmentAccess(Stream stream)
			{
				this.stream = stream;
			} // ctor

			protected override void Dispose(bool disposing)
			{
				base.Dispose(disposing);

				if (disposing)
				{
					stream?.Dispose();
					mStream?.Dispose();
				}
			} // proc Dispose

			public override Stream GetStream()
				=> new WindowStream(stream, 0, -1, false, true);

			public override void CopyTo(Stream dst)
			{
				if (mStream == null)
				{
					mStream = new MemoryStream();
					stream.Flush();
					stream.CopyTo(mStream);
					mStream.Flush();
				}
				mStream.Position = 0;
				mStream.CopyTo(dst);
			}
		} // class PpsStreanAttachmentAccess

		#endregion

		#region -- class PpsObjectAttachmentAccess --------------------------------------

		private sealed class PpsObjectAttachmentAccess : PpsAttachmentAccess
		{
			private readonly PpsObjectAccess obj;

			public PpsObjectAttachmentAccess(PpsObjectAccess obj)
			{
				this.obj = obj;
			} // ctor

			public override void CopyTo(Stream dst)
			{
				using (var src = GetStream())
					src.CopyTo(dst);
			} // proc CopyTo

			public override Stream GetStream()
				=> obj.GetDataStream();
		} // class PpsStreanAttachmentAccess

		#endregion

		/// <summary></summary>
		/// <param name="sp"></param>
		/// <param name="name"></param>
		public PpsAttachmentItem(IServiceProvider sp, string name)
			: base(sp, name)
		{
		} // ctor

		/// <summary>Get a access object from the stream.</summary>
		/// <param name="src"></param>
		/// <returns></returns>
		protected override PpsAttachmentAccess GetDataFromStream(Stream src)
			=> new PpsStreamAttachmentAccess(src);

		/// <summary>Write the data to an stream.</summary>
		/// <param name="data"></param>
		/// <param name="dst"></param>
		protected override void WriteDataToStream(PpsAttachmentAccess data, Stream dst)
			=> data.CopyTo(dst);

		/// <summary>Pull data from the database.</summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		protected override PpsAttachmentAccess PullData(PpsObjectAccess obj)
			=> new PpsObjectAttachmentAccess(obj);

		/// <summary>Attachment do not track revisions.</summary>
		/// <param name="data"></param>
		/// <returns></returns>
		protected override bool IsDataRevision(PpsAttachmentAccess data)
			=> false;

		/// <summary></summary>
		/// <param name="obj"></param>
		/// <param name="data"></param>
		/// <param name="release"></param>
		/// <returns></returns>
		protected override bool LuaPush(PpsObjectAccess obj, object data, bool release)
		{
			switch (data)
			{
				case byte[] b:
					using (var a = new PpsStreamAttachmentAccess(new MemoryStream(b, false)))
						return PushData(obj, a, release);

				case string s:
					using (var a = new PpsStreamAttachmentAccess(new FileStream(s, FileMode.Open, FileAccess.Read)))
						return PushData(obj, a, release);

				case Stream src:
					using (var a = new PpsStreamAttachmentAccess(src))
						return PushData(obj, a, release);

				case PpsAttachmentAccess a:
					return PushData(obj, a, release);

				case null:
					throw new ArgumentNullException(nameof(data));

				default:
					throw new ArgumentException();
			}
		} // func LuaPush
	} // class PpsAttachment

	#endregion
}
