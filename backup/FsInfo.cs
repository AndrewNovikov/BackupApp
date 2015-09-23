using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace backup {
  public class FsInfo : FileSystemInfo {

    private string _name;
    private string _server;
    private string _rootFolder;
    private FileAttributes _attributes;

    public bool IsDirectory {
      get {
        return (_attributes & FileAttributes.Directory) == FileAttributes.Directory;
      }
    }

    public bool IsFile {
      get {
        return !IsDirectory;
      }
    }

    public override string Name {
      get {
        return this._name;
      }
    }

    public string Server {
      get {
        return _server;
      }
    }

    public string RootFolder {
      get {
        return _rootFolder;
      }
    }

    public string RelativeFolder {
      get {
        return this.GetDirectoryName().Remove(0, _rootFolder.Length);
      }
    }

    public long Length {
      get {
        return IsDirectory ? 0 : IOHelper.GetFileLength(FullPath);
      }
    }

    public override string FullName {
      get {
        return FullPath;
      }
    }

    public new DateTime LastWriteTime {
      get {
        return IOHelper.GetFileLastWriteTime(FullPath).Truncate(TimeSpan.FromMilliseconds(1));
      }
    }

    public override bool Exists {
      get {
        if ((_attributes & FileAttributes.Directory) == FileAttributes.Directory) {
          return IOHelper.DirectoryExists(base.FullPath);
        } else {
          return IOHelper.FileExists(base.FullPath);
        }
      }
    }

    public FsInfo(string server, string rootFolder, string fullPath) {
      _server = server;
      if (rootFolder == null) {
        throw new ArgumentNullException("rootFolder");
      }
      _rootFolder = rootFolder;
      if (fullPath == null) {
        throw new ArgumentNullException("fullPath");
      }
      base.OriginalPath = fullPath;
      _attributes = IOHelper.GetAttributes(fullPath);
      if (IsDirectory) {
        _name = string.Empty;
        base.FullPath = fullPath + Path.DirectorySeparatorChar;
      } else {
        int fileNameStart = fullPath.LastIndexOf(Path.DirectorySeparatorChar);
        fileNameStart = fileNameStart == -1 ? 0 : fileNameStart + 1;
        _name = fullPath.Substring(fileNameStart);
        base.FullPath = fullPath;
      }
    }

    public override void Delete() {
      if (IsDirectory) {
        IOHelper.DeleteDirectory(FullPath, false);
      } else {
        IOHelper.DeleteFile(FullPath);
      }
    }

    public string GetDirectoryName() {
      return FullPath.Substring(0, FullPath.Length - Name.Length);
    }

  }
}
