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
using System.IO;
using System.Security.Cryptography;

namespace TecWare.PPSn.Stuff
{
	#region -- enum HashStreamDirection -----------------------------------------------

	/// <summary>Basic direction of the hash stream.</summary>
	public enum HashStreamDirection
	{
		/// <summary>Stream can only read data.</summary>
		Read,
		/// <summary>Stream can only write data.</summary>
		Write
	} // enum HashStreamDirection

	#endregion

	#region -- class HashStream -------------------------------------------------------

	/// <summary>Stream that calculates a hash sum during the read/write.</summary>
	public class HashStream : Stream
	{
		private readonly Stream baseStream;
		private readonly bool leaveOpen;

		private readonly HashStreamDirection direction;
		private readonly HashAlgorithm hashAlgorithm;
		private bool isFinished = false;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary>Stream that calculates a hash sum during the read/write.</summary>
		/// <param name="baseStream"></param>
		/// <param name="direction"></param>
		/// <param name="leaveOpen"></param>
		/// <param name="hashAlgorithm"></param>
		public HashStream(Stream baseStream, HashStreamDirection direction, bool leaveOpen, HashAlgorithm hashAlgorithm)
		{
			this.baseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));
			this.leaveOpen = leaveOpen;
			this.hashAlgorithm = hashAlgorithm ?? throw new ArgumentNullException(nameof(hashAlgorithm));
			this.direction = direction;

			if (direction == HashStreamDirection.Write && !baseStream.CanWrite)
				throw new ArgumentException("baseStream is not writeable.");
			if (direction == HashStreamDirection.Read && !baseStream.CanRead)
				throw new ArgumentException("baseStream is not readable.");
		} // ctor

		/// <summary></summary>
		/// <param name="disposing"></param>
		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				// force finish
				if (!isFinished)
					FinalBlock(Array.Empty<byte>(), 0, 0);

				// close base stream
				if (!leaveOpen)
					baseStream?.Close();
			}
			else if (!isFinished)
				Debug.Print("HashStream not closed correctly."); // maybe an exception?

			base.Dispose(disposing);
		} // proc Dispose

		#endregion

		#region -- Read/Write ---------------------------------------------------------

		/// <summary></summary>
		public override void Flush()
			=> baseStream.Flush();

		/// <summary></summary>
		/// <param name="buffer"></param>
		/// <param name="offset"></param>
		/// <param name="count"></param>
		/// <returns></returns>
		public override int Read(byte[] buffer, int offset, int count)
		{
			if (direction != HashStreamDirection.Read)
				throw new NotSupportedException("The stream is not in read mode.");
			else if (isFinished)
				return 0;

			var readed = baseStream.Read(buffer, offset, count);
			if (readed == 0 || baseStream.CanSeek && baseStream.Position == baseStream.Length)
			{
				FinalBlock(buffer, offset, readed);
				isFinished = true;
			}
			else
				hashAlgorithm.TransformBlock(buffer, offset, readed, buffer, offset);

			return readed;
		} // func Read

		/// <summary></summary>
		/// <param name="buffer"></param>
		/// <param name="offset"></param>
		/// <param name="count"></param>
		public override void Write(byte[] buffer, int offset, int count)
		{
			if (direction != HashStreamDirection.Write)
				throw new NotSupportedException("The stream is in write mode.");
			else if (isFinished)
				throw new InvalidOperationException("Stream is finished.");

			baseStream.Write(buffer, offset, count);

			if (count == 0)
				FinalBlock(buffer, offset, count);
			else
				hashAlgorithm.TransformBlock(buffer, offset, count, buffer, offset);
		} // proc Write

		#endregion

		#region -- Hash Calculation ---------------------------------------------------

		/// <summary></summary>
		/// <returns></returns>
		public byte[] CalcHash()
		{
			if (!isFinished)
				FinalBlock(Array.Empty<byte>(), 0, 0);

			return hashAlgorithm.Hash;
		} // func CalcHash

		private void FinalBlock(byte[] buffer, int offset, int count)
		{
			hashAlgorithm.TransformFinalBlock(buffer, offset, count);
			isFinished = true;

			OnFinished(hashAlgorithm.Hash);
		} // proc FinalBlock

		/// <summary>Gets called if the stream is finished.</summary>
		/// <param name="hash"></param>
		protected virtual void OnFinished(byte[] hash)
		{
		} // proc Finished

		#endregion

		#region -- Seek ---------------------------------------------------------------

		/// <summary></summary>
		/// <param name="offset"></param>
		/// <param name="origin"></param>
		/// <returns></returns>
		public override long Seek(long offset, SeekOrigin origin)
		{
			var currentPosition = baseStream.Position;
			switch (origin)
			{
				case SeekOrigin.Begin:
					if (currentPosition == offset)
						return currentPosition;
					goto default;
				case SeekOrigin.Current:
					if (offset == 0)
						return currentPosition;
					goto default;
				case SeekOrigin.End:
					if (baseStream.Length - offset == currentPosition)
						return currentPosition;
					goto default;
				default:
					throw new NotSupportedException();
			}
		} // func Seek

		/// <summary></summary>
		/// <param name="value"></param>
		public override void SetLength(long value)
		{
			if (direction == HashStreamDirection.Write)
				baseStream.SetLength(value);
			else
				throw new NotSupportedException();
		} // proc SetLength

		#endregion

		/// <summary></summary>
		public override bool CanRead => direction == HashStreamDirection.Read;
		/// <summary></summary>
		public override bool CanWrite => direction == HashStreamDirection.Write;
		/// <summary></summary>
		public override bool CanSeek => false;
		/// <summary></summary>
		public override long Length => baseStream.Length;
		/// <summary></summary>
		public override long Position
		{
			get => baseStream.Position;
			set
			{
				if (baseStream.Position == value)
					return;
				throw new NotSupportedException();
			}
		} // prop Position

		/// <summary>Base stream</summary>
		public Stream BaseStream => baseStream;
		/// <summary>Hash function/algorithm.</summary>
		public HashAlgorithm HashAlgorithm => hashAlgorithm;

		/// <summary>Is the stream fully read/written.</summary>
		public bool IsFinished => isFinished;

		/// <summary>Hash sum</summary>
		public byte[] HashSum => isFinished ? hashAlgorithm.Hash : null;
	} // class HashStream

	#endregion
}
