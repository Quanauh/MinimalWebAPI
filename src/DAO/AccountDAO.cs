using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;
using Oracle.ManagedDataAccess.Client;

class AccountDAO : IAccountDAO
{
    // Kiem tra ton tai
    public bool Exists(string stk)
    {
        using OracleConnection conn = Database.GetConnection();
        int dem = conn.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM ACCOUNTS WHERE ACCOUNT_NUMBER=:stk", new { stk });
        return dem > 0;
    }

    public bool checkPassWord(string stk, string mk)
    {
        using OracleConnection conn = Database.GetConnection();
        int dem = conn.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM ACCOUNTS WHERE ACCOUNT_NUMBER=:stk AND PASSWORD=:mk", new { stk, mk });
        return dem > 0;
    }

    private int GetNextId(OracleConnection conn, OracleTransaction tran, string tenSequence)
    {
        return conn.ExecuteScalar<int>($"SELECT {tenSequence}.NEXTVAL FROM dual", transaction: tran);
    }

    // Tao tai khoan
    public void CreateAccount(string name, string phone, string email, string birth, string adress, string stk, string PASSWORD)
    {
        using OracleConnection conn = Database.GetConnection();
        using OracleTransaction trann = conn.BeginTransaction();
        try
        {
            int customerId = GetNextId(conn, trann, "seq_customers");

            conn.Execute(
                @"INSERT INTO CUSTOMERS (CUSTOMER_ID, FULL_NAME, PHONE, EMAIL, DATE_OF_BIRTH, ADDRESS)
                  VALUES (:customerId, :name, :phone, :email, TO_DATE(:birth,'dd/MM/YYYY'), :adress)",
                new { customerId, name, phone, email, birth, adress }, trann);

            conn.Execute(
                "INSERT INTO ACCOUNTS (ACCOUNT_NUMBER, CUSTOMER_ID, PASSWORD, BALANCE) VALUES (:stk, :customerId, :PASSWORD, 0)",
                new { stk, customerId, PASSWORD }, trann);

            trann.Commit();
        }
        catch
        {
            trann.Rollback();
            throw;
        }
    }

    // Cong/tru so du, tra ve ca so du truoc va sau (dung RETURNING cho so du sau)
    private (decimal before, decimal after) UpdateBalance(OracleConnection conn, OracleTransaction tran, string stk, decimal delta)
    {
        decimal before = conn.ExecuteScalar<decimal>(
            "SELECT BALANCE FROM ACCOUNTS WHERE ACCOUNT_NUMBER = :stk", new { stk }, tran);

        var p = new DynamicParameters();
        p.Add(":delta", delta);
        p.Add(":stk", stk);
        p.Add(":soDuMoi", dbType: DbType.Decimal, direction: ParameterDirection.Output);

        conn.Execute(
            "UPDATE ACCOUNTS SET BALANCE = BALANCE + :delta WHERE ACCOUNT_NUMBER = :stk RETURNING BALANCE INTO :soDuMoi",
            p, tran);

        decimal after = p.Get<decimal>(":soDuMoi");
        return (before, after);
    }

    // Chuyen tien - chi 1 dong TRANSACTIONS, chi track so du ben khoi tao (stk1)
    public void TransferMoney(string stk1, string stk2, decimal a)
{
    using OracleConnection conn = Database.GetConnection();
    using OracleTransaction trann = conn.BeginTransaction();
    try
    {
        var (before1, after1) = UpdateBalance(conn, trann, stk1, -a);
        var (before2, after2) = UpdateBalance(conn, trann, stk2, a);

        int transactionId = GetNextId(conn, trann, "seq_transactions");

        conn.Execute(
            @"INSERT INTO TRANSACTIONS (TRANSACTION_ID, ACCOUNT_NUMBER, COUNTERPART_ACCOUNT_NUMBER, TRANSACTION_TYPE, AMOUNT, BALANCE_BEFORE, BALANCE_AFTER)
              VALUES (:transactionId, :stk1, :stk2, 'TRANSFER_OUT', :a, :before1, :after1)",
            new { transactionId, stk1, stk2, a, before1, after1 }, trann);

        conn.Execute(
            @"INSERT INTO TRANSACTIONS (TRANSACTION_ID, ACCOUNT_NUMBER, COUNTERPART_ACCOUNT_NUMBER, TRANSACTION_TYPE, AMOUNT, BALANCE_BEFORE, BALANCE_AFTER)
              VALUES (:transactionId, :stk2, :stk1, 'TRANSFER_IN', :a, :before2, :after2)",
            new { transactionId, stk2, stk1, a, before2, after2 }, trann);

        trann.Commit();
    }
    catch
    {
        trann.Rollback();
        throw;
    }
}

