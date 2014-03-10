using NLog;
using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Security.Cryptography;

namespace backup {
	public static class Crypto {
		private static readonly Logger LOGGER = LogManager.GetCurrentClassLogger();
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

		public static void EncryptFile(string md5, string sourceFileName, Stream destinationStream, string encryptionKey) {
			using (FileStream source = new FileStream(sourceFileName, FileMode.Open, FileAccess.Read, FileShare.Read)) {
				InternalEncryptFile(md5, source, destinationStream, encryptionKey);
			}
		}

		public static void EncryptFile(string md5, string sourceFileName, string destinationFileName, string encryptionKey) {
			using (FileStream source = new FileStream(sourceFileName, FileMode.Open, FileAccess.Read, FileShare.Read)) {
				using (FileStream destination = new FileStream(destinationFileName, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
					InternalEncryptFile(md5, source, destination, encryptionKey);
				}
			}
		}

		private static void InternalEncryptFile(string md5, Stream source, Stream destination, string encryptionKey) {
			if (encryptionKey == null || encryptionKey.Length == 0)
				throw new ArgumentException("encryptionKey");
			
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

							/*if (!md5.SequenceEqual(md5Hasher.Hash)) {
							throw new ApplicationException("While file was encrypting it was changed. Md5 mismatch.");
						}*/

							StringBuilder sb = new StringBuilder();
							foreach (Byte b in hash)
								sb.Append(b.ToString("x2").ToLower());

							if (sb.ToString() != md5) {
								throw new ApplicationException("While file was encrypting it was changed. Md5 mismatch.");
							}

							//source.CopyTo(cs);
						}
					}
				}
			}
		}
		
		public static void DecryptFile(string sourceFileName, string destinationFileName, string encryptionKey) {
			if (encryptionKey == null || encryptionKey.Length == 0)
				throw new ArgumentException("encryptionKey");
			
			using (var provider = new AesCryptoServiceProvider()) {
				byte[] keyBytes = Sha128String(encryptionKey);
				provider.KeySize = keyBytes.Length * 8;
				provider.Key = keyBytes;
				provider.Mode = CipherMode.CBC;
				provider.Padding = PaddingMode.PKCS7;
				using (FileStream source = new FileStream(sourceFileName, FileMode.Open, FileAccess.Read, FileShare.Read)) {
					byte[] iv = new byte[16];
					source.Read(iv, 0, provider.BlockSize / 8);
					provider.IV = iv;
					using (var decryptor = provider.CreateDecryptor(provider.Key, provider.IV)) {
						using (var cs = new CryptoStream(source, decryptor, CryptoStreamMode.Read)) {
							using (FileStream destination = new FileStream(destinationFileName, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
								cs.CopyTo(destination);
							}
						}
					}
				}
			}
			
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

			using (FileStream fs = File.Open(filePath, FileMode.Open, FileAccess.Read)) {
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

		public static string GetMd5_old(string filePath) {
			StringBuilder sb = new StringBuilder();
			MD5 md5Hasher = MD5.Create();
			
			try {
				using (FileStream fs = File.OpenRead(filePath)) {
					foreach (Byte b in md5Hasher.ComputeHash(fs))
						sb.Append(b.ToString("x2").ToLower());
				}
			} catch (IOException exc) {
				LOGGER.Error("IOException on "+filePath+" with message:"+exc.Message);
				return null;
			}
			
			return sb.ToString();
		}

	}
}

