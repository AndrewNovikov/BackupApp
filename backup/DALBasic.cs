using NLog;
using System;
using System.Data;
using MySql.Data.MySqlClient;
using System.Collections.Generic;

namespace backup {
	public class DALBasic: IDisposable {
		private static readonly Logger LOGGER = LogManager.GetCurrentClassLogger();
		private static readonly object _lock = new object();

		#region "Singleton"
		DALBasic() {
		}

		public static DALBasic Instance {
			get {
				return SingletonFactory.instance;
			}
		}

		class SingletonFactory {
			static SingletonFactory() {}
			internal static readonly DALBasic instance = new DALBasic();
		}
		#endregion

		public void Dispose() {
			_db.Dispose();
		}

		//[ThreadStatic]
		private static MySqlConnection _db;
		private static MySqlConnection db {
			get {
				if (_db == null || _db.State == ConnectionState.Closed || _db.State == ConnectionState.Broken) {
					lock (_lock) {
						if (_db != null && (_db.State == ConnectionState.Closed || _db.State == ConnectionState.Broken)) {
							_db.Dispose();
							_db = null;
						}
						if (_db == null) {
							_db = new MySqlConnection(MainClass.DbConnectionString);
							_db.Open();
						}
					}
				}
				if (_db.State != ConnectionState.Open) {
					LOGGER.Error("SQL connection is in " + _db.State + " state. Timeout=" + _db.ConnectionTimeout);
					///Console.WriteLine("SQL connection is in " + _db.State + " state. Timeout=" + _db.ConnectionTimeout);
				}
				return _db;
			}
		}

		private static MySqlParameter[] parameters = new MySqlParameter[]{
			new MySqlParameter(),
			new MySqlParameter(),
			new MySqlParameter(),
			new MySqlParameter()
		};

		private static MySqlParameter GetParameter(int index, string name, object value) {
			MySqlParameter result = parameters[index];
			result.ResetDbType();
			result.ParameterName = name;
			result.Value = value;
			return result;
		}

		private static IDbCommand CreateMySqlCommand(MySqlConnection db, int timeout = 240) {
			IDbCommand result = db.CreateCommand();
			result.CommandTimeout = timeout;
			return result;
		}

		/*private static Dictionary<DbItemSearchInfo, DbItemDataInfo> allDbItems;
		public static bool GetDbCacheDataForServer(string server, DbItemSearchInfo searchInfo, out DbItemDataInfo dataInfo) {
			if (allDbItems == null) {
				lock (allDbItemsLock) {
					if (allDbItems == null) {
						allDbItems = DALBasic.GetAllDbItemsForServer(System.Environment.MachineName);
					}
				}
			}
			return allDbItems.TryGetValue(searchInfo, out dataInfo);
		}

		private static Dictionary<DbItemSearchInfo, DbItemDataInfo> GetAllDbItemsForServer(string server) {
			Dictionary<DbItemSearchInfo, DbItemDataInfo> result = new Dictionary<DbItemSearchInfo, DbItemDataInfo>();
			lock (_lock) {
				using (IDbCommand cmd = CreateMySqlCommand(db)) {
					cmd.CommandText = 
						"SELECT paths.path, paths.mTime, data.size, data.md5, data.dataId, paths.pathId, data.ready FROM paths INNER JOIN data ON paths.dataId = data.dataId " +
						"WHERE paths.server = @server";
					cmd.Parameters.Add(GetParameter(0, "server", server));
					IDataReader reader = cmd.ExecuteReader();
					try {
						while (reader.Read()) {
							DbItemSearchInfo search = new DbItemSearchInfo(
								reader.GetString(0),
								reader.GetDateTime(1),
								reader.GetInt64(2)
								);
							DbItemDataInfo data = new DbItemDataInfo(
								reader.GetString(3),
								reader.GetInt64(4),
								reader.GetInt64(5),
								reader.GetBoolean(6)
								);
							result.Add(search, data);
						}
					} finally {
						cmd.Parameters.Clear();
						reader.Close();
						reader.Dispose();
					}
				}
			}
			return result;
		}*/

