# E-Greeting

Ứng dụng web ASP.NET Core để gửi thiệp điện tử (e-card). Người dùng chọn thiệp từ danh mục trực tuyến, cá nhân hóa bằng lời nhắn, ảnh hoặc video, nhập email người nhận, và website sẽ gửi thiệp qua email thay cho người gửi.

E-card là phiên bản kỹ thuật số của bưu thiếp hoặc thiệp chúc mừng.

## Chức năng

### Website
- Mẫu thiệp phân theo danh mục: Sinh nhật, Đám cưới, Năm mới, Lễ hội, v.v.
- Điều hướng: Trang chủ, các danh mục, Đăng ký gói (Subscribe), Phản hồi (Feedback), Đăng nhập (Admin / User), Đăng ký tài khoản
- Header và footer ở mọi trang

### Người dùng (User)
- Đăng ký, đăng nhập (xác thực bằng cookie, phiên làm việc 2 giờ)
- Xem mẫu thiệp và chỉnh sửa (lời nhắn, ảnh, video)
- Gửi thiệp: cửa sổ gửi gồm **To**, **Subject** và nút **Send**
- Gửi phản hồi cho admin

### Gói đăng ký (gửi thiệp hằng ngày)
- Người dùng đã đăng ký có thể đăng ký gói gửi thiệp tự động mỗi ngày
- Yêu cầu tối thiểu **10 địa chỉ email người nhận**
- Trả phí theo tháng. Admin kiểm tra thanh toán, nếu không thanh toán hoặc hết tháng thì dịch vụ bị vô hiệu hóa
- Một background service tự động gửi thiệp theo lịch
- Một background service gửi email nhắc người dùng VIP trước khi gói hết hạn

### Quản trị (Admin)
- Thêm, xóa mẫu thiệp và danh mục
- Xem phản hồi của người dùng
- Xem toàn bộ giao dịch: thiệp nào được gửi, gửi đến đâu, lúc nào
- Báo cáo giao dịch hằng ngày
- Vô hiệu hóa gói đăng ký của người dùng

## Công nghệ

- ASP.NET Core MVC (C#)
- Entity Framework Core (code-first, migrations)
- Xác thực bằng Cookie
- Hosted background services (gửi thiệp theo lịch, nhắc hết hạn gói)
- LibMan quản lý thư viện phía client (`wwwroot/lib`)
- Fabric.js để chỉnh sửa thiệp trên trình duyệt

## Cấu trúc thư mục

```
E-Greeting/
├── Context/          AppDbContext (EF Core)
├── Controllers/
├── Migrations/
├── Models/
├── Service/          Xử lý nghiệp vụ (Account, Email, Template, Category, Subscription...)
├── ViewModels/
├── Views/
├── wwwroot/          audio, css, fabric.js, js, lib, uploads
├── libman.json
├── Program.cs
└── E-Greeting.csproj
```

## Hướng dẫn chạy

### Yêu cầu
- [.NET SDK](https://dotnet.microsoft.com/download) (đúng phiên bản trong `E-Greeting.csproj`)
- Database server phù hợp với EF Core provider của project
- Visual Studio (khuyến nghị) hoặc .NET CLI
- Tài khoản SMTP để gửi email

### 1. Clone

```bash
git clone <repo-url>
cd E-Greetings/E-Greeting
```

### 2. Cấu hình

File `appsettings.json` không được commit (nằm trong `.gitignore`). Hãy tạo file này trong thư mục project `E-Greeting`, dùng đúng tên key mà code đọc. Ví dụ:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "<chuoi-ket-noi-cua-ban>"
  },
  "EmailSettings": {
    "SmtpServer": "smtp.gmail.com",
    "Port": 587,
    "Username": "<email-cua-ban>",
    "Password": "<app-password>"
  }
}
```

Không commit thông tin đăng nhập thật lên repo.

### 3. Restore, tạo database, chạy

```bash
dotnet restore
dotnet tool install --global dotnet-ef   # chỉ cần lần đầu
dotnet ef database update
libman restore                           # thư viện client-side
dotnet run
```

Với Visual Studio: mở `E-Greeting.slnx`, chuột phải `libman.json` > **Restore Client-Side Libraries**, rồi nhấn **F5**.

Mở đường dẫn hiện trong console (ví dụ `https://localhost:xxxx`).

## Xử lý lỗi thường gặp

| Vấn đề | Cách sửa |
|---|---|
| Tab file ghi "Miscellaneous Files" | Mở file `E-Greeting.slnx` thay vì chỉ mở thư mục |
| `The system cannot find the path specified` | `cd` vào đúng thư mục project `E-Greeting` (nằm trong `E-Greetings`) |
| App crash khi khởi động | Kiểm tra `appsettings.json` đã có và connection string đúng |
| Lỗi database | Chạy `dotnet ef database update` |
| Thiếu thư viện CSS/JS | Chạy `libman restore` |
| Không gửi được email | Kiểm tra cấu hình SMTP; Gmail cần dùng App Password |

## Nguồn yêu cầu

Dựa trên đề bài và bản đặc tả dự án eProject *E-Greetings* của Aptech.
