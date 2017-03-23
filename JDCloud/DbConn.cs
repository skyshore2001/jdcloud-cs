using System;
using System.Collections.Generic;
using System.Text;
using System.Data;

using System.Data.Common;
using System.Text.RegularExpressions;
//using System.Data.OleDb;
//using System.Data.SqlClient;
//using System.Data.Odbc;

/**
如果未指定providerName，则默认使用ODBC连接(System.Data.Odbc)

连接字符串示例：
http://www.cnblogs.com/liubo68/archive/2012/12/28/2836777.html

	<connectionStrings>
		<!--add name="default" connectionString="DRIVER=MySQL ODBC 5.3 Unicode Driver; PORT=3306; DATABASE=<mydb>; SERVER=<myserver>; UID=<uid>; PWD=<pwd>; CHARSET=UTF8;" /-->
		<!--add name="default" connectionString="DRIVER={SQL Server Native Client 10.0}; DATABASE=<mydb>; SERVER=<myserver>; Trusted_Connection=<Yes>; UID=<uid>; PWD=<pwd>;" /-->
	</connectionStrings>

- MySQL

ODBC方式

	connectionString="DRIVER=MySQL ODBC 5.3 Unicode Driver; PORT=3306; DATABASE=<mydb>; SERVER=<myserver>; UID=<uid>; PWD=<pwd>; CHARSET=UTF8;"

- SQL Server

ODBC方式

	connectionString="DRIVER={SQL Server Native Client 10.0}; DATABASE=<mydb>; SERVER=<myserver>; Trusted_Connection=<Yes/No>; UID=<uid>; PWD=<pwd>;" 

myserver示例：".", ".\MyInst", "192.168.3.3\MyInst"

SqlConnection (.NET)方式

	providerName="System.Data.SqlClient"
	connectionString="DATABASE=<mydb>; SERVER=<myserver>; Trusted_Connection=<Yes/No>; UID=<uid>; PWD=<pwd>;" 

 */
namespace JDCloud
{
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

	public interface IDbStrategy
	{
		void init(DbConn cnn);
		int getLastInsertId();

		// 处理LIMIT语句，转换成SQL服务器支持的语句
		string fixPaging(string sql);

		// 表名或字段名转义
		string quoteName(string s);

		// 在group-by, order-by中允许使用alias
		bool acceptAliasInOrderBy();
	}

	public class DbConn: IDbConnection
	{
		public DbTransaction Trans;
		public DbConnection DbConnection { get { return m_conn; } }
		public delegate void OnExecSql(string sql);
		public OnExecSql onExecSql;

		protected DbConnection m_conn;
		protected IDbStrategy m_dbStrategy;

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

		public void Dispose() { if (m_conn!=null) m_conn.Dispose(); } // call m_conn.Close();
#endregion

		public DbProviderFactory Provider { 
			get;
			protected set;
		}
		public string DbType
		{
			get;
			protected set;
		}
		public IDbStrategy DbStrategy
		{
			get { return m_dbStrategy; }
		}

		public DbConn() { }

		public void Open(string connStr, string providerName = null, string dbType = null)
		{
			if (providerName == null || providerName.Length == 0)
			{
				providerName = "System.Data.Odbc";
			}

			if (dbType == null)
			{
				if (providerName == "System.Data.SqlClient")
				{
					dbType = "mssql";
				}
				else if (providerName == "System.Data.Odbc")
				{
					var m = Regex.Match(connStr, @"DRIVER=(.*?);", RegexOptions.IgnoreCase);
					if (m.Success)
					{
						string driver = m.Groups[1].Value;
						if (driver.IndexOf("SQL Server", StringComparison.CurrentCultureIgnoreCase) >= 0)
						{
							dbType = "mssql";
						}
					}
				}
				if (dbType == null)
					dbType = "mysql";
			}
			this.DbType = dbType;

			Provider = DbProviderFactories.GetFactory(providerName);

			if (dbType == "mssql")
				m_dbStrategy = new MsSQLStrategy();
			else
				m_dbStrategy = new MySQLStrategy();
			m_dbStrategy.init(this);

			m_conn = Provider.CreateConnection();
			this.ConnectionString = connStr;
			this.Open();
		}

/**
使用ODBC方式连接指定类型数据库
*/
		public void OpenOdbc(string dbType, string dbname, string server=null, string dbuser=null, string dbpwd=null)
		{
			string connStr = dbname;
			string authStr = null;
			if (dbuser != null)
			{
				authStr = "UID=" + dbuser + ";PWD=" + dbpwd;
			}
			switch (dbType)
			{
				case "mssql":
					//m_conn = new SqlConnection(cnnstr);
					if (server == null)
						server = "127.0.0.1";
					if (authStr == null)
						authStr = "Trusted_Connection=Yes";
					connStr = string.Format("Driver={SQL Server};SERVER={0}; DATABASE={1};", server, dbname) + authStr;
					break;
				
				case "mysql":
					connStr = string.Format("DRIVER=MySQL ODBC 5.3 Unicode Driver; PORT=3306; SERVER={0}; DATABASE={1}; CHARSET=UTF8;", server, dbname);
					if (authStr != null)
						connStr += authStr;
					break;

				case "access":
					//m_conn = new OleDbConnection(cnnstr);
					// TODO: use ODBC
					connStr = "Provider=Microsoft.Jet.OleDb.4.0;Data Source=" + dbname;
					break;
				
				default:
					throw new Exception("Unsupported Dbtype");
			}
			this.Open(connStr, null, dbType);
		}

