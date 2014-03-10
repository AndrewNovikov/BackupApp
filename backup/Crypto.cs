//using NLog;
using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Security.Cryptography;

namespace backup {
	public static class Crypto {
		//private static readonly Logger LOGGER = LogManager.GetCurrentClassLogger();
		//private static readonly string EmptyMd5 = "00000000000000000000000000000000";
		public static readonly byte[] EmptyMd5 = new byte[] {0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0};

		private static string _emptyMd5;
		public static string EmptyMd5Str {
			get {
				if (_emptyMd5 == null) {
					StringBuilder sb = new StringBuilder();
					foreach (Byte b in EmptyMd5)
						sb.Append(b.ToString("x2").ToLower());
					_emptyMd5 = sb.ToString();
				}
				return _emptyMd5;
			}
		}

		public static long EncryptFile(string md5, string sourceFileName, Stream destinationStream, string encryptionKey) {
			//using (FileStream source = new FileStream(sourceFileName, FileMode.Open, FileAccess.Read, FileShare.Read)) {
      using (FileStream source = IOHelper.OpenFile(sourceFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
				return InternalEncryptFile(md5, source, destinationStream, encryptionKey);
			}
		}

		public static long EncryptFile(string md5, string sourceFileName, string destinationFileName, string encryptionKey) {
			//using (FileStream source = new FileStream(sourceFileName, FileMode.Open, FileAccess.Read, FileShare.Read)) {
      using (FileStream source = IOHelper.OpenFile(sourceFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
				//using (FileStream destination = new FileStream(destinationFileName, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
        using (FileStream destination = IOHelper.OpenFile(destinationFileName, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
					return InternalEncryptFile(md5, source, destination, encryptionKey);
				}
			}
		}

		private static long InternalEncryptFile(string md5, Stream source, Stream destination, string encryptionKey) {
			if (encryptionKey == null || encryptionKey.Length == 0)
				throw new ArgumentException("encryptionKey");
			long result = 0;

			using (var provider = new AesCryptoServiceProvider()) {
				byte[] keyBytes = Sha128String(encryptionKey);
				provider.KeySize = keyBytes.Length * 8;
				provider.Key = keyBytes;
				provider.Mode = CipherMode.CBC;
				provider.Padding = PaddingMode.PKCS7;
				using (MD5 md5Hasher = MD5.Create()) {
					bool finished = false;
					using (var encryptor = provider.CreateEncryptor(provider.Key, provider.IV)) {
						using (var cs = new CryptoStream(destination, encryptor, CryptoStreamMode.Write)) {
							destination.Write(provider.IV, 0, provider.BlockSize / 8);

							int num;
							byte[] buffer = new byte[0x14000];
							while ((num = source.Read(buffer, 0, buffer.Length)) != 0) {
								if (finished) {
									throw new ApplicationException("Not suppose to be here. Encryption must be done by now");
								}

								if (source.Position < source.Length) {
									md5Hasher.TransformBlock(buffer, 0, num, null, 0);
								} else {
									md5Hasher.TransformFinalBlock(buffer, 0, num);
									finished = true;
								}

								cs.Write(buffer, 0, num);
							}

							byte[] hash;
							if (finished) {
								hash = md5Hasher.Hash;
							} else {
								if (source.Length == 0)
									hash = EmptyMd5;
								else
									throw new ApplicationException("During calculating md5 no finish of file has been reached");
							}

							StringBuilder sb = new StringBuilder();
							foreach (Byte b in hash)
								sb.Append(b.ToString("x2").ToLower());

							if (sb.ToString() != md5) {
								throw new ApplicationException("While file was encrypting it was changed. Md5 mismatch.");
							}

							cs.Close();
							result = destination.Position;
						}
					}
				}
			}
			return result;
		}

		public static long DecryptFile(string sourceFileName, string destinationFileName, string encryptionKey) {
			//using (FileStream sourceStream = new FileStream(sourceFileName, FileMode.Open, FileAccess.Read, FileShare.Read)) {
			using (FileStream sourceStream = IOHelper.OpenFile(sourceFileName, FileMode.Open, FileAccess.Read, FileShare.Read)) {
				return DecryptFile(sourceStream, destinationFileName, encryptionKey);
			}
		}

		public static long DecryptFile(Stream sourceStream, string destinationFileName, string encryptionKey) {
			if (encryptionKey == null || encryptionKey.Length == 0)
				throw new ArgumentException("encryptionKey");
			long result = 0;
			
			using (var provider = new AesCryptoServiceProvider()) {
				byte[] keyBytes = Sha128String(encryptionKey);
				provider.KeySize = keyBytes.Length * 8;
				provider.Key = keyBytes;
				provider.Mode = CipherMode.CBC;
				provider.Padding = PaddingMode.PKCS7;
				byte[] iv = new byte[16];
				sourceStream.Read(iv, 0, provider.BlockSize / 8);
				provider.IV = iv;
				using (var decryptor = provider.CreateDecryptor(provider.Key, provider.IV)) {
					using (var cs = new CryptoStream(sourceStream, decryptor, CryptoStreamMode.Read)) {
						//using (FileStream destination = new FileStream(destinationFileName, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
						using (FileStream destination = IOHelper.OpenFile(destinationFileName, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
							//cs.CopyTo(destination);
							int num;
							byte[] buffer = new byte[0x14000];
							while ((num = cs.Read(buffer, 0, buffer.Length)) != 0) {
								destination.Write(buffer, 0, num);
							}

							cs.Close();
							result = destination.Position;
						}
					}
				}
			}
			return result;
		}

		private static byte[] Sha128String(string key) {
			using (SHA256 sha = new SHA256CryptoServiceProvider()) {
				byte[] keyBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(key));
				byte[] result = new byte[16];
				for (int i=0; i< 16; i++) {
					result[i] = (byte)(keyBytes[i] ^ keyBytes[i + 16]);
				}
				return result;
			}
		}

		public static string GetMd5(string filePath) {
			StringBuilder sb = new StringBuilder();
			MD5 md5Hasher = MD5.Create();

			using (FileStream fs = IOHelper.OpenFile(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
				int num;
				bool finished = false;
				byte[] buffer = new byte[0x14000];
				while ((num = fs.Read(buffer, 0, buffer.Length)) != 0) {
					if (finished) {
						throw new ApplicationException("Not suppose to be here. Hashing must be done by now");
					}
					if (fs.Position < fs.Length) {
						md5Hasher.TransformBlock(buffer, 0, num, null, 0);
					} else {
						md5Hasher.TransformFinalBlock(buffer, 0, num);
						finished = true;
					}
				}

				/*if (!finished) {
					if (fs.Length == 0) return EmptyMd5;
					else throw new ApplicationException("During calculating md5 no finish of file has been reached");
				}*/
				byte[] hash;
				if (finished) {
					hash = md5Hasher.Hash;
				} else {
					if (fs.Length == 0) hash = EmptyMd5;
					else throw new ApplicationException("During calculating md5 no finish of file has been reached");
				}

				foreach (Byte b in hash)
					sb.Append(b.ToString("x2").ToLower());

			}

			return sb.ToString();
			//return md5Hasher.Hash;
		}

	}
}

