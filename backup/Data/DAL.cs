using NLog;
using System;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
//using MySql.Data.MySqlClient;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using DynamicProxy;

namespace backup {
	public class DAL {

		DAL() {
		}

		public static IDAL Instance {
			get {
        return (IDAL)SynchronizerProxy.NewInstance(MSSQLDAL.Instance);
			}
		}

	}
}