		public DbDataReader ExecQuery(string sql)
		{
			sql = getSqlForExec(sql);
			DbCommand cmd = m_conn.CreateCommand();
			cmd.Transaction = Trans;
			cmd.CommandText = sql;
			return cmd.ExecuteReader();
		}
		public int ExecNonQuery(string sql)
		{
			sql = getSqlForExec(sql);
			DbCommand cmd = m_conn.CreateCommand();
			cmd.Transaction = Trans;
			cmd.CommandText = sql;
			return cmd.ExecuteNonQuery();
		}
		public object ExecScalar(string sql)
		{
			sql = getSqlForExec(sql);
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

		public int getLastInsertId()
		{
			return m_dbStrategy.getLastInsertId();
		}
		public string fixPaging(string sql)
		{
			return m_dbStrategy.fixPaging(sql);
		}

		protected string getSqlForExec(string sql)
		{
			sql = fixTableName(sql);
			if (this.onExecSql != null)
				onExecSql(sql);
			return sql;
		}
		private string fixTableName(string sql)
		{
			string q = m_dbStrategy.quoteName("$1");
			return Regex.Replace(sql, @"(?<= (?:UPDATE | FROM | JOIN | INTO) \s+ )([\w|.]+)", q, RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace | RegexOptions.Singleline);
		}
	}

	class MySQLStrategy : IDbStrategy
	{
		DbConn m_cnn;

		public void init(DbConn cnn)
		{
			m_cnn = cnn;
		}

		public int getLastInsertId()
		{
			object ret = m_cnn.ExecScalar("SELECT LAST_INSERT_ID()");
			return Convert.ToInt32(ret);
		}

		public string quoteName(string s)
		{
			return "`" + s + "`";
		}

		public string fixPaging(string sql)
		{
			return sql;
		}

		public bool acceptAliasInOrderBy()
		{
			return true;
		}
	}

	class MsSQLStrategy : IDbStrategy
	{
		DbConn m_cnn;

		public void init(DbConn cnn)
		{
			m_cnn = cnn;
		}

		public int getLastInsertId()
		{
			// or use "SELECT @@IDENTITY"
			object ret = m_cnn.ExecScalar("SELECT SCOPE_IDENTITY()");
			return Convert.ToInt32(ret);
		}

		public string quoteName(string s)
		{
			return "[" + s + "]";
		}

		public string fixPaging(string sql)
		{
			// for MSSQL: LIMIT -> TOP+ROW_NUMBER
			return Regex.Replace(sql, @"SELECT(.*?) (?: 
	LIMIT\s+(\d+)
	| (ORDER\s+BY.*?)\s*LIMIT\s+(\d+),(\d+)  
)\s*$" 
				, m => {
					if (m.Groups[2].Length > 0)
					{
						return "SELECT TOP " + m.Groups[2].Value + " " + m.Groups[1].Value;
					}
					int n1 = int.Parse(m.Groups[4].Value)+1;
					int n2 = n1+int.Parse(m.Groups[5].Value)-1;
					return string.Format("SELECT * FROM (SELECT ROW_NUMBER() OVER({0}) _row, {1}) t0 WHERE _row BETWEEN {2} AND {3}",
						m.Groups[3].Value, m.Groups[1].Value, n1, n2);
			}, RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.IgnorePatternWhitespace);
		}

		public bool acceptAliasInOrderBy()
		{
			return false;
		}
	}
}
