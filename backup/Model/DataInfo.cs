using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace backup {
  public class DataInfo {

    public long ID {
      get;
      private set;
    }

    public string MD5 {
      get;
      private set;
    }

    public long Length {
      get;
      private set;
    }

    public bool OnLocal {
      get;
      set;
    }

    public bool OnRemote {
      get;
      set;
    }

    public DateTime? InProgressByOtherProcess {
      get;
      private set;
    }

    public DateTime? InRemoteProgressByThisProcess {
      get;
      set;
    }

    public DateTime? InLocalProgressByThisProcess {
      get;
      set;
    }

    public bool InProgress {
      get {
        return
          this.InRemoteProgressByThisProcess.HasValue
          || this.InLocalProgressByThisProcess.HasValue
          || (this.InProgressByOtherProcess.HasValue && !MainClass.IgnoreInProgressByOther && (DateTime.Now - this.InProgressByOtherProcess.Value).TotalHours < 24);
      }
    }

    public DataInfo(long id, string md5, long length, bool onLocal, bool onRemote, DateTime? inProgressByOtherProcess) {
      this.ID = id;
      this.MD5 = md5;
      this.Length = length;
      this.OnLocal = onLocal;
      this.OnRemote = onRemote;
      this.InProgressByOtherProcess = inProgressByOtherProcess;
    }

    public override bool Equals(object obj) {
      DataInfo objData = obj as DataInfo;
      if (objData == null) return false;
      else return this.ID == objData.ID && this.MD5 == objData.MD5 && this.Length == objData.Length;
    }

    public override int GetHashCode() {
      return this.ID.GetHashCode() ^ this.MD5.GetHashCode() ^ this.Length.GetHashCode();
    }

  }
}