    // Nap tien
    public void Deposit(string stk, decimal a)
    {
        using OracleConnection conn = Database.GetConnection();
        using OracleTransaction trann = conn.BeginTransaction();
        try
        {
            var (before, after) = UpdateBalance(conn, trann, stk, a);

            int transactionId = GetNextId(conn, trann, "seq_transactions");
            conn.Execute(
                @"INSERT INTO TRANSACTIONS (TRANSACTION_ID, ACCOUNT_NUMBER, COUNTERPART_ACCOUNT_NUMBER, TRANSACTION_TYPE, AMOUNT, BALANCE_BEFORE, BALANCE_AFTER)
                  VALUES (:transactionId, :stk, NULL, 'DEPOSIT', :a, :before, :after)",
                new { transactionId, stk, a, before, after }, trann);

            trann.Commit();
        }
        catch
        {
            trann.Rollback();
            throw;
        }
    }

    // Rut tien
    public void Withdraw(string stk, decimal a)
    {
        using OracleConnection conn = Database.GetConnection();
        using OracleTransaction trann = conn.BeginTransaction();
        try
        {
            var (before, after) = UpdateBalance(conn, trann, stk, -a);

            int transactionId = GetNextId(conn, trann, "seq_transactions");
            conn.Execute(
                @"INSERT INTO TRANSACTIONS (TRANSACTION_ID, ACCOUNT_NUMBER, COUNTERPART_ACCOUNT_NUMBER, TRANSACTION_TYPE, AMOUNT, BALANCE_BEFORE, BALANCE_AFTER)
                  VALUES (:transactionId, :stk, NULL, 'WITHDRAW', :a, :before, :after)",
                new { transactionId, stk, a, before, after }, trann);

            trann.Commit();
        }
        catch
        {
            trann.Rollback();
            throw;
        }
    }

    public decimal GetBalance(string stk)
    {
        using OracleConnection conn = Database.GetConnection();
        return conn.ExecuteScalar<decimal>(
            "SELECT BALANCE FROM ACCOUNTS WHERE ACCOUNT_NUMBER = :stk", new { stk });
    }

    public AccountInfo? GetAccountInfor(string stk)
    {
        using OracleConnection conn = Database.GetConnection();

        string sql = @"SELECT a.ACCOUNT_NUMBER as AccountNumber, c.FULL_NAME as FullName, c.PHONE, c.EMAIL, c.DATE_OF_BIRTH as DateOfBirth, c.ADDRESS, a.BALANCE
                       FROM ACCOUNTS a
                       JOIN CUSTOMERS c ON a.CUSTOMER_ID = c.CUSTOMER_ID
                       WHERE a.ACCOUNT_NUMBER = :stk";

        return conn.QueryFirstOrDefault<AccountInfo>(sql, new { stk });
    }

    public List<TransactionHistory> GetTransactionHistory(string stk)
    {
        using OracleConnection conn = Database.GetConnection();
        string sql = @"SELECT ACCOUNT_NUMBER as AccountNumber , COUNTERPART_ACCOUNT_NUMBER as CounterAccount, TRANSACTION_TYPE as TransactionType, AMOUNT,BALANCE_BEFORE as BalanceBefore, BALANCE_AFTER as BalanceAfter, TRANSACTION_DATE as TransactionDate
                        FROM TRANSACTIONS
                        WHERE ACCOUNT_NUMBER = :stk
                        ORDER BY TRANSACTION_DATE ASC";

        return conn.Query<TransactionHistory>(sql, new { stk }).ToList();
    }
}