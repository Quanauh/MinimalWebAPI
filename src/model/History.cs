using System;

class TransactionHistory
{
    public string AccountNumber { get; set; } = "";
    public string? CounterAccount { get; set; }
    public string TransactionType { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal BalanceBefore { get; set; }
    public decimal BalanceAfter { get; set; }   // nullable - se la null khi day la giao dich "nhan ve"
    public DateTime TransactionDate { get; set; }
    public string Direction { get; set; } = "";

    public override string ToString()
    {
        string balancePart =$" - Balance after: {BalanceAfter}";
        return $"[{TransactionDate:dd/MM/yyyy HH:mm:ss}] {Direction} - Amount: {Amount}{balancePart}"
             + (CounterAccount != null ? $" - Counterpart: {CounterAccount}" : "");
    }
}