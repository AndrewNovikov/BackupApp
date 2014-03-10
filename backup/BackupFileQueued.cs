using System;

namespace backup {
	public class BackupFileQueued {

		public BackupFile File {
			get;
			set;
		}

		public Action<BackupFile> OnWorkerEnds {
			get;
			set;
		}

		public BackupFileQueued(BackupFile file, Action<BackupFile> onWorkerEnds) {
			File = file;
			OnWorkerEnds = onWorkerEnds;
		}
	}
}