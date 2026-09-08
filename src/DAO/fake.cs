using Dapper;
using Oracle.ManagedDataAccess.Client;
using System.Data;
class FakeAccountDAO: IAccountDAO

{
    public bool Exists(string stk) => stk == "TEST123";
    public bool checkPassWord(string stk, string mk) => mk == "1234";
    private int GetNextId(OracleConnection conn, OracleTransaction tran, string tenSequence)
    {
        return conn.ExecuteScalar<int>($"SELECT {tenSequence}.NEXTVAL FROM dual", transaction: tran);
    }

    // Tạo tài khoản
    public void CreateAccount(string ten, string sdt, string email, string ngaysinh, string diachi, string stk, string mk)
    {
        using OracleConnection conn = Database.GetConnection();
        using OracleTransaction trann = conn.BeginTransaction();
        try
        {
            int maKh = GetNextId(conn, trann, "seq_khach_hang");

            conn.Execute(
                @"INSERT INTO khach_hang(MAKH,HOTEN,SDT,EMAIL,NGAYSINH,DIACHI)
                  VALUES (:maKh,:ten,:sdt,:email,TO_DATE(:ngaysinh,'dd/MM/YYYY'),:diachi)",
                new { maKh, ten, sdt, email, ngaysinh, diachi }, trann);

            conn.Execute(
                "INSERT INTO tai_khoan (SOTK,MAKH,MATKHAU) VALUES (:stk,:maKh,:mk)",
                new { stk, maKh, mk }, trann);

            trann.Commit();
        }
        catch
        {
            trann.Rollback();
            throw;
        }
    }

    //Ghi 1 dòng số dư sau giao dịch cho 1 tài khoản, gắn với 1 mã giao dịch (maGd)
    private void BalanceAfterTranaction(OracleConnection conn, OracleTransaction tran, int maGd, string stk, decimal soDuMoi)
    {
        int maSoDu = GetNextId(conn, tran, "seq_so_du_sau_gd");
        conn.Execute(
            "INSERT INTO LICH_SU_SO_DU (MASODU,MAGD,SOTK,SODUSAUGD) VALUES (:maSoDu,:maGd,:stk,:soDu)",
            new { maSoDu, maGd, stk, soDu = soDuMoi }, tran);
    }

    //Cộng/trừ số dư 1 tài khoản, trả về số dư mới luôn (dùng RETURNING, tránh phải SELECT lại)
    //Dapper không trực tiếp trả về output parameter qua kết quả Execute, nên dùng DynamicParameters
    //để khai báo :soDuMoi là tham số OUTPUT rồi đọc lại bằng p.Get<decimal>(...)
    private decimal UpdateBalance(OracleConnection conn, OracleTransaction tran, string stk, decimal a)
    {
        var p = new DynamicParameters();
        p.Add(":delta", a);
        p.Add(":stk", stk);
        p.Add(":soDuMoi", dbType: DbType.Decimal, direction: ParameterDirection.Output);

        conn.Execute(
            "UPDATE TAI_KHOAN SET SODU = SODU + :delta WHERE SOTK = :stk RETURNING SODU INTO :soDuMoi",
            p, tran);

        return p.Get<decimal>(":soDuMoi");
    }

    //chuyen tien - 1 dong GIAO_DICH + 2 dong SO_DU_SAU_GD (ben gui, ben nhan), cung 1 transaction
    public void TransferMoney(string stk1, string stk2, decimal a)
    {
        using OracleConnection conn = Database.GetConnection();
        using OracleTransaction trann = conn.BeginTransaction();
        try
        {
            decimal soDuStk1 = UpdateBalance(conn, trann, stk1, -a);
            decimal soDuStk2 = UpdateBalance(conn, trann, stk2, a);

            int maGd = GetNextId(conn, trann, "seq_giao_dich");
            conn.Execute(
                "INSERT INTO GIAO_DICH (MAGD,SOTK,SOTK_DOI,LOAIGD,SOTIEN) VALUES (:maGd,:stk1,:stk2,'CHUYEN',:a)",
                new { maGd, stk1, stk2, a }, trann);

            BalanceAfterTranaction(conn, trann, maGd, stk1, soDuStk1);
            BalanceAfterTranaction(conn, trann, maGd, stk2, soDuStk2);

            trann.Commit();
        }
        catch
        {
            trann.Rollback();
            throw;
        }
    }
    // chuyen tien bang procedure
        public void TransferMoney1(string stk1, string stk2, decimal a)
    {
        using OracleConnection conn = Database.GetConnection();
        var p = new DynamicParameters();
        p.Add(":p_stk1", stk1);
        p.Add(":p_stk2", stk2);
        p.Add(":p_sotien", a);
 
        conn.Execute("sp_chuyen_tien", p, commandType: CommandType.StoredProcedure);
    }

