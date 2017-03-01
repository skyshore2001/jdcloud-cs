using System;
using System.Collections.Generic;
using System.Text;
using System.Data;

using System.Data.Common;
//using System.Data.OleDb;
//using System.Data.SqlClient;
//using System.Data.Odbc;

namespace SSTk
{
    public enum DbConnType
    {
        MsAccess, Mssql, Odbc, OleDb
    }

	public class SSDataTable : DataTable
	{
		public DbDataAdapter DataAdapter;
		public DataRow AddRow()
		{
			DataRow row = this.NewRow();
			this.Rows.Add(row);
			return row;
		}
		public int Update()
		{
			return DataAdapter.Update(this);
		}
	}

    public class DbConn: IDbConnection
    {
        private DbConnection m_conn;
		public DbTransaction Trans;
        public DbConnection DbConnection { get { return m_conn; } }

#region implement IDbConnection
        public string ConnectionString {
            get { return m_conn.ConnectionString; }
            set { m_conn.ConnectionString = value; }
        }
        public int ConnectionTimeout { get {return m_conn.ConnectionTimeout;} }
        public string Database { get { return m_conn.Database; } }
        public ConnectionState State { get { return m_conn.State; } }
		public IDbTransaction BeginTransaction() { Trans = m_conn.BeginTransaction(); return Trans; }
		public IDbTransaction BeginTransaction(IsolationLevel il) { Trans = m_conn.BeginTransaction(il); return Trans; }
        public void ChangeDatabase(string databaseName) { m_conn.ChangeDatabase(databaseName); }
        public void Close() { m_conn.Close(); }
        public IDbCommand CreateCommand() { return m_conn.CreateCommand(); }
        public void Open() { m_conn.Open(); }

        public void Dispose() { m_conn.Dispose(); } // call m_conn.Close();
#endregion

        private DbProviderFactory m_provider;
        public DbProviderFactory Provider { 
            get { return m_provider; }
            protected set { m_provider = value; }
        }

		public DbConn() { }

        public DbConn(DbConnType type, string dbname)
        {
            Open(type, dbname, "", "");
        }

        public void Open(DbConnType type, string dbname, string dbuser, string dbpwd)
        {
            string cnnstr = dbname;
			string authstr = "";
			if (dbuser != null && dbuser.Length > 0 && dbpwd != null)
			{
				if (dbuser == "@")
				{
					authstr = ";Trusted_Connection=Yes";
				}
				else
				{
					authstr = ";UID=" + dbuser + ";PWD=" + dbpwd;
				}
			}
            switch (type)
            {
                case DbConnType.MsAccess:
                case DbConnType.OleDb:
                    //m_conn = new OleDbConnection(cnnstr);
                    this.Provider = DbProviderFactories.GetFactory("System.Data.OleDb");
                    cnnstr = "Provider=Microsoft.Jet.OleDb.4.0;Data Source=" + dbname;
                    break;
                
                case DbConnType.Mssql:
                    //m_conn = new SqlConnection(cnnstr);
                    this.Provider = DbProviderFactories.GetFactory("System.Data.SqlClient");
					cnnstr = "Driver={SQL Server};SERVER=127.0.0.1;DATABASE=" + dbname + authstr;
                    break;
                
                case DbConnType.Odbc:
                    this.Provider = DbProviderFactories.GetFactory("System.Data.Odbc");
                    if (dbname.EndsWith(".dsn", StringComparison.CurrentCultureIgnoreCase))
                        cnnstr = "filedsn=" + dbname + authstr;
                    else
						cnnstr = "dsn=" + dbname + authstr;
                    break;

                default:
                    throw new Exception("Unsupported Dbtype");
            }
            m_conn = Provider.CreateConnection();
            this.ConnectionString = cnnstr;
            this.Open();
        }

        public DbDataReader ExecQuery(string sql)
        {
            DbCommand cmd = m_conn.CreateCommand();
			cmd.Transaction = Trans;
            cmd.CommandText = sql;
            return cmd.ExecuteReader();
        }
        public int ExecNonQuery(string sql)
        {
            DbCommand cmd = m_conn.CreateCommand();
			cmd.Transaction = Trans;
            cmd.CommandText = sql;
            return cmd.ExecuteNonQuery();
        }
        public object ExecScalar(string sql)
        {
            DbCommand cmd = m_conn.CreateCommand();
			cmd.Transaction = Trans;
            cmd.CommandText = sql;
            return cmd.ExecuteScalar();
        }

        public DbDataAdapter CreateDataAdapter(string sql)
        {
            DbDataAdapter da = Provider.CreateDataAdapter();
            DbCommand cmd = Provider.CreateCommand();
            cmd.CommandText = sql;
            cmd.Connection = m_conn;
            da.SelectCommand = cmd;
            return da;
        }

        public SSDataTable ExecQueryForWrite(string sql)
        {
            // OleDbDataAdapter da = new OleDbDataAdapter(sql, conn);
            DbDataAdapter da = this.CreateDataAdapter(sql);
            DbCommandBuilder cb = Provider.CreateCommandBuilder();
            cb.DataAdapter = da;
            // 字段名加括号，否则如果与DB关键字冲突，则执行时会出错！
            //cb.QuotePrefix = "[";
            //cb.QuoteSuffix = "]";

            SSDataTable tbl = new SSDataTable();
            tbl.DataAdapter = da;
            da.Fill(tbl);
            return tbl;
        }

		public void Commit()
		{
			if (Trans != null)
			{
				Trans.Commit();
				Trans.Dispose();
				Trans = null;
			}
		}

		public void Rollback()
		{
			if (Trans != null)
			{
				Trans.Rollback();
				Trans.Dispose();
				Trans = null;
			}
		}
    }
}
