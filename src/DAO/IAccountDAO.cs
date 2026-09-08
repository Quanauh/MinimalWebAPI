interface IAccountDAO
{
    bool Exists(string stk);
    bool checkPassWord(string stk, string mk);
    void CreateAccount(string ten, string sdt, string email, string ngaysinh, string diachi, string stk, string mk);
    void Deposit(string stk, decimal a);
    void Withdraw(string stk, decimal a);
    void TransferMoney(string stk1, string stk2, decimal a);
    decimal GetBalance(string stk);
    AccountInfo? GetAccountInfor(string stk);
    List<TransactionHistory> GetTransactionHistory(string stk);
}