# VNUFLearning.Gateway

API Gateway cho VNUFLearning, lấy cảm hứng từ FishShop.Gateway. Project **cùng solution** `VNUFLearning.sln` (đặt trong solution folder `Gateway`).

## Mục tiêu

- Reverse proxy (YARP) đứng trước VNUFLearning, là điểm vào duy nhất cho client.
- Kiểm JWT (Bearer token hoặc cookie `access_token`) trên các route bảo vệ.
- Chặn role-based: `/Admin/*` → `Admin`, `/Teacher/*` → `GiangVien`, `/Student/*` → `SinhVien`.
- Inject header `X-User-Id`, `X-User-Role`, `X-User-Name`, `X-Api-Key` xuống backend.
- Rate limiting theo IP (siết chặt cho login).
- Logging tập trung qua Serilog (file + console).

## Chạy

### Cách 1 — Visual Studio (khuyến nghị)
Mở `VNUFLearning.sln`, chọn launch profile **"All (Backend + Gateway)"** trên thanh toolbar (cạnh nút Start), bấm F5. Cả 2 project khởi chạy cùng lúc.

### Cách 2 — Terminal (2 cửa sổ)
```powershell
# Terminal 1 - backend (5153)
dotnet run --project C:\Users\Admin\Desktop\VNUFLearning\VNUFLearning\VNUFLearning.csproj

# Terminal 2 - gateway (7000)
dotnet run --project C:\Users\Admin\Desktop\VNUFLearning\Gateway\VNUFLearning.Gateway\VNUFLearning.Gateway.csproj
```

Truy cập ứng dụng qua `http://localhost:7000/Account/Login` (KHÔNG vào trực tiếp port 5153 nữa).

## Flow đăng nhập

### 1. MVC (browser)
- Browser → Gateway → `/Account/Login` (public).
- POST username/password → backend kiểm tra mật khẩu (BCrypt + auto-rehash legacy) → set Cookie auth + cookie `access_token` (JWT).
- Các request sau, Gateway đọc `access_token` từ cookie, validate JWT, inject header và forward.

### 2. API (mobile/SPA/khác)
- POST `http://localhost:7000/api/auth/login` với JSON `{ "username": "...", "password": "..." }`.
- Trả về `accessToken` + `expiresAt`.
- Các request sau gửi `Authorization: Bearer <token>` qua Gateway.

## Cấu hình quan trọng (`appsettings.json`)

| Key | Ý nghĩa |
| --- | --- |
| `Gateway:ApiKey` | Khoá nội bộ Gateway gửi xuống backend. Phải khớp với `Gateway:ApiKey` trong `VNUFLearning/appsettings.json`. |
| `Gateway:JwtSecret` | Khoá ký JWT, phải khớp với `Jwt:Secret` của backend. |
| `Gateway:UnauthenticatedPaths` | Các path bypass JWT (login, static, healthcheck...). |
| `Gateway:RoleProtectedPaths` | Map prefix → role bắt buộc. |
| `IpRateLimiting.GeneralRules` | Giới hạn request theo IP. |

## Lưu ý production

- Đổi `Gateway:JwtSecret` và `Gateway:ApiKey` sang giá trị mạnh, đưa vào secret store (User Secrets / Azure Key Vault / env var) — KHÔNG commit.
- Bật HTTPS, set cookie `Secure=true`.
- Ràng `KnownProxies` trong `ForwardedHeadersOptions` cho prod để tránh spoof IP.
