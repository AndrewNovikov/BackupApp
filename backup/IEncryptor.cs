using System;

namespace backup {
	public interface IEncryptor {
		long Encrypt(BackupFile file);
	}
}

