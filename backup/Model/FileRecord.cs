using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace backup {
  public class FileRecord : IBackupItem {
    private Cache _parent;

    public DataInfo Data {
      get;
      private set;
    }

    public long ID {
      get;
      private set;
    }

    public string RootFolder {
      get {
        return _parent.RootFolder;
      }
    }

    public string RelativeFolder {
      get;
      private set;
    }

    public string Name {
      get;
      private set;
    }

    public string FullName {
      get {
        return RootFolder + RelativeFolder + Name;
      }
    }

    public string Md5 {
      get {
        return this.Data.MD5;
      }
    }

    public long Length {
      get {
        return this.Data.Length;
      }
    }

    public DateTime LastModifyTime {
      get;
      private set;
    }

    private FileRecord(Cache parent, long id, string relativeFolder, string name, DateTime lastModifyTime, DataInfo data) {
      _parent = parent;
      this.ID = id;
      this.RelativeFolder = relativeFolder;
      this.Name = name;
      this.LastModifyTime = lastModifyTime;
      this.Data = data;
    }

    public override bool Equals(object obj) {
      FileRecord o = obj as FileRecord;
      if (o == null) return false;
      return o.FullName == this.FullName && o.Length == this.Length && o.LastModifyTime == this.LastModifyTime;
    }

    public override int GetHashCode() {
      return this.FullName.GetHashCode() ^ this.Length.GetHashCode() ^ this.LastModifyTime.GetHashCode();
    }

    public string GetShortFileName() {
      if (System.Text.Encoding.UTF8.GetByteCount(this.Name) > 255) {
        return IOHelper.ShrinkTo255Bytes(this.Name);
      }
      return this.Name;
    }

    public string GetShortRelativeFolder() {
      string result = string.Empty;
      string separator = string.Empty;
      foreach (string folder in this.RelativeFolder.Split(System.IO.Path.DirectorySeparatorChar)) {
        if (System.Text.Encoding.UTF8.GetByteCount(folder) > 255) {
          result += separator + IOHelper.ShrinkTo255Bytes(folder);
        } else {
          result += separator + folder;
        }
        if (separator == string.Empty) {
          separator = System.IO.Path.DirectorySeparatorChar.ToString();
        }
      }
      return result;
    }

    public class Cache {
      private Dictionary<string, DataInfo> _cacheDI;
      private Dictionary<Cache.FileRecordFootPrint, CachedFileRecord> _cacheFR;
      private Dictionary<string, int> _cacheFolders;

      public string RootFolder {
        get;
        private set;
      }

      public int FileRecordsCount {
        get {
          return _cacheFR.Count;
        }
      }

      public Cache(string rootFolder) {
        RootFolder = rootFolder;
        _cacheDI = new Dictionary<string, DataInfo>();
        _cacheFR = new Dictionary<FileRecordFootPrint, CachedFileRecord>();
        _cacheFolders = new Dictionary<string, int>();
      }

      public FileRecord AddFileRecord(long fileId, string relativeFolder, string name, DateTime lastWriteTime, DataInfo dbData) {
        CachedFileRecord rec = new CachedFileRecord(fileId, dbData);
        int relativeFolderCode;
        if (!_cacheFolders.TryGetValue(relativeFolder, out relativeFolderCode)) {
          relativeFolderCode = _cacheFolders.Count;
          _cacheFolders.Add(relativeFolder, relativeFolderCode);
        }
        AddDataInfo(dbData);
        FileRecordFootPrint fp = new FileRecordFootPrint(name, relativeFolderCode, dbData.Length, lastWriteTime);
        _cacheFR.Add(fp, rec);
        return new FileRecord(this, fileId, relativeFolder, name, lastWriteTime, dbData);
      }

      public void AddDataInfo(DataInfo dbData) {
        if (!_cacheDI.ContainsKey(dbData.MD5)) {
          _cacheDI.Add(dbData.MD5, dbData);
        }
      }

      public bool TryGetFileRecord(FsInfo item, out FileRecord rec) {
        int relativeFolderCode;
        if (!_cacheFolders.TryGetValue(item.RelativeFolder, out relativeFolderCode)) {
          rec = null;
          return false;
        }
        FileRecordFootPrint fp = new FileRecordFootPrint(item.Name, relativeFolderCode, item.Length, item.LastWriteTime);
        CachedFileRecord cacheRec;
        if (_cacheFR.TryGetValue(fp, out cacheRec)) {
          rec = new FileRecord(this, cacheRec.ID, item.RelativeFolder, fp.Name, fp.LastWriteTime, cacheRec.Data);
          return true;
        } else {
          rec = null;
          return false;
        }
      }

      public bool TryGetDataInfo(string md5, out DataInfo data) {
        return _cacheDI.TryGetValue(md5, out data);
      }

      #region "FileRecordFootPrint"
      private struct FileRecordFootPrint {

        private string _name;
        public string Name {
          get {
            return _name;
          }
        }

        private int _relativeFolderCode;
        public int RelativeFolderCode {
          get {
            return _relativeFolderCode;
          }
        }

        private long _length;
        public long Length {
          get {
            return _length;
          }
        }

        private DateTime _lastWriteTime;
        public DateTime LastWriteTime {
          get {
            return _lastWriteTime;
          }
        }

        public FileRecordFootPrint(string name, int relativeFolderCode, long length, DateTime lastModifyTime) {
          this._name = name;
          this._relativeFolderCode = relativeFolderCode;
          this._length = length;
          this._lastWriteTime = lastModifyTime;
        }

        public override bool Equals(object obj) {
          if (obj is FileRecordFootPrint) {
            FileRecordFootPrint o = (FileRecordFootPrint)obj;
            return o.Name == this.Name && o.RelativeFolderCode == this.RelativeFolderCode && o.Length == this.Length && o.LastWriteTime == this.LastWriteTime;
          }
          return false;
        }

        public override int GetHashCode() {
          return this.Name.GetHashCode() ^ this.RelativeFolderCode.GetHashCode() ^ this.Length.GetHashCode() ^ this.LastWriteTime.GetHashCode();
        }
      }
      #endregion

      #region "CachedFileRecord"
      private class CachedFileRecord {

        public long ID {
          get;
          private set;
        }

        public DataInfo Data {
          get;
          private set;
        }

        public CachedFileRecord(long id, DataInfo data) {
          ID = id;
          Data = data;
        }
      }
      #endregion
    }
  }
}
