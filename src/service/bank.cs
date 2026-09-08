using System;
using System.Collections.Generic;
using Oracle.ManagedDataAccess.Client;
using Serilog;
using Microsoft.Extensions.Logging;
enum TransactionResult
{
    Success,
    //tk tồn tại
    AccountAlreadyExists,
    //ko tìm thấy tk
    AccountNotFound,
    //sai mk
    IncorrectPassword,
    //số tiền ko hợp lệ
    InvalidAmount,
    //số dư không đủ
    InsufficientBalance,
    //ko chuyển tiền cho chính mình
    CannotTransferToSelf,
    //sđt đã tồn tại
    PhoneAlreadyExists,
    //email đã tồn tại
    EmailAlreadyExists,
    //Lỗi hệ thống
    SystemError,
    //Ngày sinh không hợp lệ
    InvalidDate,
    // Thông tin trống
    IsNull
}

class BankManager
{
    private readonly IAccountDAO acd;
    private readonly ILogger<BankManager> logger;

    public BankManager(IAccountDAO acd, ILogger<BankManager> logger)
    {
        this.acd = acd;
        this.logger = logger;
    }
    public bool AccountExists(string stk)
    {
        return acd.Exists(stk);
    }

    public TransactionResult CreateAccount(string ten, string sdt, string email, string ngaysinh, string diachi, string stk, string mk)
    {
        if (string.IsNullOrWhiteSpace(ten) || string.IsNullOrWhiteSpace(stk) || string.IsNullOrWhiteSpace(mk))
        {
            logger.LogWarning("Dang ky - Thong tin khong duoc de trong");
            return TransactionResult.IsNull;
        }
        try
        {
            if (acd.Exists(stk)){ 
                logger.LogWarning("Dang Ki - Sotk {stk} da ton tai",stk);
                return TransactionResult.AccountAlreadyExists;
            }
            else
            {
                acd.CreateAccount(ten, sdt, email, ngaysinh, diachi, stk, mk);
                logger.LogInformation("Tao Tai Khoan - Tao tai khoan {stk} thanh cong",stk);
                return TransactionResult.Success;
            }
        }
        catch (OracleException ex)
        {
            if (ex.Number > 0)
            {
                if (ex.Message.Contains("UQ_KHACHHANG_SDT")){
                    return TransactionResult.PhoneAlreadyExists;
                }
                if (ex.Message.Contains("UQ_KHACHHANG_EMAIL")){
                    logger.LogWarning("Tao tai khoan - Email {email} da ton tai",email);
                    return TransactionResult.EmailAlreadyExists;
                }

                if (ex.Message.Contains("PK_TAI_KHOAN")){
                    logger.LogWarning("Tao tai khoan - So tai khoan {stk} da ton tai",stk);
                    return TransactionResult.AccountAlreadyExists;
                }
                if (ex.Message.Contains("month")){
                     logger.LogWarning("Tao tai khoan - Ngay sinh {ngaysinh} khong hop le",ngaysinh);
                    return TransactionResult.InvalidDate;
                }
            }
           logger.LogError(ex,"Tao tai khoan - Co loi xay ra");
            return TransactionResult.SystemError;
        }
    }
    //Đăng nhập
    public TransactionResult Login(string stk, string mk)
    {
        try{
        if (!AccountExists(stk)){
            logger.LogWarning("So tai khoan {Stk} khong ton tai", stk);
            return TransactionResult.AccountNotFound;
        }

        if (!acd.checkPassWord(stk, mk)){
            logger.LogWarning("Dang Nhap - So tai khoan {stk} nhap khong dung mat khau",stk);
            return TransactionResult.IncorrectPassword;
        }
        logger.LogInformation("Dang Nhap - stk {stk} Dang nhap thanh cong",stk);
        return TransactionResult.Success;
        }
        catch(Exception ex)
        {
            logger.LogError(ex,"Dang Nhap - Loi he thong khi STK {stk} dang nhap",stk);
            return TransactionResult.SystemError;
        }
    }
    // Nạp tiền
    public TransactionResult Deposit(string stk, decimal a)
    {
        try{
        if (!AccountExists(stk)){
            logger.LogWarning("Nap tien - So tai khoan {stk} khong ton tai, nap tien that bai ",stk);
            return TransactionResult.AccountNotFound;
        }
        if (a <= 0){
            logger.LogWarning("Nap tien - So tai khoan {stk} nap tien that bai, so tien {a} khong hop le",stk,a);
            return TransactionResult.InvalidAmount;
        }
        acd.Deposit(stk, a);
        logger.LogInformation("Nap Tien - So tai khoan {stk} nap tien thanh cong, so tien {a}",stk,a);
        return TransactionResult.Success;
        }
        catch(Exception ex)
        {
           logger.LogError(ex,"Nap tien - Loi he thong khi STK {stk} nap tien",stk);
            return TransactionResult.SystemError;
        }
    }
    // Rút tiền
    public TransactionResult Withdraw(string stk, decimal a)
    {
        try{
        if (!AccountExists(stk)){
            logger.LogWarning("Rut tien - So tai khoan {stk} khong ton tai,rut tien that bai",stk);
            return TransactionResult.AccountNotFound;
        }
        if (a <= 0)
            {
            logger.LogWarning("Rut tien - So tai khoan {stk} rut tien that bai, so tien {a} khong hop le",stk,a);
            return TransactionResult.InvalidAmount;
            }
        if (a > acd.GetBalance(stk))
            {
            logger.LogWarning("Rut tien - So tai khoan {stk} rut tien that bai, so du khong du {a}",stk,a);
            return TransactionResult.InsufficientBalance;
            }
        acd.Withdraw(stk, a);
        logger.LogWarning("Rut tien - So tai khoan {stk} rut tien thanh cong",stk);
        return TransactionResult.Success;
        }
        catch(Exception ex)
        {
            logger.LogError(ex,"Rut tien - Loi he thong khi STK {stk} rut tien", stk);
            return TransactionResult.SystemError;
        }
    }
    // Chuyển tiền
    public TransactionResult TransferMoney(string stk1, string stk2, decimal a)
    {
        try{
        if (stk1 == stk2)
            {
            logger.LogWarning("Chuyen tien - So tai khoan {stk1} khong the chuyen cho chinh minh,chuyen tien that bai",stk1);
            return TransactionResult.CannotTransferToSelf;
            }
        if (!AccountExists(stk2))
            {
            logger.LogWarning("Chuyen tien - So tai khoan {stk2} khong ton tai,chuyen tien that bai",stk2);
            return TransactionResult.AccountNotFound;
            }
        if (a <= 0)
            {
            logger.LogWarning("Chuyen tien - So tai khoan {stk1} chuyen tien that bai,so tien khong hop le :{a}",stk1,a);
            return TransactionResult.InvalidAmount;
            }
        if (a > acd.GetBalance(stk1)){
            logger.LogWarning("Chuyen tien - So tai khoan {stk1} khong du so du {a},chuyen tien that bai",stk1,a);
            return TransactionResult.InsufficientBalance;
        }
        acd.TransferMoney(stk1, stk2, a);
        logger.LogInformation("Chuyen tien - So tai khoan {stk1} chuyen tien thanh cong cho so tai khoan {stk2}, So tien {a}",stk1,stk2,a);
        return TransactionResult.Success;
        }
        catch(Exception ex)
        {
            logger.LogError(ex,"Chuyen Tien - Loi he thong khi STK {stk1} chuyen tien cho {stk2}", stk1,stk2);
            return TransactionResult.SystemError;
        }
    }

