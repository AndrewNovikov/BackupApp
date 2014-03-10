using System;

namespace backup {
	public interface IDecryptor {
		long Decrypt(string md5, string targetPath);
	}
}

