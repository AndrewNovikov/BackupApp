using System;
using System.Collections.Generic;

namespace backup {
  public interface IDAL {
    //todo: Depend upon Abstractions. Do not depend upon concretions

    void AddFileRecScan(long fileRecId, int scanId);
    DataInfo AddIfNewData(string md5, long length);
    long AddIfNewFileRec(string name, long mTimeTicks, long dataId, string relativeFolder, string rootFolder, string server);

    int CreateNewScan(string server);
    void EndScan(int scanId);

    FileRecord.Cache GetAllData(string server, string rootFolder);
    DataInfo GetData(long id);
    ////IEnumerable<RecoverInfo> GetAllPathsStartedWith(int scanId, string pathStart);


    void SetDataReady(DataInfo data);
    void SetDataReadyOnLocal(DataInfo data);
    void SetDataReadyOnRemote(DataInfo data);
  }
}
