using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace backup {
  public interface IRecoverer {
    long Recover(string md5, string targetPath);
  }
}
