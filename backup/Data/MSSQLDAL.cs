using NLog;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Text;

namespace backup {
	public class MSSQLDAL : IDisposable, IDAL {
		private static readonly Logger LOGGER = LogManager.GetCurrentClassLogger();
		//private static readonly object _lock = new object();

		#region "Singleton"
		MSSQLDAL() {
		}

		public static MSSQLDAL Instance {
			get {
				return SingletonFactory.instance;
			}
		}

		class SingletonFactory {
			static SingletonFactory() {}
			internal static readonly MSSQLDAL instance = new MSSQLDAL();
		}
		#endregion

		#region "Dispose"
		public void Dispose() {
			if (_db != null) {
				_db.Dispose();
				_db = null;
			}
		}
		#endregion

		#region "DB specific"
    private static readonly object _dblock = new object();
		private IDbConnection _db;
		private IDbConnection db {
			get {
				if (_db == null || _db.State == ConnectionState.Closed || _db.State == ConnectionState.Broken) {
          lock (_dblock) {
						if (_db != null) {
							_db.Dispose();
						}
						_db = new SqlConnection(backup.MainClass.Settings.DbConnectionString);
						_db.Open();
					}
				}
				if (_db.State != ConnectionState.Open) {
					LOGGER.Error("SQL connection is in " + _db.State + " state. Timeout=" + _db.ConnectionTimeout);
				}
				return _db;
			}
		}

		private DbParameter GetParameter(string name, object value, DbType? type = null) {
			DbParameter result = new SqlParameter();
			result.ResetDbType();
			result.ParameterName = name;
			result.Value = value;
			if (type.HasValue) {
				result.DbType = type.Value;
			}
			return result;
		}

		private IDbCommand CreateCommand(int timeout = 240) {
			IDbCommand result = db.CreateCommand();
			result.CommandTimeout = timeout;
			return result;
		}
		#endregion

		#region "IDAL"
    public void AddFileRecScan(long fileRecId, int scanId) {
      //lock (_lock) {
      using (IDbCommand cmd = CreateCommand()) {
        cmd.CommandText = "INSERT INTO fileScan (fileId, scanId, scanTime) VALUES (@fileId,@scanId,GETDATE());";
        cmd.Parameters.Add(GetParameter("fileId", fileRecId));
        cmd.Parameters.Add(GetParameter("scanId", scanId));
        cmd.ExecuteNonQuery();
      }
      //}
    }

    public backup.DataInfo AddIfNewData(string md5, long length) {
      backup.DataInfo result;
      //lock (_lock) {
      DateTime now = DateTime.Now;
      using (IDbCommand cmd = CreateCommand()) {
        cmd.CommandText =
          "merge data WITH (HOLDLOCK) as T " +
            "using (select @md5 as md5, @length as length, @onLocal as onLocal, @onRemote as onRemote, @inProgressStart as inProgressStart) as S " +
            "on (T.md5 = S.md5) and (T.length = S.length) " +
          "when not matched then " +
            "insert (md5, length, onLocal, onRemote, inProgressStart) values(S.md5, S.length, S.onLocal, S.onRemote, S.inProgressStart);" +
          "SELECT dataId, md5, length, onLocal, onRemote, inProgressStart FROM data WHERE (md5 = @md5 AND length = @length);";
        cmd.Parameters.Add(GetParameter("md5", md5));
        cmd.Parameters.Add(GetParameter("length", length));
        cmd.Parameters.Add(GetParameter("onLocal", false));
        cmd.Parameters.Add(GetParameter("onRemote", false));
        cmd.Parameters.Add(GetParameter("inProgressStart", now.Ticks));

        using (IDataReader reader = cmd.ExecuteReader()) {
          if (reader.Read()) {
            long _dataId = reader.GetInt64(0);
            string _md5 = reader.GetString(1);
            long _length = reader.GetInt64(2);
            bool _onLocal = reader.GetBoolean(3);
            bool _onRemote = reader.GetBoolean(4);
            DateTime _inProgressStart = new DateTime(reader.GetValue<long>(5));

            if (_inProgressStart == now) {
              result = new backup.DataInfo(_dataId, _md5, _length, _onLocal, _onRemote, null);
            } else {
              result = new backup.DataInfo(_dataId, _md5, _length, _onLocal, _onRemote, _inProgressStart);
            }

          } else {
            throw new ApplicationException("Exception occured while inserting data with md5=" + md5);
          }
        }
      }
      //}
      return result;
    }