    public decimal GetBalance(string stk)
    {
        try{
        logger.LogInformation("Xem so du - Xem so du {stk} thanh cong",stk);
        return acd.GetBalance(stk);
        }
        catch(Exception ex)
        {
            logger.LogError(ex,"Xem So du - Loi he thong khi lay so du {stk}",stk);
            return -1;
        }
    }

    public AccountInfo? GetAccountInfor(string stk)
    {
        try{
        logger.LogInformation("Xem thong tin - Xem thong tin {stk} thanh cong",stk);
        return acd.GetAccountInfor(stk);
        }
        catch(Exception ex)
        {
            logger.LogError(ex,"Lay thong tin - Loi he thong khi lay thong tin {stk}",stk);
            return null;
        }
    }

    public List<TransactionHistory> GetTransactionHistory(string stk)
{
    try
    {
        logger.LogInformation("Xem lich su - Xem lich su giao dich {Stk} thanh cong", stk);
        List<TransactionHistory> ds = acd.GetTransactionHistory(stk);
        foreach (TransactionHistory gd in ds)
        {
            gd.Direction = gd.TransactionType switch
            {
                "TRANSFER_OUT" => "Chuyen di",
                "TRANSFER_IN" => "Nhan ve",
                "DEPOSIT" => "Nap tien",
                "WITHDRAW" => "Rut tien",
                _ => gd.TransactionType
            };
        }
        return ds;
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Lay lich su giao dich - Loi he thong khi lay lich su giao dich {Stk}", stk);
        return new List<TransactionHistory>();
    }
}
}