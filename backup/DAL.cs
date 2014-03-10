using NLog;
using System;
using MySql.Data.MySqlClient;
using System.Collections.Generic;

namespace backup {
	public class DAL {
		private static readonly Logger LOGGER = LogManager.GetCurrentClassLogger();
		private static uint maxAttempts = 2;

		#region "Singleton"
		DAL() {
		}
		
		public static DAL Instance {
			get {
				return SingletonFactory.instance;
			}
		}
		
		class SingletonFactory {
			static SingletonFactory() {}
			internal static readonly DAL instance = new DAL();
		}
		#endregion

		public bool GetDbCacheDataForServer(string server, string path, DateTime mTime, long size, ref string md5, ref long dbDataId, ref bool? ready, out long dbPathId) {
			return IntGetDbCacheDataForServer(0, server, path, mTime, size, ref md5, ref dbDataId, ref ready, out dbPathId);
		}
		
		private bool IntGetDbCacheDataForServer(uint attempt, string server, string path, DateTime mTime, long size, ref string md5, ref long dbDataId, ref bool? ready, out long dbPathId) {
			try {
				return DALBasic.Instance.GetDbCacheDataForServer(server, path, mTime, size, ref md5, ref dbDataId, ref ready, out dbPathId);
			} catch (MySqlException) {
				if (attempt < maxAttempts) {
					LOGGER.Info("GetAllDbItems attempt No" + attempt + " failed");
					///Console.WriteLine("GetAllDbItems attempt No" + attempt + " failed");
					return IntGetDbCacheDataForServer(attempt + 1, server, path, mTime, size, ref md5, ref dbDataId, ref ready, out dbPathId);
				} else {
					throw;
				}
			}
		}

		public IEnumerable<long> GetOrphanPathId(int limit) {
			return DALBasic.Instance.GetOrphanPathId(limit);
		}

		public IEnumerable<KeyValuePair<long,string>> GetOrphanDataId(int limit) {
			return DALBasic.Instance.GetOrphanDataId(limit);
		}

		public IEnumerable<RecoverInfo> GetAllPathsStartedWith(int scanId, string pathStart) {
			return IntGetAllPathsStartedWith(0, scanId, pathStart);
		}
		
		private IEnumerable<RecoverInfo> IntGetAllPathsStartedWith(uint attempt, int scanId, string pathStart) {
			try {
				return DALBasic.Instance.GetAllPathsStartedWith(scanId, pathStart);
			} catch (MySqlException) {
				if (attempt < maxAttempts) {
					LOGGER.Info("GetAllPathsStartedWith attempt No" + attempt + " failed");
					return IntGetAllPathsStartedWith(attempt + 1, scanId, pathStart);
				} else {
					throw;
				}
			}
		}

		public IEnumerable<RecoverInfo> GetAllPathsStartedWith(string server, string pathStart) {
			return IntGetAllPathsStartedWith(0, server, pathStart);
		}

		private IEnumerable<RecoverInfo> IntGetAllPathsStartedWith(uint attempt, string server, string pathStart) {
			try {
				return DALBasic.Instance.GetAllPathsStartedWith(server, pathStart);
			} catch (MySqlException) {
				if (attempt < maxAttempts) {
					LOGGER.Info("GetAllPathsStartedWith attempt No" + attempt + " failed");
					return IntGetAllPathsStartedWith(attempt + 1, server, pathStart);
				} else {
					throw;
				}
			}
		}

		public bool ItemInDb(string server, BackupItem item, ref string md5, ref long dataId, ref bool? ready, out long pathId) {
			return IntItemInDb(0, server, item, ref md5, ref dataId, ref ready, out pathId);
		}

		private bool IntItemInDb(uint attempt, string server, BackupItem item, ref string md5, ref long dataId, ref bool? ready, out long pathId) {
			try {
				return DALBasic.Instance.ItemInDb(server, item, ref md5, ref dataId, ref ready, out pathId);
			} catch (MySqlException) {
				if (attempt < maxAttempts) {
					LOGGER.Info("ItemInDb attempt No" + attempt + " failed");
					///Console.WriteLine("ItemInDb attempt No" + attempt + " failed");
					return IntItemInDb(attempt + 1, server, item, ref md5, ref dataId, ref ready, out pathId);
				} else {
					throw;
				}
			}
		}

		public long GetDataId(string md5, long itemLength) {
			return IntGetDataId(0, md5, itemLength);
		}

		private long IntGetDataId(uint attempt, string md5, long itemLength) {
			try {
				return DALBasic.Instance.GetDataId(md5, itemLength);
			} catch (MySqlException) {
				if (attempt < maxAttempts) {
					LOGGER.Info("GetDataId attempt No" + attempt + " failed");
					///Console.WriteLine("GetDataId attempt No" + attempt + " failed");
					return IntGetDataId(attempt + 1, md5, itemLength);
				} else {
					throw;
				}
			}
		}

		public bool AddIfNewData(string md5, BackupItem item, out long dataId) {
			return IntAddIfNewData(0, md5, item, out dataId);
		}