    public long AddIfNewFileRec(string name, long mTimeTicks, long dataId, string relativeFolder, string rootFolder, string server) {
      if (name == string.Empty && (relativeFolder.Length > 0 && relativeFolder[relativeFolder.Length - 1] != System.IO.Path.DirectorySeparatorChar)) throw new ApplicationException("Path should ends with directory separator if it is a folder");

      long result;
      int rootFolderId = AddIfNewRootFolder(server, rootFolder);
      long relativeFolderId = AddIfNewRelativeFolder(rootFolderId, relativeFolder);
      //lock (_lock) {
      using (IDbCommand cmd = CreateCommand()) {
        cmd.CommandText =
          "merge files WITH (HOLDLOCK) as T " +
          "using (select @fileName as fileName, @mTime as mTime, @dataId as dataId, @folderId as folderId) as S " +
          "on (T.fileName = S.fileName) and (T.mTime = S.mTime) and (T.dataId = S.dataId) and (T.folderId = S.folderId) " +
          "when not matched then " +
          "insert (fileName, mTime, dataId, folderId) values(S.fileName, S.mTime, S.dataId, S.folderId);" +
          "SELECT fileId FROM files WHERE (fileName = @fileName AND mTime = @mTime AND dataId = @dataId AND folderId = @folderId);";
        cmd.Parameters.Add(GetParameter("fileName", name));
        cmd.Parameters.Add(GetParameter("mTime", mTimeTicks));
        cmd.Parameters.Add(GetParameter("dataId", dataId));
        cmd.Parameters.Add(GetParameter("folderId", relativeFolderId));
        using (IDataReader reader = cmd.ExecuteReader()) {
          if (reader.Read()) {
            result = reader.GetInt64(0);
          } else {
            throw new ApplicationException("Exception occured while inserting file " + name);
          }
        }
      }
      //}
      return result;
    }

    #region "Cache"
    public backup.FileRecord.Cache GetAllData(string server, string rootFolder) {
      FileRecord.Cache result = GetAllFileRecordsFor(server, rootFolder);
      foreach (var row in GetDataInfoNotFor(server, rootFolder)) {
        result.AddDataInfo(row.Value);
      }
      return result;
    }

    private Dictionary<string, backup.DataInfo> GetDataInfoNotFor(string server, string rootFolder) {
      Dictionary<string, backup.DataInfo> result = new Dictionary<string, backup.DataInfo>();
      LOGGER.Info("Loading files data...");
      //lock (_lock) {
      using (IDbCommand cmd = CreateCommand(3600)) {
        cmd.CommandText =
          "SELECT dataId, md5, [length], onLocal, onRemote, inProgressStart FROM data " +
          "LEFT OUTER JOIN files ON data.dataId = files.dataId INNER JOIN rootFolders ON files.folderId = rootFolders.folderId " +
          "WHERE (rootFolders.server <> @server OR rootFolders.folder <> @rootFolder)";
        using (IDataReader reader = cmd.ExecuteReader()) {
          while (reader.Read()) {

            long id = (long)reader.GetValue(0);
            string md5 = reader.GetString(1);
            long length = (long)reader.GetValue(2);
            bool onLocal = reader.GetBoolean(3);
            bool onRemote = reader.GetBoolean(4);
            DateTime? inProgressSince;
            if (reader.IsDBNull(5)) {
              inProgressSince = null;
            } else {
              inProgressSince = new DateTime(reader.GetValue<long>(5));
            }

            backup.DataInfo data = new backup.DataInfo(id, md5, length, onLocal, onRemote, inProgressSince);
            result.Add(data.MD5, data);
          }
        }
      }
      //}
      LOGGER.Info("Files data loaded (" + result.Count + " items)");
      return result;
    }

