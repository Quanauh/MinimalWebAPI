using Oracle.ManagedDataAccess.Client;
using Dapper;
class Database
{
    private static string conStr =
        "User Id=HUANBANK;" +
        "Password=huan123;" +
        "Data Source=localhost:1521/orclpdb";

    public static OracleConnection GetConnection()
    {
        OracleConnection conn = new OracleConnection(conStr);
        conn.Open();
        return conn;
    }
}          