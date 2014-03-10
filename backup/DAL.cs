using NLog;
using System;
using System.IO;
using System.Data;
using MySql.Data.MySqlClient;
using System.Collections.Generic;

namespace backup {
	public static class DAL {
		//private static readonly Logger LOGGER = LogManager.GetCurrentClassLogger();
		private static readonly string connectionString = "Server=z-server;Database=backup;User ID=root;Password=36x;Pooling=false;charset=utf8";
		private static object _lock = new object();
		//[ThreadStatic]
		private static Lazy<MySqlConnection> _db = new Lazy<MySqlConnection>(() => {
			MySqlConnection result = new MySqlConnection(connectionString);
			result.Open();
			return result;
		});

		/*private static readonly Lazy<DAL> _instance = new Lazy<DAL>(() => new DAL());
		public static DAL Instance {
			get {
				return _instance.Value;
			}
		}*/
		private static MySqlConnection db {
			get {
				return _db.Value;
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

		/*public DAL() {
			//db = new MySqlConnection(connectionString);
			//db.Open();
		}*/

		/*public void Dispose() {
			//db.Close();
			//db.Dispose();
		}*/
		private static Lazy<Dictionary<DbItemSearchInfo, DbItemDataInfo>> allDbItems = new Lazy<Dictionary<DbItemSearchInfo, DbItemDataInfo>>(() => {
			/*var parovoz = DAL.GetAllDbItems("parovoz");

			long size = 0;
			using (Stream stream = new MemoryStream())
			{
				System.Runtime.Serialization.IFormatter formatter = new  System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
				
				formatter.Serialize(stream, parovoz);
				size = stream.Length;
			}*/

			/*foreach (var t in parovoz) {
				size += System.Runtime.InteropServices.Marshal.SizeOf(t.Key);
				size += System.Runtime.InteropServices.Marshal.SizeOf(t.Value);
			}
			DbItemSearchInfo[] searchs = new DbItemSearchInfo[parovoz.Count];
			parovoz.Keys.CopyTo(searchs, 0);
			DbItemDataInfo[] datas = new DbItemDataInfo[parovoz.Count];
			parovoz.Values.CopyTo(datas, 0);

			int size = System.Runtime.InteropServices.Marshal.SizeOf(searchs);
			size += System.Runtime.InteropServices.Marshal.SizeOf(datas);*/

			//LOGGER.Info("size of cache = "+size);
			return DAL.GetAllDbItems(System.Environment.MachineName);
		});

		public static bool GetDbCacheData(DbItemSearchInfo searchInfo, out DbItemDataInfo dataInfo) {
			return allDbItems.Value.TryGetValue(searchInfo, out dataInfo);
		}

		private static Dictionary<DbItemSearchInfo, DbItemDataInfo> GetAllDbItems(string server) {
			Dictionary<DbItemSearchInfo, DbItemDataInfo> result = new Dictionary<DbItemSearchInfo, DbItemDataInfo>();
			lock (_lock) {
				using (IDbCommand cmd = db.CreateCommand()) {
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
		}

		public static bool ItemInDb(string server, BackupItem item, ref string md5, ref long dataId, ref bool? ready, out long pathId) {
			bool result = false;
			lock (_lock) {
				using (IDbCommand cmd = db.CreateCommand()) {
					cmd.CommandText = 
						"SELECT paths.path, data.md5, data.dataId, paths.pathId, data.ready FROM paths INNER JOIN data ON paths.dataId = data.dataId " +
						"WHERE paths.server = @server" +
						" AND paths.path = @path" +
						" AND paths.mTime = @mTime" +
						" AND data.size = @size";
					cmd.Parameters.Add(GetParameter(0, "server", server));
					cmd.Parameters.Add(GetParameter(1, "path", item.FullName));
					cmd.Parameters.Add(GetParameter(2, "mTime", item.LastWriteTime));
					cmd.Parameters.Add(GetParameter(3, "size", item.Length));
					IDataReader reader = cmd.ExecuteReader();
					try {
						result = reader.Read() && item.FullName == reader.GetString(0); //additional check for russian letters
						if (result) {
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

		public static long GetDataId(string md5, long itemLength) {
			long result = -1;
			lock (_lock) {
				using (IDbCommand cmd = db.CreateCommand()) {
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
			//}
		}

		/*public static long GetPathId(FileInfo file) {
			throw new NotImplementedException();
		}*/

		public static bool AddIfNewData(string md5, BackupItem item, out long dataId) {
			bool result;
			try {
				lock (_lock) {
					using (IDbCommand cmd = db.CreateCommand()) {
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
			} catch (Exception exc) {
				exc.WriteToLog("in AddIfNewData with item=" + item.FullName + " md5=" + md5 + " and size=" + item.Length + ":" + exc.Message);
				throw;
			}
			return result;
		}

		public static void SetDataReady(long dataId) {
			try {
				lock (_lock) {
					using (IDbCommand cmd = db.CreateCommand()) {
						cmd.CommandText = "UPDATE data SET ready = 1 WHERE dataId = @dataId";
						cmd.Parameters.Add(GetParameter(0, "dataId", dataId));
						try {
							cmd.ExecuteNonQuery();
						} finally {
							cmd.Parameters.Clear();
						}
					}
				}
			} catch (Exception exc) {
				exc.WriteToLog("in SetDataReady with dataId=" + dataId + ":" + exc.Message);
				throw;
			}
		}

		public static bool GetDataReady(long dataId) {
			bool result;
			try {
				lock (_lock) {
					using (IDbCommand cmd = db.CreateCommand()) {
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
			} catch (Exception exc) {
				exc.WriteToLog("in GetDataReady with dataId=" + dataId + ":" + exc.Message);
				throw;
			}
			return result;
		}

		/*private static bool FileExist(string md5, FileInfo file, ref long dataId) {
			bool result;
			using (IDbCommand cmd = db.CreateCommand()) {
				cmd.CommandText = "SELECT dataId,size FROM data WHERE md5=@md5";
				cmd.Parameters.Add(new MySqlParameter("md5", md5));
				cmd.ExecuteNonQuery();
				IDataReader reader = cmd.ExecuteReader();
				try {
					result = reader.Read();
					if (result) {
						dataId = reader.GetInt64(0);
						long size = reader.GetInt64(1);
						if (size != file.Length)
							throw new ApplicationException("File " + file.FullName + " with md5 " + md5 + " already exist in db but with size=" + size + " and not " + file.Length);
					}
				} finally {
					reader.Close();
					reader.Dispose();
				}
			}
			return result;
		}

		private static long AddFile(string md5, FileInfo file) {
			long result;
			using (IDbCommand cmd = db.CreateCommand()) {
				cmd.CommandText = "INSERT INTO data (md5,size) VALUES (@md5,@size)";
				cmd.Parameters.Add(new MySqlParameter("md5", md5));
				cmd.Parameters.Add(new MySqlParameter("size", file.Length));
				cmd.ExecuteNonQuery();
			
				cmd.CommandText = "SELECT last_insert_id()";
				IDataReader reader = cmd.ExecuteReader();
				try {
					if (reader.Read()) {
						result = reader.GetInt64(0);
					} else {
						throw new ApplicationException("Exception occured while inserting data about file " + file.FullName);
					}
				} finally {
					reader.Close();
					reader.Dispose();
				}
			}
			return result;
		}*/

		public static long AddNewPath(string server, long dataId, BackupItem item) {
			long result;
			try {
				lock (_lock) {
					using (IDbCommand cmd = db.CreateCommand()) {
						//cmd.CommandText = "INSERT INTO folders (folder) VALUES (@folder); SELECT last_insert_id();";
						//cmd.Parameters.Add(new MySqlParameter("folder", item.FolderPath));

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
			} catch (Exception exc) {
				exc.WriteToLog("in AddNewPath with dataId=" + dataId + " and file=" + item.FullName + ":");
				throw;
			}
			return result;
		}

		public static void SetStatus(int scanId, long pathId, BackupFile.Status status) {
			try {
				lock (_lock) {
					using (IDbCommand cmd = db.CreateCommand()) {
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
			} catch (Exception exc) {
				exc.WriteToLog("in SetStatus with pathId=" + pathId + ":" + exc.Message);
				throw;
			}
		}

		public static int CreateNewScan() {
			int result;
			try {
				lock (_lock) {
					using (IDbCommand cmd = db.CreateCommand()) {
						cmd.CommandText = "INSERT INTO scans (startTime) VALUES (@startTime)";
						cmd.Parameters.Add(GetParameter(0, "startTime", DateTime.Now));
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
			} catch (Exception exc) {
				exc.WriteToLog("in CreateNewScan error:" + exc.Message);
				throw;
			}
			return result;
		}

		public static void EndScan(int scanId) {
			try {
				lock (_lock) {
					using (IDbCommand cmd = db.CreateCommand()) {
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
			} catch (Exception exc) {
				exc.WriteToLog("in EndScan error:" + exc.Message);
				throw;
			}
		}

	}
}

