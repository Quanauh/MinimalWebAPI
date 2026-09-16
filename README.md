### Tìm hiểu về JWT trong WebApi
# Khởi tạo thư viện
Chạy lệnh
```bash
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
```

```
Client gửi JWT
        ↓
ASP.NET Core
        ↓
Kiểm tra Issuer
        ↓
Kiểm tra Audience
        ↓
Kiểm tra thời hạn
        ↓
Kiểm tra chữ ký bằng Jwt:Key
        ↓
Tất cả đúng
        ↓
Token hợp lệ
        ↓
Cho vào endpoint [Authorize]
```