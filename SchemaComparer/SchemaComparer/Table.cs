using NetCore5.DatabaseLayer;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SchemaComparer
{
    public class Table
    {
        static readonly string connstring = ConfigurationManager.ConnectionStrings["MasterDb_LOCAL"].ConnectionString;
        private static DataSet DataSet { get; set; }
        private static int TABLE_COLUMNS = 1;
        private static int TABLE_IDENTITY = 2;
        private static int TABLE_INDEX = 5;
        private static int TABLE_CONSTRAINT = 6;
        public static string TABLE { get; set; }
        private static string primaryConstraintName;

        public static List<string> GetTables()
        {
            SqlServer sqlServer = new(connstring);

            List<string> lstTables = new();

            string commandText = "SELECT name FROM sys.objects WHERE type in (N'U') order by name";
            DataSet = sqlServer.GetDataset(commandText);

            foreach (DataRow dr in DataSet.Tables[0].Rows)
            {
                lstTables.Add(dr[0].ToString());
            }
            return lstTables;
        }

        public static string ColumnScript()
        {
            string sqlCreateColumn = File.ReadAllText(".\\Resources\\CreateColumn.txt");
            string columns = "";
            int i = 0;
            foreach (DataRow dr in DataSet.Tables[TABLE_COLUMNS].Rows)
            {
                string columnName = dr[0].ToString();
                string datatype = dr[1].ToString();
                string nullable = dr[6].ToString() == "no" ? "Not NULL" : "NULL";
                string length = dr[3].ToString() == "-1" ? "max" : dr[3].ToString();
                if (datatype == "uniqueidentifier" || datatype == "bit")
                {
                    length = "";
                }
                else if (DataSet.Tables[TABLE_IDENTITY].Rows[0]["Identity"].ToString() == columnName
                    && DataSet.Tables[TABLE_IDENTITY].Rows[0]["Identity"].ToString().Trim() != "No identity column defined."
                    && DataSet.Tables[TABLE_IDENTITY].Rows[0]["Seed"].ToString() != "")
                {
                    length = $"IDENTITY({DataSet.Tables[TABLE_IDENTITY].Rows[0]["Seed"]},{DataSet.Tables[TABLE_IDENTITY].Rows[0]["Increment"]})";
                }
                else if (datatype.ToLower() == "int")
                {
                    length = "";
                }
                else
                    length = $"({length})";

                string appendLine;
                if (i != 0)
                    appendLine = "\n";
                else
                    appendLine = "";
                columns += appendLine + string.Format(sqlCreateColumn, columnName, datatype, length, nullable);
                i++;
            }

            return columns.Substring(0, columns.LastIndexOf(","));
        }

        public static string IndexScript()
        {
            string column = "";
            string AllIndexes = "";
            string createIndexScript = File.ReadAllText(".\\Resources\\CreateIndex.txt");

            foreach (DataRow dr in DataSet.Tables[TABLE_INDEX].Rows)
            {
                if (dr[1].ToString().StartsWith("nonclustered"))
                {
                    column += $"{ dr[2]} ASC";
                    string indexScript = string.Format(createIndexScript, dr[0], TABLE, column);
                    AllIndexes += indexScript + "\n";
                }
            }

            return AllIndexes;
        }

        public static string PrimaryKeyClustered()
        {
            string constraints = "";
            string[] columns;

            foreach (DataRow dr in DataSet.Tables[TABLE_CONSTRAINT].Rows)
            {
                if (dr[0].ToString() == "PRIMARY KEY (clustered)")
                {
                    primaryConstraintName = dr[1].ToString();
                    columns = dr[6].ToString().Split(",", StringSplitOptions.None);
                    foreach (string str in columns)
                    {
                        constraints += str + " ASC,\n";
                    }
                    break;
                }
            }

            return constraints.IndexOf(",") > 0 ? constraints.Substring(0, constraints.LastIndexOf(",")) : "";
        }

        public static string TableScript(string table)
        {
            TABLE = table;
            string commandText = $"sp_help {table}";
            string connstring = ConfigurationManager.ConnectionStrings["MasterDb_LOCAL"].ConnectionString;
            SqlServer sqlServer = new(connstring);
            DataSet = sqlServer.GetDataset(commandText);
            string sqlCreateTable = File.ReadAllText(".\\Resources\\CreateTable.txt");

            string columns = ColumnScript();
            string primaryConstraint = PrimaryKeyClustered();
            string indexes = IndexScript();

            sqlCreateTable = string.Format(sqlCreateTable, table, columns, primaryConstraintName, primaryConstraint, indexes);

            return sqlCreateTable + "\n\n\n";
        }
    }
}
