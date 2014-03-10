using System;

namespace backup {
	public class BackupFileQueued {

		public BackupFile File {
			get;
			set;
		}

		public Action OnWorkerEnds {
			get;
			set;
		}

		public BackupFileQueued(BackupFile file, Action onWorkerEnds) {
			File = file;
			OnWorkerEnds = onWorkerEnds;
		}
	}
}