		private bool IntAddIfNewData(uint attempt, string md5, BackupItem item, out long dataId) {
			try {
				return DALBasic.Instance.AddIfNewData(md5, item, out dataId);
			} catch (MySqlException) {
				if (attempt < maxAttempts) {
					LOGGER.Info("AddIfNewData attempt No" + attempt + " failed");
					///Console.WriteLine("AddIfNewData attempt No" + attempt + " failed");
					return IntAddIfNewData(attempt + 1, md5, item, out dataId);
				} else {
					throw;
				}
			}
		}

		public void SetDataReady(long dataId) {
			IntSetDataReady(0, dataId);
		}

		private void IntSetDataReady(uint attempt, long dataId) {
			try {
				DALBasic.Instance.SetDataReady(dataId);
			} catch (MySqlException) {
				if (attempt < maxAttempts) {
					LOGGER.Info("SetDataReady attempt No" + attempt + " failed");
					///Console.WriteLine("SetDataReady attempt No" + attempt + " failed");
					IntSetDataReady(attempt + 1, dataId);
				} else {
					throw;
				}
			}
		}

		public bool GetDataReady(long dataId) {
			return IntGetDataReady(0, dataId);
		}

		private bool IntGetDataReady(uint attempt, long dataId) {
			try {
				return DALBasic.Instance.GetDataReady(dataId);
			} catch (MySqlException) {
				if (attempt < maxAttempts) {
					LOGGER.Info("GetDataReady attempt No" + attempt + " failed");
					///Console.WriteLine("GetDataReady attempt No" + attempt + " failed");
					return IntGetDataReady(attempt + 1, dataId);
				} else {
					throw;
				}
			}
		}

		public long AddNewPath(string server, long dataId, BackupItem item) {
			return IntAddNewPath(0, server, dataId, item);
		}

		private long IntAddNewPath(uint attempt, string server, long dataId, BackupItem item) {
			try {
				return DALBasic.Instance.AddNewPath(server, dataId, item);
			} catch (MySqlException) {
				if (attempt < maxAttempts) {
					LOGGER.Info("AddNewPath attempt No" + attempt + " failed");
					///Console.WriteLine("AddNewPath attempt No" + attempt + " failed");
					return IntAddNewPath(attempt + 1, server, dataId, item);
				} else {
					throw;
				}
			}
		}

		public void DeletePathRow(long pathId) {
			IntDeletePathRow(0, pathId);
		}
		
		private void IntDeletePathRow(uint attempt, long pathId) {
			try {
				DALBasic.Instance.DeletePathRow(pathId);
			} catch (MySqlException) {
				if (attempt < maxAttempts) {
					LOGGER.Info("DeletePathRow attempt No" + attempt + " failed");
					IntDeletePathRow(attempt + 1, pathId);
				} else {
					throw;
				}
			}
		}

		public void DeleteDataRow(long dataId) {
			IntDeleteDataRow(0, dataId);
		}
		
		private void IntDeleteDataRow(uint attempt, long dataId) {
			try {
				DALBasic.Instance.DeleteDataRow(dataId);
			} catch (MySqlException) {
				if (attempt < maxAttempts) {
					LOGGER.Info("DeleteDataRow attempt No" + attempt + " failed");
					IntDeleteDataRow(attempt + 1, dataId);
				} else {
					throw;
				}
			}
		}

		public void SetStatus(int scanId, long pathId, BackupFile.Status status) {
			IntSetStatus(0, scanId, pathId, status);
		}

		private void IntSetStatus(uint attempt, int scanId, long pathId, BackupFile.Status status) {
			try {
				DALBasic.Instance.SetStatus(scanId, pathId, status);
			} catch (MySqlException) {
				if (attempt < maxAttempts) {
					LOGGER.Info("SetStatus attempt No" + attempt + " failed");
					///Console.WriteLine("SetStatus attempt No" + attempt + " failed");
					IntSetStatus(attempt + 1, scanId, pathId, status);
				} else {
					throw;
				}
			}
		}

		public int CreateNewScan(string server) {
			return IntCreateNewScan(0, server);
		}

		private int IntCreateNewScan(uint attempt, string server) {
			try {
				return DALBasic.Instance.CreateNewScan(server);
			} catch (MySqlException) {
				if (attempt < maxAttempts) {
					LOGGER.Info("CreateNewScan attempt No" + attempt + " failed");
					///Console.WriteLine("CreateNewScan attempt No" + attempt + " failed");
					return IntCreateNewScan(attempt + 1, server);
				} else {
					throw;
				}
			}
		}

		public void EndScan(int scanId) {
			IntEndScan(0, scanId);
		}

		private void IntEndScan(uint attempt, int scanId) {
			try {
				DALBasic.Instance.EndScan(scanId);
			} catch (MySqlException) {
				if (attempt < maxAttempts) {
					LOGGER.Info("EndScan attempt No" + attempt + " failed");
					///Console.WriteLine("EndScan attempt No" + attempt + " failed");
					IntEndScan(attempt + 1, scanId);
				} else {
					throw;
				}
			}
		}
	}
}

