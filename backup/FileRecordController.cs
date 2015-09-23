using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace backup {
	public class FileRecordController {
		private static readonly Logger NEW_FILES_LOGGER = LogManager.GetLogger("newfiles");
    private static readonly Logger PROFILER_LOGGER = LogManager.GetLogger("profiler");
    private FileRecord.Cache _filesCache;

		public FileRecordController(string server, string rootFolder) {
			_filesCache = DAL.Instance.GetAllData(server, rootFolder);
		}

		public FileRecord GetAndUpsertDbFile(FsInfo item) {
      Stopwatch sw = Stopwatch.StartNew();
			FileRecord dbRec;
			DataInfo dbData;
			if (!_filesCache.TryGetFileRecord(item, out dbRec)) {
				NEW_FILES_LOGGER.Debug("Scanned " + item.FullName + ". File is new. (FullName=" + item.FullName + " & Length=" + item.Length + " & LastWriteTime=" + item.LastWriteTime.ToString("dd/MM/yyyy HH:mm:ss.fff") + ")");
				
				string md5;
				try {
          sw.Stop();
					md5 = item.Length == 0 ? Crypto.EmptyMd5Str : Crypto.GetMd5(item.FullName);
          sw.Start();
				} catch (Exception exc) {
					if (exc is System.IO.IOException || exc is UnauthorizedAccessException) {
						exc.WriteToLog("Error on file " + item.FullName + ":");
						return null;
					}
					throw;
				}

        if (!_filesCache.TryGetDataInfo(md5, out dbData)) {
					NEW_FILES_LOGGER.Debug("Scanned " + item.FullName + ". Data is new. (md5=" + md5 + ")");
					dbData = DAL.Instance.AddIfNewData(md5, item.Length);
          _filesCache.AddDataInfo(dbData);
				}

				long fileId = DAL.Instance.AddIfNewFileRec(item.Name, item.LastWriteTime.Ticks, dbData.ID, item.RelativeFolder, item.RootFolder, item.Server);
				dbRec = _filesCache.AddFileRecord(fileId, item.RelativeFolder, item.Name, item.LastWriteTime, dbData);
			}
      sw.Stop();
      PROFILER_LOGGER.Info("GetAndUpsertDbFile took: {0}ms for file '{1}'", sw.ElapsedMilliseconds, item.FullName);
			return dbRec;
		}

	}

}

