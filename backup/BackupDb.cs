using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlClient;
using System.Configuration;
using System.Data;

namespace backup {
  public class BackupDb {

    /// <summary>
    /// Backups the data base.
    /// </summary>
    /// <param name="fileName">Name of the file.</param>
    /// <returns></returns>
    public static bool BackupDataBase(string fileName) {
      try {
        using (SqlConnection con = new SqlConnection(MainClass.Settings.DbConnectionString)) {
          SqlCommand cmd = con.CreateCommand();
          cmd.CommandText = string.Format(@"BACKUP DATABASE [backup] TO  DISK = N'{0}' WITH  INIT ,  NOUNLOAD ,  NOSKIP ,  STATS = 10,  NOFORMAT", fileName);
          con.Open();
          cmd.ExecuteNonQuery();
        }
        return true;
      } catch (SqlException exc) {
        exc.WriteToLog("sql backup exception: ");
        return false;
      }
    }

  }
}
