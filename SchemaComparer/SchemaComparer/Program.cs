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
        static void Main(string[] args)
        {
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
    }
}