    private backup.FileRecord.Cache GetAllFileRecordsFor(string server, string rootFolder) {
      LOGGER.Info("Loading db cache...");
      backup.FileRecord.Cache result = new backup.FileRecord.Cache(rootFolder);
      //lock (_lock) {
      using (IDbCommand cmd = CreateCommand(3600)) {
        cmd.CommandText =
          "SELECT " +
            "files.fileId as id, " + //0
            "relativeFolders.folder as relativeFolder, " + //1
            "files.fileName as name, " + //2
            "files.mTime as mTime, " + //3
            "data.md5 as md5, " + //4
            "data.length as length, " + //5
            "data.dataId as dataId, " + //6
            "data.onLocal as onLocal, " + //7
            "data.onRemote as onRemote, " + //8
            "data.inProgressStart as inProgressStart " + //9

            "FROM files " +
            "INNER JOIN (SELECT fileName, MAX(mTime) AS maxMTime, folderId FROM files GROUP BY fileName, folderId) as mFiles ON mFiles.fileName = files.fileName AND mFiles.maxMTime = files.mTime AND mFiles.folderId = files.folderId " +
            "INNER JOIN relativeFolders ON files.folderId = relativeFolders.folderId " +
            "INNER JOIN rootFolders ON relativeFolders.rootFolderId = rootFolders.folderId " +
            "INNER JOIN data ON files.dataId = data.dataId " +
            "WHERE (rootFolders.server = @server AND rootFolders.folder = @rootFolder)";
        cmd.Parameters.Add(GetParameter("server", server));
        cmd.Parameters.Add(GetParameter("rootFolder", rootFolder));
        using (IDataReader reader = cmd.ExecuteReader()) {
          while (reader.Read()) {

            long dataId = reader.GetInt64(6);
            string md5 = reader.GetString(4);
            long length = reader.GetInt64(5);
            bool onLocal = reader.GetBoolean(7);
            bool onRemote = reader.GetBoolean(8);
            DateTime? inProgressSince;
            if (reader.IsDBNull(9)) {
              inProgressSince = null;
            } else {
              inProgressSince = new DateTime(reader.GetValue<long>(9));
            }
            backup.DataInfo data = new backup.DataInfo(dataId, md5, length, onLocal, onRemote, inProgressSince);

            result.AddFileRecord(
              reader.GetInt64(0),//id
              reader.GetString(1),//relativeFolder
              reader.GetString(2),//name
              new DateTime(reader.GetValue<long>(3)),//mTime
              data//data
            );
          }
        }
      }
      //}
      LOGGER.Info("Db cache loaded (" + result.FileRecordsCount + " items)");
      //LOGGER.Info("stringLessThanInt=" + FileRecord.Cache.stringLessThanInt + " intLessThanString=" + FileRecord.Cache.intLessThanString);
      return result;
    }
    #endregion

    public backup.DataInfo GetData(long id) {
      backup.DataInfo result;
      //lock (_lock) {
      using (IDbCommand cmd = CreateCommand(3600)) {
        cmd.CommandText = "SELECT md5, [length], onLocal, onRemote, inProgressStart FROM data WHERE dataId = @dataId";
        cmd.Parameters.Add(GetParameter("dataId", id));
        using (IDataReader reader = cmd.ExecuteReader()) {
          if (reader.Read()) {

            string md5 = reader.GetString(0);
            long length = (long)reader.GetValue(1);
            bool onLocal = reader.GetBoolean(2);
            bool onRemote = reader.GetBoolean(3);
            DateTime? inProgressSince;
            if (reader.IsDBNull(4)) {
              inProgressSince = null;
            } else {
              inProgressSince = new DateTime(reader.GetValue<long>(4));
            }

            result = new backup.DataInfo(id, md5, length, onLocal, onRemote, inProgressSince);
          } else {
            result = null;
          }
          if (reader.Read()) {
            throw new ApplicationException("More than 1 records with data id " + id + " in database");
          }
        }
      }
      //}
      return result;
    }

    public void SetDataReady(backup.DataInfo data) {
      //lock (_lock) {
      using (IDbCommand cmd = CreateCommand()) {
        cmd.CommandText = "UPDATE data SET inProgressStart = NULL WHERE (dataId = @dataId);";
        cmd.Parameters.Add(GetParameter("dataId", data.ID));
        cmd.ExecuteNonQuery();
      }
      //}
    }

    public void SetDataReadyOnLocal(backup.DataInfo data) {
      //lock (_lock) {
      using (IDbCommand cmd = CreateCommand()) {
        cmd.CommandText = "UPDATE data SET onLocal = 1 WHERE dataId = @dataId";
        cmd.Parameters.Add(GetParameter("dataId", data.ID));
        cmd.ExecuteNonQuery();

        //todo: should not be here. maybe transfere to DataInfo (pattern ActiveRecord)
        lock (data) {
          data.InLocalProgressByThisProcess = null;
          data.OnLocal = true;
        }
      }
      //}
    }

    public void SetDataReadyOnRemote(backup.DataInfo data) {
      //lock (_lock) {
      using (IDbCommand cmd = CreateCommand()) {
        cmd.CommandText = "UPDATE data SET onRemote = 1 WHERE dataId = @dataId";
        cmd.Parameters.Add(GetParameter("dataId", data.ID));
        cmd.ExecuteNonQuery();

        //todo: should not be here. maybe transfere to DataInfo (pattern ActiveRecord)
        lock (data) {
          data.InRemoteProgressByThisProcess = null;
          data.OnRemote = true;
        }
      }
      //}
    }

