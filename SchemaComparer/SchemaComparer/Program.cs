using NetCore5.DatabaseLayer;
using NLog.Web;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;

namespace SchemaComparer
{
    class Program
    {
        static readonly NLog.Logger logger = NLogBuilder.ConfigureNLog("nlog.config").GetCurrentClassLogger();
        static readonly string connstring = ConfigurationManager.ConnectionStrings["MasterDb_LOCAL"].ConnectionString;

        static void Main(string[] args)
        {
            //if u want to execute generated script, uncomment below line
            //ExecuteScript();

            List<string> tables = Table.GetTables();

            string dbScript = "";

            foreach (string tbl in tables)
            {
                dbScript += Table.TableScript(tbl);
            }

            dbScript += Table.CreateColumnsScript.ToString();
            dbScript += Table.foreignKeyConstraints;

            File.WriteAllText("dbscript.sql", dbScript);
        }

        static void ExecuteScript()
        {        
            IDatabase sqlserver = new SqlServer(connstring, logger);
            string script = File.ReadAllText(".\\dbscript.sql");
            sqlserver.ExecuteNonQuery(script);
            System.Environment.Exit(0);
        }
    }
}
