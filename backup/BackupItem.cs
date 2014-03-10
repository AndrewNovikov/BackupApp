using System;
using System.Collections.Generic;

namespace backup {
	public abstract class BackupItem {
		protected string _server;

		#region "Properties"
		public abstract string FullName {
			get;
		}

		public abstract string FolderPath {
			get;
		}

		public abstract DateTime LastWriteTime {
			get;
		}

		public abstract string Md5 {
			get;
		}

		public abstract long Length {
			get;
		}

		public abstract bool Encrypted {
			get;
		}

		public bool Encrypting {
			get;
			protected set;
		}

		public Worker Encryptor {
			get;
			set;
		}

		protected long _dbDataId = -2;
		protected long DbDataId {
			get {
				if (_dbDataId == -2) {
					_dbDataId = DAL.GetDataId(this.Md5, this.Length);
				}
				return _dbDataId;
			}
		}

		protected long _dbPathId = -2;
		protected long DbPathId {
			get {
				if (_dbPathId == -2) {
					this.DataOrPathIsNew();
				}
				return _dbPathId;
			}
		}

		public bool DataIsNew {
			get {
				return DbDataId == -1;
			}
		}
		#endregion

		#region "Constructors"
		public BackupItem(string server) {
			_server = server;
			Encrypting = false;
		}
		#endregion

		public abstract bool DataOrPathIsNew();

		public abstract void Encrypt();

		public void UpgradeStatus(int scanId, Status cur) {
			if (DbPathId == -1) throw new ApplicationException("No pathId is set. Forget to process new path?");
			DAL.SetStatus(scanId, DbPathId, cur);
		}

		private bool _newFileProcessed = false;
		public bool WriteNewData() {
			_newFileProcessed = false;
			
			bool result = DAL.AddIfNewData(this.Md5, this, out _dbDataId);
			
			lock (_eventsAPNFLock) {
				_newFileProcessed = true;
				while (_eventsAPNF.Count > 0) {
					_eventsAPNF.Dequeue()();
				}
			}
			
			return result;
		}
		
		private bool _newPathProcessed = false;
		public void WriteNewPath() {
			_newPathProcessed = false;
			_dbPathId = DAL.AddNewPath(_server, DbDataId, this);
			
			lock (_eventsAPNPLock) {
				_newPathProcessed = true;
				while (_eventsAPNP.Count > 0) {
					_eventsAPNP.Dequeue()();
				}
			}
		}
		
		#region "events"
		private void AfterN(Action act, Queue<Action> queue, bool onQueue, object _lock) {
			lock (_lock) {
				if (!onQueue)
					act();
				else if (act != null) {
					queue.Enqueue(act);
				}
			}
		}
		
		private Queue<Action> _eventsAPNF = new Queue<Action>();
		private object _eventsAPNFLock = new object();
		public void AfterProcessNewFile(Action act) {
			AfterN(act, _eventsAPNF, !_newFileProcessed, _eventsAPNFLock);
		}
		
		private Queue<Action> _eventsAPNP = new Queue<Action>();
		private object _eventsAPNPLock = new object();
		public void AfterProcessNewPath(Action act) {
			AfterN(act, _eventsAPNP, !_newPathProcessed, _eventsAPNPLock);
		}
		#endregion


		public enum Status {
			NotSet = 0,
			Exist = 1,
			Removed = 2
		}
	}
}

