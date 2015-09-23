using System;

namespace backup {
	public interface IBackupEngine {
		long Backup(IBackupItem file);
	}
}

