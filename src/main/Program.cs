using Microsoft.AspNetCore.Mvc;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .WriteTo.File(
        path: "logs/log_.txt",
        rollingInterval: RollingInterval.Day
    )
    .CreateLogger();

builder.Services.AddOpenApi();
builder.Services.AddSingleton<IAccountDAO, AccountDAO>();
builder.Services.AddSingleton<BankManager>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddLogging(b =>
    b.AddSerilog(dispose: true)
);

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseHttpsRedirection();

app.MapPost("/login", (LoginRequest req, BankManager qlnh) =>
{
    var kq = qlnh.Login(req.Stk, req.Mk);

    return kq switch
    {
        TransactionResult.Success =>
            Results.Ok(new { message = "Dang nhap thanh cong" }),

        TransactionResult.AccountNotFound =>
            Results.NotFound(new { message = "Khong tim thay tai khoan" }),

        TransactionResult.IncorrectPassword =>
            Results.BadRequest(new { message = "Sai mat khau" }),

        _ => Results.StatusCode(500)
    };
});

app.MapPost("/Register",(RsRequest req,BankManager qlnh)=>{
    var kq=qlnh.CreateAccount(req.ten,req.sdt,req.email,req.ngaysinh,req.diachi,req.stk,req.mk);
    return kq switch
    {
        TransactionResult.Success =>
            Results.Created($"/account/{req.stk}",new{message="Tao tai khoan thanh cong",stk=req.stk}),
        TransactionResult.IsNull=>
            Results.BadRequest(new { message = "Khong duoc de trong thong tin" }),
        TransactionResult.AccountAlreadyExists=>
            Results.Conflict(new {message="So tai khoan da ton tai"}),
        TransactionResult.PhoneAlreadyExists=>
            Results.Conflict(new{message="So dien thoai da ton tai"}),
        TransactionResult.EmailAlreadyExists=>
            Results.Conflict(new{message="email da ton tai"}),
        TransactionResult.InvalidDate=>
            Results.BadRequest(new{message="Ngay sinh khong hop le"}),
        _ => Results.StatusCode(500)
    };
});

app.MapPost("/Deposit",(DpRequest req, BankManager qlnh)=>{
    var kq=qlnh.Deposit(req.stk,req.a);
    return kq switch
    {
        TransactionResult.AccountNotFound =>
            Results.NotFound(new { message = "Khong tim thay tai khoan" }),
        TransactionResult.InvalidAmount=>
            Results.BadRequest(new{message = "So tien khong hop le"}),
        TransactionResult.Success =>
            Results.Ok(new {message="Nap tien thanh cong",soDu = qlnh.GetBalance(req.stk)}),
            _=>Results.StatusCode(500)

    };
});
app.MapPost("/Withdraw",(WdRequest req,BankManager qlnh)=>{
    var kq=qlnh.Withdraw(req.stk,req.a);
    return kq switch
    {
        TransactionResult.Success=>
            Results.Ok(new {message="Rut tien thanh cong"}),
        TransactionResult.AccountNotFound=>
            Results.NotFound(new { message = "Khong tim thay tai khoan" }),
        TransactionResult.InvalidAmount=>
            Results.BadRequest(new{message = "So tien khong hop le"}),
        TransactionResult.InsufficientBalance=>
            Results.BadRequest(new{message = "So du khong du"}),
        _=>Results.StatusCode(500)
    };
});
app.MapPost("/TransferMoney",(TmRequest req,BankManager qlnh) =>
{
    var kq=qlnh.TransferMoney(req.stk1,req.stk2,req.a);
    return kq switch
    {
        TransactionResult.Success=>
            Results.Ok(new {message="Chuyen tien thanh cong"}),
        TransactionResult.AccountNotFound=>
            Results.NotFound(new {message="Khong tim thay tai khoan"}),
        TransactionResult.InvalidAmount=>
            Results.BadRequest(new {message="So tien khong hop le"}) ,
        TransactionResult.InsufficientBalance=>
            Results.BadRequest(new{message = "So du khong du"}),
        TransactionResult.CannotTransferToSelf=>
            Results.BadRequest(new { message = "Khong the chuyen tien cho chinh minh" }),
        _=> Results.StatusCode(500)
    };
});
app.MapGet("/GetBalance",(string stk,BankManager qlnh)=>{
    var kq=qlnh.GetBalance(stk);
    if (!qlnh.AccountExists(stk))
        return Results.NotFound(new { message = "Khong tim thay tai khoan" });
    if (kq == -1)
    {
        return Results.StatusCode(500);
    }
    return Results.Ok(new
    {
        stk= stk,
        soDu = kq
    });
});
app.MapGet("/GetAccountInfor", (string stk, BankManager qlnh) =>
{
    if (!qlnh.AccountExists(stk))
        return Results.NotFound(new { message = "Khong tim thay tai khoan" });

    var kq = qlnh.GetAccountInfor(stk);
    if (kq == null) return Results.StatusCode(500);

    return Results.Ok(new {
        kq.AccountNumber,
        kq.FullName,
        kq.Phone,
        kq.Email,
        DateOfBirth = kq.DateOfBirth?.ToString("dd/MM/yyyy"),
        kq.Address,
        kq.Balance});

});
app.MapGet("/GetTransactionHistory", (string stk, BankManager qlnh) =>
{
    if (!qlnh.AccountExists(stk))
        return Results.NotFound(new { message = "Khong tim thay tai khoan" });

    var kq = qlnh.GetTransactionHistory(stk);
    return Results.Ok(kq);
});
app.Run();

record TmRequest(string stk1,string stk2, decimal a);
record WdRequest(string stk,decimal a);
record DpRequest(string stk,decimal a);
record RsRequest(string ten, string sdt, string email, string ngaysinh, string diachi, string stk, string mk);
record LoginRequest(string Stk, string Mk);