    //Nạp tiền - 1 dong GIAO_DICH + 1 dong SO_DU_SAU_GD, cung 1 transaction
    public void Deposit(string stk, decimal a)
    {
        using OracleConnection conn = Database.GetConnection();
        using OracleTransaction trann = conn.BeginTransaction();
        try
        {
            decimal soDuMoi = UpdateBalance(conn, trann, stk, a);

            int maGd = GetNextId(conn, trann, "seq_giao_dich");
            conn.Execute(
                "INSERT INTO GIAO_DICH (MAGD,SOTK,SOTK_DOI,LOAIGD,SOTIEN) VALUES (:maGd,:stk,NULL,'NAP',:a)",
                new { maGd, stk, a }, trann);

            BalanceAfterTranaction(conn, trann, maGd, stk, soDuMoi);
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
        return conn.ExecuteScalar<decimal>("SELECT sodu FROM tai_khoan WHERE SOTK=:stk", new { stk });
    }
    public void Withdraw1(string stk,decimal a)
    {
        using OracleConnection conn=Database.GetConnection();
        var p=new DynamicParameters();
        p.Add(":p_stk",stk);
        p.Add(":p_sotien",a);
        conn.Execute("sp_rut_tien",p,commandType:CommandType.StoredProcedure);

    }
    //Rut tien - 1 dong GIAO_DICH + 1 dong SO_DU_SAU_GD, cung 1 transaction
    public void Withdraw(string stk, decimal a)
    {
        using OracleConnection conn = Database.GetConnection();
        using OracleTransaction trann = conn.BeginTransaction();
        try
        {
            decimal soDuMoi = UpdateBalance(conn, trann, stk, -a);

            int maGd = GetNextId(conn, trann, "seq_giao_dich");
            conn.Execute(
                "INSERT INTO GIAO_DICH (MAGD,SOTK,SOTK_DOI,LOAIGD,SOTIEN) VALUES (:maGd,:stk,NULL,'RUT',:a)",
                new { maGd, stk, a }, trann);

            BalanceAfterTranaction(conn, trann, maGd, stk, soDuMoi);
            trann.Commit();
        }
        catch
        {
            trann.Rollback();
            throw;
        }
    }

    public AccountInfo? GetAccountInfor(string stk)
    {
        using OracleConnection conn = Database.GetConnection();

        string sql = @"SELECT tk.SOTK, kh.HOTEN, kh.SDT, kh.EMAIL, kh.NGAYSINH, kh.DIACHI, tk.SODU
                       FROM TAI_KHOAN tk
                       JOIN KHACH_HANG kh ON tk.MAKH = kh.MAKH
                       WHERE tk.SOTK = :stk";

        return conn.QueryFirstOrDefault<AccountInfo>(sql, new { stk });
    }

    
    public List<TransactionHistory> GetTransactionHistory(string stk)
    {
        using OracleConnection conn = Database.GetConnection();
        string sql = @"SELECT gd.SOTK, gd.SOTK_DOI, gd.LOAIGD, gd.SOTIEN, ls.SODUSAUGD, gd.THOIGIANGD
                        FROM GIAO_DICH gd
                        JOIN LICH_SU_SO_DU ls ON ls.MAGD = gd.MAGD AND ls.SOTK = :stk1
                        WHERE gd.SOTK = :stk2 OR gd.SOTK_DOI = :stk3
                        ORDER BY gd.THOIGIANGD ASC";

        return conn.Query<TransactionHistory>(sql, new { stk1 = stk, stk2 = stk, stk3 = stk }).ToList();
    }
}
