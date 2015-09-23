using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;

namespace backup {
	public class MyEncryptionStream : Stream {
		Stream _inner;
		CryptoStreamMode _mode;
		AesCryptoServiceProvider _provider;
		ICryptoTransform _encryptor;
		CryptoStream _cs;
		
		public override long Length {
			get {
				return _inner.Length;
			}
		}
		
		public override bool CanRead {
			get {
				return _inner.CanRead;
			}
		}
		
		public override bool CanSeek {
			get {
				return _inner.CanSeek;
			}
		}
		
		public override bool CanWrite {
			get {
				return _inner.CanWrite;
			}
		}
		
		public override long Position {
			get {
				return _inner.Position;
			}
			set {
				_inner.Position = value;
			}
		}
		
		public MyEncryptionStream(Stream inner, string encryptionKey, CryptoStreamMode mode) {
			if (encryptionKey == null || encryptionKey.Length == 0)
				throw new ArgumentException("encryptionKey");
			
			_inner = inner;
			_mode = mode;
			
			_provider = new AesCryptoServiceProvider();
			byte[] keyBytes = Sha128String(encryptionKey);
			_provider.KeySize = keyBytes.Length * 8;
			_provider.Key = keyBytes;
			_provider.Mode = CipherMode.CBC;
			_provider.Padding = PaddingMode.PKCS7;

			if (_mode == CryptoStreamMode.Write) {
				_encryptor = _provider.CreateEncryptor(_provider.Key, _provider.IV);

				_inner.Write(_provider.IV, 0, _provider.BlockSize / 8);
			} else {
				byte[] iv = new byte[16];
				_inner.Read(iv, 0, _provider.BlockSize / 8);
				_provider.IV = iv;

				_encryptor = _provider.CreateDecryptor(_provider.Key, _provider.IV);
			}
			_cs = new CryptoStream(_inner, _encryptor, _mode);
		}
		
		public override void SetLength(long value) {
			_inner.SetLength(value);
		}
		
		public override long Seek(long offset, SeekOrigin origin) {
			return _inner.Seek(offset, origin);
		}
		
		public override void Flush() {
			_inner.Flush();
		}
		
		private static byte[] Sha128String(string key) {
			using (SHA256 sha = new SHA256CryptoServiceProvider()) {
				byte[] keyBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(key));
				byte[] result = new byte[16];
				for (int i = 0; i < 16; i++) {
					result[i] = (byte)(keyBytes[i] ^ keyBytes[i + 16]);
				}
				return result;
			}
		}
		
		public override int Read(byte[] buffer, int offset, int count) {
			//if (_mode == CryptoStreamMode.Write) throw new ApplicationException("Can not write in Read mode");
			return _cs.Read(buffer, offset, count);
		}
		
		public override void Write(byte[] buffer, int offset, int count) {
			//if (_mode == CryptoStreamMode.Read) throw new ApplicationException("Can not read in Write mode");
			_cs.Write(buffer, offset, count);
		}
		
		protected override void Dispose(bool disposing) {
			_cs.Dispose();
			_encryptor.Dispose();
			_provider.Clear();
			base.Dispose(disposing);
		}
		
	}
}