		#region "rootFolders"
		Dictionary<string, Dictionary<string, int>> _rootFoldersCache = new Dictionary<string, Dictionary<string, int>>();
    private int AddIfNewRootFolder(string server, string rootFolder) {
      int rootFolderId;
      //lock (_lock) {
      Dictionary<string, int> rootFolders;
      if (!_rootFoldersCache.TryGetValue(server, out rootFolders)) {
        rootFolders = GetAllRootFolders(server);
        _rootFoldersCache.Add(server, rootFolders);
      }
      if (!rootFolders.TryGetValue(rootFolder, out rootFolderId)) {
        using (IDbCommand cmd = CreateCommand()) {
          cmd.CommandText =
            "merge rootFolders WITH (HOLDLOCK) as T " +
              "using (select @folder as folder, @server as server) as S on (T.folder = S.folder) and (T.server = S.server) " +
              "when not matched then " +
              "insert (folder, server) values(S.folder, S.server);" +
              "SELECT folderId FROM rootFolders WHERE (server = @server AND folder = @folder);";
          cmd.Parameters.Add(GetParameter("server", server));
          cmd.Parameters.Add(GetParameter("folder", rootFolder));
          using (IDataReader reader = cmd.ExecuteReader()) {
            if (reader.Read()) {
              rootFolderId = reader.GetInt32(0);
            } else {
              throw new ApplicationException("Exception occured while inserting root folder " + rootFolder);
            }
          }
        }
        rootFolders.Add(rootFolder, rootFolderId);
      }
      //}
      return rootFolderId;
    }

    private Dictionary<string, int> GetAllRootFolders(string server) {
      Dictionary<string, int> result = new Dictionary<string, int>();
      //lock (_lock) {
      using (IDbCommand cmd = CreateCommand()) {
        cmd.CommandText = "SELECT folderId, folder FROM rootFolders WHERE (server = @server)";
        cmd.Parameters.Add(GetParameter("server", server));
        using (IDataReader reader = cmd.ExecuteReader()) {
          while (reader.Read()) {
            int id = reader.GetInt32(0);
            string folder = reader.GetString(1);
            result.Add(folder, id);
          }
        }
      }
      //}
      return result;
    }
		#endregion

		#region "relativeFolders"
		Dictionary<Tuple<int, string>, long> _relativeFoldersCache = new Dictionary<Tuple<int, string>, long>();
    private long AddIfNewRelativeFolder(int rootFolderId, string folder) {
      long result;
      //lock (_lock) {
      Tuple<int, string> key = new Tuple<int, string>(rootFolderId, folder);
      if (!_relativeFoldersCache.TryGetValue(key, out result)) {
        using (IDbCommand cmd = CreateCommand()) {
          cmd.CommandText =
            "merge relativeFolders WITH (HOLDLOCK) as T " +
              "using (select @folder as folder, @rootFolderId as rootFolderId) as S on (T.folder = S.folder) and (T.rootFolderId = S.rootFolderId) " +
              "when not matched then " +
              "insert (folder, rootFolderId) values(S.folder, S.rootFolderId);" +
              "SELECT folderId FROM relativeFolders WHERE (rootFolderId = @rootFolderId AND folder = @folder);";
          cmd.Parameters.Add(GetParameter("folder", folder));
          cmd.Parameters.Add(GetParameter("rootFolderId", rootFolderId));
          using (IDataReader reader = cmd.ExecuteReader()) {
            if (reader.Read()) {
              result = reader.GetInt64(0);
            } else {
              throw new ApplicationException("Exception occured while inserting relative folder " + folder);
            }
          }
        }
        _relativeFoldersCache.Add(key, result);
      }
      //}
      return result;
    }
		#endregion

    public int CreateNewScan(string server) {
      int result;
      //lock (_lock) {
      using (IDbCommand cmd = CreateCommand()) {
        cmd.CommandText = "INSERT INTO scans (startTime,server) VALUES (@startTime,@server); SELECT SCOPE_IDENTITY();";
        cmd.Parameters.Add(GetParameter("startTime", DateTime.Now));
        cmd.Parameters.Add(GetParameter("server", server));

        using (IDataReader reader = cmd.ExecuteReader()) {
          if (reader.Read() && !reader.IsDBNull(0)) {
            result = (int)(decimal)reader.GetValue(0);
          } else {
            throw new ApplicationException("Exception occured while creating new scan");
          }
        }
      }
      //}
      return result;
    }

    public void EndScan(int scanId) {
      //lock (_lock) {
      using (IDbCommand cmd = CreateCommand()) {
        cmd.CommandText = "UPDATE scans SET endTime = @endTime WHERE scanId = @scanId";
        cmd.Parameters.Add(GetParameter("endTime", DateTime.Now));
        cmd.Parameters.Add(GetParameter("scanId", scanId));
        cmd.ExecuteNonQuery();
      }
      //}
    }

		#endregion
	}
}