		private static FileSystemCache GetAllDbItemsForServer(string server) {
			FileSystemCache result = new FileSystemCache();
			lock (_lock) {
				using (IDbCommand cmd = CreateMySqlCommand(db, 3600)) {
					cmd.CommandText = 
						"Select paths.path, paths.mTime, data.size, data.md5, data.dataId, paths.pathId, data.ready " +
							"From paths Inner Join " +
							"(select paths.path as path, MAX(paths.mTime) as mTime, MAX(paths.pathId) as pathId from paths " +
							"where paths.server = @server group by paths.path) lastPaths on " +
							"paths.path = lastPaths.path and paths.mTime = lastPaths.mTime and paths.pathId = lastPaths.pathId " +
							"Inner Join data On paths.dataId = data.dataId Where paths.server = @server";
						//"SELECT paths.path, paths.mTime, data.size, data.md5, data.dataId, paths.pathId, data.ready FROM paths INNER JOIN data ON paths.dataId = data.dataId " +
						//	"WHERE paths.server = @server";
					cmd.Parameters.Add(GetParameter(0, "server", server));
					IDataReader reader = cmd.ExecuteReader();
					try {
						while (reader.Read()) {
							string path = null;
							try {
								path = reader.GetString(0);
								result.Add(path, reader.GetDateTime(1), reader.GetInt64(2),
							           	reader.GetString(3), reader.GetInt64(4), reader.GetInt64(5), reader.GetBoolean(6));
							} catch (Exception exc) {
								exc.WriteToLog("Path " + path + " exception: ");
								throw;
							}
						}
					} finally {
						cmd.Parameters.Clear();
						reader.Close();
						reader.Dispose();
					}
				}
			}
			return result;
		}

		private static FileSystemCache allDbItems;
		private static readonly object allDbItemsLock = new object();
		public bool GetDbCacheDataForServer(string server, string path, DateTime mTime, long size, ref string md5, ref long dbDataId, ref bool? ready, out long dbPathId) {
			if (allDbItems == null) {
				lock (allDbItemsLock) {
					if (allDbItems == null) {
						LOGGER.Info("Loading db cache");
						allDbItems = DALBasic.GetAllDbItemsForServer(server);
						LOGGER.Info("Db cache loaded (" + allDbItems.TotalItems + " items)");
					}
				}
			}
			return allDbItems.TryGet(path, mTime, size, ref md5, ref dbDataId, ref ready, out dbPathId);
		}

