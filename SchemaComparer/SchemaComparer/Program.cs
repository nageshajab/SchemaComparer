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
            string[] menu = { "1. Generate Script", "2. Run Script", "3. Exit" };

            do
            {
                Console.WriteLine("Database pointed to: " + connstring);
                foreach (string str in menu) Console.WriteLine(str);
                Console.WriteLine("Enter your choice");
                string choice = Console.ReadLine();
                switch (choice)
                {
                    case "1": GenerateDbScript(); break;
                    case "2": ExecuteScript(); break;
                    case "3": System.Environment.Exit(0); break;
                    default: continue;
                }
            } while (1 == 1);
        }
        static void GenerateDbScript()
        {
            string printErrorsOnly = ConfigurationManager.AppSettings["printErrorsOnly"];
            List<string> tables = Table.GetTables();

            string dbScript = "" +
                $"declare @printErrorsOnly bit\nset @printErrorsOnly = {printErrorsOnly}\n";

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
