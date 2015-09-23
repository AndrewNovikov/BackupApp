using NLog;
using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;

namespace backup {
  public class LocalBackup : IBackupEngine {
    private static readonly Logger LOGGER = LogManager.GetCurrentClassLogger();
    private static readonly Logger PROFILER_LOGGER = LogManager.GetLogger("profiler");
    LinkerService.LinkerService backupService;
    //static readonly object lockObj = new object();
    private DateTime _localTime;
    private string _rootPath;
    private bool _singleTarget;

    public string BackupPath {
      get {
        string result = _rootPath.TrimEnd(Path.DirectorySeparatorChar);
        if (!_singleTarget) {
          result += Path.DirectorySeparatorChar + _localTime.ToString("yyyy-MM-dd");
        }
        return result;
      }
    }

    public LocalBackup(string rootPath, bool singleTarget) {
      _rootPath = rootPath;
      _singleTarget = singleTarget;
      _localTime = DateTime.Now;
      if (MainClass.Settings.LocalBackup.ThreadsCount > 0) {
        System.Runtime.Remoting.Channels.Tcp.TcpChannel channel = new System.Runtime.Remoting.Channels.Tcp.TcpChannel(0);
        System.Runtime.Remoting.Channels.ChannelServices.RegisterChannel(channel, false);
        backupService = (LinkerService.LinkerService)Activator.GetObject(typeof(LinkerService.LinkerService), "tcp://" + MainClass.Settings.LocalBackup.LinkerService + "/LinkerService");
      }
    }

    public long Backup(IBackupItem file) {
      if (string.IsNullOrEmpty(_rootPath)) return 0;

      //file.Data.InLocalProgressByThisProcess = DateTime.Now;
      long totalWrite = 0;

      //long existFileLength = backupService.GetFileLength(file.Md5);
      //if (existFileLength == -1) {

      //lock (lockObj) {
      if (file.Length > 0) {
        //using (MD5 md5Hasher = MD5.Create()) {
        //  using (System.IO.Stream source = IOHelper.OpenFile(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {

        //    //byte[] buffer = new byte[1048576];
        //    byte[] buffer = new byte[81920];
        //    int bytesRead;
        //    while ((bytesRead = source.Read(buffer, 0, buffer.Length)) != 0) {
        //      if (source.Position < source.Length) {
        //        md5Hasher.TransformBlock(buffer, 0, bytesRead, null, 0);
        //      } else {
        //        md5Hasher.TransformFinalBlock(buffer, 0, bytesRead);
        //      }

        //      if (bytesRead != buffer.Length) {
        //        Array.Resize(ref buffer, bytesRead);
        //      }
        //      backupService.WriteFile(file.Md5, buffer, totalWrite != 0);
        //      totalWrite += bytesRead;
        //    }

        //    System.Text.StringBuilder sb = new System.Text.StringBuilder();
        //    foreach (Byte b in md5Hasher.Hash)
        //      sb.Append(b.ToString("x2").ToLower());

        //    if (sb.ToString() != file.Md5) {
        //      throw new ApplicationException("While file was backing up it was changed. Md5 mismatch.");
        //    }

        //  }
        //}
        Stopwatch sw = Stopwatch.StartNew();
        using (System.IO.Stream source = IOHelper.OpenFile(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read)) {

          byte[] buffer = new byte[81920];
          int bytesRead;
          while ((bytesRead = source.Read(buffer, 0, buffer.Length)) != 0) {

            if (bytesRead != buffer.Length) {
              Array.Resize(ref buffer, bytesRead);
            }
            backupService.WriteFile(file.Md5, buffer, totalWrite != 0);
            totalWrite += bytesRead;
          }

        }
        LOGGER.Trace(file.Md5 + " data is backed up to local");
        sw.Stop();
        PROFILER_LOGGER.Info("LocalBackup took: {0}ms for file '{1}'", sw.ElapsedMilliseconds, file.FullName);
      } else {
        backupService.WriteFile(file.Md5, new byte[0], false);
        LOGGER.Trace(file.Md5 + " empty data is written to local");
      }

      //} else {
      //  if (existFileLength != file.Length) {
      //    throw new ApplicationException("Md5 " + file.Md5 + " already exist on local backup and it has " + existFileLength + " bytes length instead of " + file.Length + " bytes. Local file name is " + file.FullName);
      //  } else {
      //    LOGGER.Trace(file.Md5 + " data was already on local");
      //  }
      //}

      Link(file);
      //file.Data.InLocalProgressByThisProcess = null;
      //file.Data.OnLocal = true;

      return totalWrite;
      //}
    }

    public void Link(IBackupItem file) {
      if (string.IsNullOrEmpty(_rootPath)) return;
      Stopwatch sw = Stopwatch.StartNew();

      //lock (lockObj) {
      int rootFolderLetterSeparatorIndex = file.RootFolder.IndexOf(':');
      string rootFolder;
      if (rootFolderLetterSeparatorIndex != -1) {
        rootFolder = "Disk" + file.RootFolder.Substring(0, rootFolderLetterSeparatorIndex) + file.RootFolder.Substring(rootFolderLetterSeparatorIndex + 1);
      } else {
        rootFolder = file.RootFolder;
      }
      if (rootFolder.StartsWith(@"\\")) {
        rootFolder = rootFolder.Substring(2, rootFolder.Length - 2);
      }
      if (rootFolder == string.Empty) {
        rootFolder = "root";
      } else {
        rootFolder = rootFolder.Trim(Path.DirectorySeparatorChar).Replace(Path.DirectorySeparatorChar, '_');
      }
      string target = BackupPath + Path.DirectorySeparatorChar +
      rootFolder + Path.DirectorySeparatorChar +
      file.GetShortRelativeFolder().Trim(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar +
      file.GetShortFileName();

      try {
        backupService.Link(file.Md5, target);
        LOGGER.Trace(file.Md5 + " data is linked to " + target);
      } catch (Exception exc) {
        exc.WriteToLog("Error on linking file " + file.Md5 + " to " + target + ": ");
      }
      sw.Stop();
      PROFILER_LOGGER.Info("Link took: {0}ms for file '{1}'", sw.ElapsedMilliseconds, file.FullName);
      //}
    }

    

  }
}