		public IEnumerable<RecoverInfo> GetAllPathsStartedWith(int scanId, string pathStart) {
			Dictionary<string, RecoverInfo> result = new Dictionary<string, RecoverInfo>();
			lock (_lock) {
				using (IDbCommand cmd = CreateMySqlCommand(db, 1200)) {
					cmd.CommandText = 
						"Select paths.path, data.md5, stat.maxTime, paths.mTime " +
						"From paths " +
						"INNER JOIN (SELECT status.pathId, MAX(status.time) AS maxTime FROM STATUS WHERE status.scanId = @scanId GROUP BY status.pathId) " +
						"stat On stat.pathId = paths.pathId " +
						"Inner Join data On data.dataId = paths.dataId";						
					if (!string.IsNullOrEmpty(pathStart)) {
						cmd.CommandText+=" Where paths.path LIKE @pathStart";
					}
					cmd.Parameters.Add(GetParameter(0, "scanId", scanId));
					if (!string.IsNullOrEmpty(pathStart)) {
						cmd.Parameters.Add(GetParameter(1, "pathStart", pathStart.Replace(@"\", @"\\") + "%"));
					}
					IDataReader reader = cmd.ExecuteReader();
					try {
						while (reader.Read()) {
							string path = reader.GetString(0);
							string md5 = reader.GetString(1);
							DateTime backupTime = reader.GetDateTime(2);
							DateTime lastModTime = reader.GetDateTime(3);
							
							RecoverInfo rec;
							if (!result.TryGetValue(md5, out rec)) {
								rec = new RecoverInfo(md5, path, backupTime, lastModTime);
								result.Add(md5, rec);
							} else {
								rec.Add(path, backupTime, lastModTime);
							}
						}
					} finally {
						cmd.Parameters.Clear();
						reader.Close();
						reader.Dispose();
					}
				}
			}
			return result.Values;
		}

		public IEnumerable<RecoverInfo> GetAllPathsStartedWith(string server, string pathStart) {
			Dictionary<string, RecoverInfo> result = new Dictionary<string, RecoverInfo>();
			lock (_lock) {
				using (IDbCommand cmd = CreateMySqlCommand(db, 1200)) {
					cmd.CommandText = 
						"Select paths.path, data.md5, stat.time, paths.mTime " +
						"From paths " +
						"Inner Join (Select status.pathId, Max(status.time) as time From status Group By status.pathId) " +
						"stat On stat.pathId = paths.pathId " +
						"Inner Join data On data.dataId = paths.dataId " +
						"Where paths.server = @server AND paths.path LIKE @pathStart";
					cmd.Parameters.Add(GetParameter(0, "server", server));
					cmd.Parameters.Add(GetParameter(1, "pathStart", pathStart.Replace(@"\", @"\\") + "%"));
					IDataReader reader = cmd.ExecuteReader();
					try {
						while (reader.Read()) {
							string path = reader.GetString(0);
							string md5 = reader.GetString(1);
							DateTime backupTime = reader.GetDateTime(2);
							DateTime lastModTime = reader.GetDateTime(3);

							RecoverInfo rec;
							if (!result.TryGetValue(md5, out rec)) {
								rec = new RecoverInfo(md5, path, backupTime, lastModTime);
								result.Add(md5, rec);
							} else {
								rec.Add(path, backupTime, lastModTime);
							}
						}
					} finally {
						cmd.Parameters.Clear();
						reader.Close();
						reader.Dispose();
					}
				}
			}
			return result.Values;
		}

		/*public IEnumerable<PathToClean> GetOrphanPaths(int limit) {
			List<PathToClean> result = new List<PathToClean>();
			lock (_lock) {
				using (IDbCommand cmd = CreateMySqlCommand(db, 3600)) {
					cmd.CommandText = 
						"SELECT data.dataId, paths.pathId, data.md5 FROM status " +
						"RIGHT JOIN paths ON (status.pathId = paths.pathId) " +
						"LEFT JOIN data ON (paths.dataId = data.dataId) " +
						"WHERE (status.scanId is null) " +
						"ORDER BY data.dataId ASC";
					if (limit != 0) cmd.CommandText += " LIMIT " + limit;
					IDataReader reader = cmd.ExecuteReader();
					try {
						while (reader.Read()) {
							result.Add(new PathToClean(reader.GetInt64(0), reader.GetInt64(1), reader.GetString(2)));
						}
					} finally {
						cmd.Parameters.Clear();
						reader.Close();
						reader.Dispose();
					}
				}
			}
			return result;
		}*/

		public IEnumerable<long> GetOrphanPathId(int limit) {
			List<long> result = new List<long>();
			lock (_lock) {
				using (IDbCommand cmd = CreateMySqlCommand(db, 3600)) {
					cmd.CommandText = 
						"SELECT paths.pathId FROM status " +
						"RIGHT JOIN paths ON (status.pathId = paths.pathId) " +
						"WHERE (status.pathId IS NULL)";
					if (limit != 0) cmd.CommandText += " LIMIT " + limit;
					IDataReader reader = cmd.ExecuteReader();
					try {
						while (reader.Read()) {
							result.Add(reader.GetInt64(0));
						}
					} finally {
						cmd.Parameters.Clear();
						reader.Close();
						reader.Dispose();
					}
				}
			}
			return result;
		}

		public IEnumerable<KeyValuePair<long,string>> GetOrphanDataId(int limit) {
			List<KeyValuePair<long,string>> result = new List<KeyValuePair<long,string>>();
			lock (_lock) {
				using (IDbCommand cmd = CreateMySqlCommand(db, 3600)) {
					cmd.CommandText = 
						"SELECT data.dataId, data.md5 FROM paths " +
						"RIGHT JOIN data ON (paths.dataId = data.dataId) " +
						"WHERE (paths.dataId IS NULL)";
					if (limit != 0) cmd.CommandText += " LIMIT " + limit;
					IDataReader reader = cmd.ExecuteReader();
					try {
						while (reader.Read()) {
							result.Add(new KeyValuePair<long, string>(reader.GetInt64(0), reader.GetString(1)));
						}
					} finally {
						cmd.Parameters.Clear();
						reader.Close();
						reader.Dispose();
					}
				}
			}
			return result;
		}

		public void DeletePathRow(long pathId) {
			lock (_lock) {
				using (IDbCommand cmd = CreateMySqlCommand(db)) {
					cmd.CommandText = "DELETE FROM paths WHERE pathId = @pathId";
					cmd.Parameters.Add(GetParameter(0, "pathId", pathId));
					try {
						cmd.ExecuteNonQuery();
					} finally {
						cmd.Parameters.Clear();
					}
				}
			}
		}

		public void DeleteDataRow(long dataId) {
			lock (_lock) {
				using (IDbCommand cmd = CreateMySqlCommand(db)) {
					cmd.CommandText = "DELETE FROM `data` WHERE dataId = @dataId";
					cmd.Parameters.Add(GetParameter(0, "dataId", dataId));
					try {
						cmd.ExecuteNonQuery();
					} finally {
						cmd.Parameters.Clear();
					}
				}
			}
		}

		public bool ItemInDb(string server, BackupItem item, ref string md5, ref long dataId, ref bool? ready, out long pathId) {
			bool result = false;
			lock (_lock) {
				using (IDbCommand cmd = CreateMySqlCommand(db)) {
					cmd.CommandText = 
						"SELECT paths.path, data.md5, data.dataId, paths.pathId, data.ready FROM paths INNER JOIN data ON paths.dataId = data.dataId" +
						" WHERE paths.server = @server" +
						" AND paths.path = @path" +
						" AND paths.mTime = @mTime" +
						" AND data.size = @size";
					cmd.Parameters.Add(GetParameter(0, "server", server));
					cmd.Parameters.Add(GetParameter(1, "path", item.FullName));
					cmd.Parameters.Add(GetParameter(2, "mTime", item.LastWriteTime));
					cmd.Parameters.Add(GetParameter(3, "size", item.Length));
					IDataReader reader = cmd.ExecuteReader();
					try {
						result = reader.Read();
						if (result && item.FullName == reader.GetString(0)) {  //additional check for russian letters
							md5 = reader.GetString(1);
							dataId = reader.GetInt64(2);
							pathId = reader.GetInt64(3);
							ready = reader.GetBoolean(4);
						} else {
							pathId = -1;
						}
						if (reader.Read())
							throw new ApplicationException("More than 1 records with path " + item.FullName + ", last write time " + item.LastWriteTime.ToString("yyyy-MM-dd") + " and size " + item.Length + " in database");
					} finally {
						cmd.Parameters.Clear();
						reader.Close();
						reader.Dispose();
					}
				}
			}
			return result;
		}

		public long GetDataId(string md5, long itemLength) {
			long result = -1;
			lock (_lock) {
				using (IDbCommand cmd = CreateMySqlCommand(db)) {
					cmd.CommandText = "SELECT dataId, size FROM data WHERE md5 = @md5";
					cmd.Parameters.Add(GetParameter(0, "md5", md5));
					IDataReader reader = cmd.ExecuteReader();
					try {
						if (reader.Read()) {
							result = reader.GetInt64(0);
							long size = reader.GetInt64(1);
							if (size != itemLength)
								throw new ApplicationException("File or folder with md5 " + md5 + " exist in db but with size " + size + " and not " + itemLength);
						}
						if (reader.Read())
							throw new ApplicationException("More than 1 records with md5 " + md5);
					} finally {
						cmd.Parameters.Clear();
						reader.Close();
						reader.Dispose();
					}
				}
			}
			return result;
		}

		public bool AddIfNewData(string md5, BackupItem item, out long dataId) {
			bool result;
			lock (_lock) {
				using (IDbCommand cmd = CreateMySqlCommand(db)) {
					cmd.CommandText = 
						"INSERT INTO data (md5,size,ready) " +
						"SELECT * FROM (SELECT @md5 as md5,@size as size,@ready as ready) AS tmp WHERE NOT EXISTS (SELECT md5 FROM data WHERE md5=@md5) LIMIT 1;";
					cmd.Parameters.Add(GetParameter(0, "md5", md5));
					cmd.Parameters.Add(GetParameter(1, "size", item.Length));
					cmd.Parameters.Add(GetParameter(2, "ready", item.Length == 0));

					int rowsInserted = cmd.ExecuteNonQuery();
					result = rowsInserted != 0;

					cmd.CommandText = "SELECT dataId,size FROM data WHERE md5=@md5";
					IDataReader reader = cmd.ExecuteReader();
					try {
						if (reader.Read()) {
							dataId = reader.GetInt64(0);
							long size = reader.GetInt64(1);
							if (size != item.Length)
								throw new ApplicationException("Item " + item.FullName + " exist in db with md5=" + md5 + " but the size=" + size + " rather then " + item.Length);
						} else {
							throw new ApplicationException("Exception occured while inserting data about" + item.FullName);
						}
					} finally {
						cmd.Parameters.Clear();
						reader.Close();
						reader.Dispose();
					}
				}
			}
			return result;
		}

		public void SetDataReady(long dataId) {
			lock (_lock) {
				using (IDbCommand cmd = CreateMySqlCommand(db)) {
					cmd.CommandText = "UPDATE data SET ready = 1 WHERE dataId = @dataId";
					cmd.Parameters.Add(GetParameter(0, "dataId", dataId));
					try {
						cmd.ExecuteNonQuery();
					} finally {
						cmd.Parameters.Clear();
					}
				}
			}
		}

		public bool GetDataReady(long dataId) {
			bool result;
			lock (_lock) {
				using (IDbCommand cmd = CreateMySqlCommand(db)) {
					cmd.CommandText = "SELECT ready FROM data WHERE dataId = @dataId";
					cmd.Parameters.Add(GetParameter(0, "dataId", dataId));
					IDataReader reader = cmd.ExecuteReader();
					try {
						if (reader.Read()) {
							result = reader.GetBoolean(0);
						} else {
							throw new ApplicationException("No data with id " + dataId + " has been found in GetDataReady");
						}
					} finally {
						cmd.Parameters.Clear();
						reader.Close();
						reader.Dispose();
					}
				}
			}
			return result;
		}

		public long AddNewPath(string server, long dataId, BackupItem item) {
			long result;
			lock (_lock) {
				using (IDbCommand cmd = CreateMySqlCommand(db)) {
					cmd.CommandText = "INSERT INTO paths (dataId,server,path,mTime) VALUES (@dataId,@server,@path,@mTime)";
					cmd.Parameters.Add(GetParameter(0, "dataId", dataId));
					cmd.Parameters.Add(GetParameter(1, "server", server));
					cmd.Parameters.Add(GetParameter(2, "path", item.FullName));
					cmd.Parameters.Add(GetParameter(3, "mTime", item.LastWriteTime));
					cmd.ExecuteNonQuery();

					cmd.CommandText = "SELECT last_insert_id()";
					IDataReader reader = cmd.ExecuteReader();
					try {
						if (reader.Read()) {
							result = reader.GetInt64(0);
						} else {
							throw new ApplicationException("Exception occured while inserting data about path " + item.FullName);
						}
					} finally {
						cmd.Parameters.Clear();
						reader.Close();
						reader.Dispose();
					}
				}
			}
			return result;
		}

		public void SetStatus(int scanId, long pathId, BackupFile.Status status) {
			lock (_lock) {
				using (IDbCommand cmd = CreateMySqlCommand(db)) {
					cmd.CommandText = "INSERT INTO status (pathId,scanId,time,status) VALUES (@pathId,@scanId,@time,@status)";
					cmd.Parameters.Add(GetParameter(0, "pathId", pathId));
					cmd.Parameters.Add(GetParameter(1, "scanId", scanId));
					cmd.Parameters.Add(GetParameter(2, "time", DateTime.Now));
					cmd.Parameters.Add(GetParameter(3, "status", status));
					try {
						cmd.ExecuteNonQuery();
					} finally {
						cmd.Parameters.Clear();
					}
				}
			}
		}

		public int CreateNewScan(string server) {
			int result;
			lock (_lock) {
				using (IDbCommand cmd = CreateMySqlCommand(db)) {
					cmd.CommandText = "INSERT INTO scans (startTime,server) VALUES (@startTime,@server)";
					cmd.Parameters.Add(GetParameter(0, "startTime", DateTime.Now));
					cmd.Parameters.Add(GetParameter(1, "server", server));
					cmd.ExecuteNonQuery();

					
					cmd.CommandText = "SELECT last_insert_id()";
					IDataReader reader = cmd.ExecuteReader();
					try {
						if (reader.Read()) {
							result = reader.GetInt32(0);
						} else {
							throw new ApplicationException("Exception occured while creating new scan");
						}
					} finally {
						cmd.Parameters.Clear();
						reader.Close();
						reader.Dispose();
					}
				}
			}
			return result;
		}

		public void EndScan(int scanId) {
			lock (_lock) {
				using (IDbCommand cmd = CreateMySqlCommand(db)) {
					cmd.CommandText = "UPDATE scans SET endTime = @endTime WHERE scanId = @scanId";
					cmd.Parameters.Add(GetParameter(0, "endTime", DateTime.Now));
					cmd.Parameters.Add(GetParameter(1, "scanId", scanId));
					try {
						cmd.ExecuteNonQuery();
					} finally {
						cmd.Parameters.Clear();
					}
				}
			}
		}

	}
}

