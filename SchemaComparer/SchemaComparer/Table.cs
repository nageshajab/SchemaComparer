using NetCore5.DatabaseLayer;
using NLog.Web;
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
        #region Variables
        static readonly NLog.Logger logger = NLogBuilder.ConfigureNLog("nlog.config").GetCurrentClassLogger();
        public static string foreignKeyConstraints;
        static readonly string connstring = ConfigurationManager.ConnectionStrings["MasterDb_LOCAL"].ConnectionString;
        private static DataSet DataSet { get; set; }
        private static int TABLE_COLUMNS = 1;
        private static int TABLE_IDENTITY = 2;
        private static int TABLE_INDEX = 5;
        private static int TABLE_CONSTRAINT = 6;
        public static string TABLE { get; set; }
        private static string primaryConstraintName;

        public static StringBuilder CreateColumnsScript = new StringBuilder();
        #endregion

        public static List<string> GetTables()
        {
            SqlServer sqlServer = new(connstring, logger);

            List<string> lstTables = new();

            string commandText = "SELECT name FROM sys.objects WHERE type in (N'U') order by name";
            DataSet = sqlServer.GetDataset(commandText);

            foreach (DataRow dr in DataSet.Tables[0].Rows)
            {
                lstTables.Add(dr[0].ToString());
            }
            return lstTables;
        }

        public static void ColumnIdentityScript(string column, string columnName, bool isIdentity)
        {
            if (isIdentity)
            {
                string sqlAlterColumn = File.ReadAllText(".\\Resources\\AlterIdentityColumn.txt");
                CreateColumnsScript.Append(
                    "\n" + string.Format(sqlAlterColumn, TABLE, columnName));
            }
            else
            {
                string sqlAlterColumn = File.ReadAllText(".\\Resources\\AlterColumn.txt");
                CreateColumnsScript.Append(
                    "\n" + string.Format(sqlAlterColumn,
                    column.Substring(0, column.LastIndexOf(",")),
                    TABLE,
                    columnName));
            }
        }

        public static string ColumnScript()
        {
            bool isIdentityColumn = false;
            string sqlCreateColumn = File.ReadAllText(".\\Resources\\CreateColumn.txt");

            string columns = "";
            int i = 0;
            foreach (DataRow dr in DataSet.Tables[TABLE_COLUMNS].Rows)
            {
                string columnName = dr[0].ToString();
                string datatype = dr[1].ToString();
                string nullable = dr[6].ToString() == "no" ? "Not NULL" : "NULL";
                string length = dr[3].ToString() == "-1" ? "max" : dr[3].ToString();
                int length1 = 0;
                if (int.TryParse(length, out length1))
                {
                    length1 = int.Parse(length);
                    if (datatype == "nvarchar")
                        length1 /= 2;
                    length = length1.ToString();
                }

                if (datatype == "uniqueidentifier" || datatype == "bit")
                {
                    length = "";
                }
                else if (datatype.ToLower() == "datetime")
                {
                    length = "";
                }
                else if (DataSet.Tables[TABLE_IDENTITY].Rows[0]["Identity"].ToString() == columnName
                    && DataSet.Tables[TABLE_IDENTITY].Rows[0]["Identity"].ToString().Trim() != "No identity column defined."
                    && DataSet.Tables[TABLE_IDENTITY].Rows[0]["Seed"].ToString() != "")
                {
                    isIdentityColumn = true;
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
                string column = string.Format(sqlCreateColumn, columnName, datatype, length, nullable);
                columns += appendLine + column;
                i++;

                ColumnIdentityScript(column, columnName, isIdentityColumn);
            }

            columns = columns.Substring(0, columns.LastIndexOf(","));
            return columns;
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
                    column = $"{ dr[2]} ASC";
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
            string script = "";

            foreach (DataRow dr in DataSet.Tables[TABLE_CONSTRAINT].Rows)
            {
                if (dr[0].ToString() == "PRIMARY KEY (clustered)")
                {
                    string primaryKeyScript = File.ReadAllText(".\\Resources\\PrimaryKeyClustered.txt");
                    primaryConstraintName = dr[1].ToString();
                    columns = dr[6].ToString().Split(",", StringSplitOptions.None);
                    foreach (string str in columns)
                    {
                        constraints += str + " ASC,\n";
                    }
                    constraints = constraints.IndexOf(",") > 0 ? constraints.Substring(0, constraints.LastIndexOf(",")) : "";
                    script = string.Format(primaryKeyScript, primaryConstraintName, constraints);
                    break;
                }
            }

            return script;
        }

        public static string TableScript(string table)
        {
            TABLE = table;
            string commandText = $"sp_help {table}";
            string connstring = ConfigurationManager.ConnectionStrings["MasterDb_LOCAL"].ConnectionString;
            SqlServer sqlServer = new(connstring, logger);
            DataSet = sqlServer.GetDataset(commandText);
            string sqlCreateTable = File.ReadAllText(".\\Resources\\CreateTable.txt");

            string columns = ColumnScript();
            string primaryConstraint = DataSet.Tables.Count > TABLE_CONSTRAINT ? PrimaryKeyClustered() : "";
            string indexes = DataSet.Tables.Count > TABLE_INDEX ? IndexScript() : "";
            foreignKeyConstraints += DataSet.Tables.Count > TABLE_CONSTRAINT ? ForeignKeyConstraintScript() : "";

            sqlCreateTable = string.Format(sqlCreateTable, table, columns, primaryConstraint, indexes);

            return sqlCreateTable + "\n\n";
        }

        public static string ForeignKeyConstraintScript()
        {
            string constraints = "";
            int rowNo = 0;

            foreach (DataRow dr in DataSet.Tables[TABLE_CONSTRAINT].Rows)
            {
                string sqlCreateForeignConstraint = File.ReadAllText(".\\Resources\\CreateForeignConstraint.txt");

                if (dr[0].ToString() == "FOREIGN KEY")
                {
                    string ConstraintName = dr[1].ToString().Replace(".", "_");
                    string constraintKey = dr[6].ToString();
                    string delete_action = dr[2].ToString();
                    string update_action = dr[3].ToString();
                    string constraintStatus = dr[4].ToString();
                    string references = DataSet.Tables[TABLE_CONSTRAINT].Rows[rowNo + 1][6].ToString();
                    string reference = references.GetLastDelimitedString(".");
                    string reference1 = reference.Split('(')[0].Trim();

                    constraints += "\n" + string.Format(sqlCreateForeignConstraint, TABLE, ConstraintName, constraintKey, reference, delete_action, update_action, reference1);
                }
                rowNo += 1;
            }

            return constraints;
        }
    }
}
