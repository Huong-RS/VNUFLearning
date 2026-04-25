using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VNUFLearning.Data;
using VNUFLearning.Models;
using VNUFLearning.Models.ViewModels;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using System.Data;
namespace VNUFLearning.Controllers.Admin
{
    [Authorize(Roles = "Admin")]
    [Route("Admin/[controller]/[action]")]
    public class UsersController : Controller
    {
        private readonly VnufLearningContext _context;

        public UsersController(VnufLearningContext context)
        {
            _context = context;
        }

        [HttpGet]
        [Route("~/Admin/Users")]
        public async Task<IActionResult> Index(string? role, string? keyword, string? status)
        {
            var query = _context.Users
                .Include(u => u.Role)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(role))
            {
                query = query.Where(u => u.Role.RoleName == role);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                if (status == "active")
                {
                    query = query.Where(u => u.IsActive);
                }
                else if (status == "locked")
                {
                    query = query.Where(u => !u.IsActive);
                }
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim();
                query = query.Where(u =>
                    u.StudentCode.Contains(keyword) ||
                    u.FullName.Contains(keyword) ||
                    (u.Email != null && u.Email.Contains(keyword)));
            }

            var users = await query
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();

            var allUsers = await _context.Users
                .Include(u => u.Role)
                .ToListAsync();

            ViewBag.TotalUsers = allUsers.Count;
            ViewBag.TotalStudents = allUsers.Count(u => u.Role.RoleName == "SinhVien");
            ViewBag.TotalTeachers = allUsers.Count(u => u.Role.RoleName == "GiangVien");
            ViewBag.TotalAdmins = allUsers.Count(u => u.Role.RoleName == "Admin");
            ViewBag.TotalLocked = allUsers.Count(u => !u.IsActive);

            ViewBag.CurrentRole = role ?? "";
            ViewBag.CurrentKeyword = keyword ?? "";
            ViewBag.CurrentStatus = status ?? "";

            return View("~/Views/Admin/Users/Index.cshtml", users);
        }

        [HttpGet]
        [Route("~/Admin/Users/Create")]
        public async Task<IActionResult> Create()
        {
            await LoadRolesAsync();
            return View("~/Views/Admin/Users/Create.cshtml", new UserFormViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("~/Admin/Users/Create")]
        public async Task<IActionResult> Create(UserFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await LoadRolesAsync(model.RoleId);
                return View("~/Views/Admin/Users/Create.cshtml", model);
            }

            var studentCode = model.StudentCode.Trim();

            var existedUser = await _context.Users
                .AnyAsync(u => u.StudentCode == studentCode);

            if (existedUser)
            {
                ModelState.AddModelError("StudentCode", "Mã tài khoản đã tồn tại.");
                await LoadRolesAsync(model.RoleId);
                return View("~/Views/Admin/Users/Create.cshtml", model);
            }

            var password = string.IsNullOrWhiteSpace(model.Password)
                ? "123456"
                : model.Password.Trim();

            var user = new User
            {
                StudentCode = studentCode,
                FullName = model.FullName.Trim(),
                Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim(),
                PasswordHash = password,
                RoleId = model.RoleId,
                CreatedAt = DateTime.Now,
                IsActive = true,
                ClassName = string.IsNullOrWhiteSpace(model.ClassName) ? null : model.ClassName.Trim(),
                DepartmentName = string.IsNullOrWhiteSpace(model.DepartmentName) ? null : model.DepartmentName.Trim()
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã tạo tài khoản {user.StudentCode} thành công.";
            return RedirectToAction(nameof(Index));
        }
        [HttpGet]
        [Route("~/Admin/Users/DownloadTemplate")]
        public IActionResult DownloadTemplate()
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("UsersTemplate");

            ws.Cell(1, 1).Value = "StudentCode";
            ws.Cell(1, 2).Value = "FullName";
            ws.Cell(1, 3).Value = "Email";
            ws.Cell(1, 4).Value = "ClassName";
            ws.Cell(1, 5).Value = "DepartmentName";

            ws.Cell(2, 1).Value = "2274801";
            ws.Cell(2, 2).Value = "Nguyễn Văn A";
            ws.Cell(2, 3).Value = "nguyenvana@gmail.com";
            ws.Cell(2, 4).Value = "K67CNTT";
            ws.Cell(2, 5).Value = "Khoa Công nghệ thông tin";

            ws.Cell(3, 1).Value = "GV01";
            ws.Cell(3, 2).Value = "Giảng viên Nguyễn Văn B";
            ws.Cell(3, 3).Value = "gv01@vnuf.edu.vn";
            ws.Cell(3, 4).Value = "";
            ws.Cell(3, 5).Value = "Khoa Công nghệ thông tin";

            ws.Row(1).Style.Font.Bold = true;
            ws.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            return File(
                stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "Mau_Import_TaiKhoan.xlsx"
            );
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("~/Admin/Users/ImportExcel")]
        public async Task<IActionResult> ImportExcel(IFormFile excelFile, string importRole)
        {
            if (excelFile == null || excelFile.Length == 0)
            {
                TempData["Success"] = "Vui lòng chọn file Excel để import.";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(importRole) || (importRole != "SinhVien" && importRole != "GiangVien"))
            {
                TempData["Success"] = "Loại import không hợp lệ. Chỉ được chọn Sinh viên hoặc Giảng viên.";
                return RedirectToAction(nameof(Index));
            }

            var extension = Path.GetExtension(excelFile.FileName).ToLower();
            if (extension != ".xlsx")
            {
                TempData["Success"] = "Chỉ hỗ trợ file Excel định dạng .xlsx";
                return RedirectToAction(nameof(Index));
            }

            var role = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == importRole);
            if (role == null)
            {
                TempData["Success"] = $"Không tìm thấy role {importRole} trong hệ thống.";
                return RedirectToAction(nameof(Index));
            }

            var successCount = 0;
            var errorMessages = new List<string>();

            using var stream = new MemoryStream();
            await excelFile.CopyToAsync(stream);
            stream.Position = 0;

            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheet(1);
            var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;

            if (lastRow < 2)
            {
                TempData["Success"] = "File Excel không có dữ liệu để import.";
                return RedirectToAction(nameof(Index));
            }

            for (int row = 2; row <= lastRow; row++)
            {
                try
                {
                    var studentCode = worksheet.Cell(row, 1).GetValue<string>().Trim();
                    var fullName = worksheet.Cell(row, 2).GetValue<string>().Trim();
                    var email = worksheet.Cell(row, 3).GetValue<string>().Trim();
                    var className = worksheet.Cell(row, 4).GetValue<string>().Trim();
                    var departmentName = worksheet.Cell(row, 5).GetValue<string>().Trim();

                    if (string.IsNullOrWhiteSpace(studentCode))
                    {
                        errorMessages.Add($"Dòng {row}: StudentCode không được để trống.");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(fullName))
                    {
                        errorMessages.Add($"Dòng {row}: FullName không được để trống.");
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(email))
                    {
                        try
                        {
                            var _ = new System.ComponentModel.DataAnnotations.EmailAddressAttribute();
                            if (!_.IsValid(email))
                            {
                                errorMessages.Add($"Dòng {row}: Email không hợp lệ.");
                                continue;
                            }
                        }
                        catch
                        {
                            errorMessages.Add($"Dòng {row}: Email không hợp lệ.");
                            continue;
                        }
                    }

                    var existedUser = await _context.Users
                        .AnyAsync(u => u.StudentCode == studentCode);

                    if (existedUser)
                    {
                        errorMessages.Add($"Dòng {row}: Mã tài khoản '{studentCode}' đã tồn tại.");
                        continue;
                    }

                    var newUser = new User
                    {
                        StudentCode = studentCode,
                        FullName = fullName,
                        Email = string.IsNullOrWhiteSpace(email) ? null : email,
                        ClassName = string.IsNullOrWhiteSpace(className) ? null : className,
                        DepartmentName = string.IsNullOrWhiteSpace(departmentName) ? null : departmentName,
                        PasswordHash = "123456",
                        RoleId = role.RoleId,
                        CreatedAt = DateTime.Now,
                        IsActive = true
                    };

                    _context.Users.Add(newUser);
                    successCount++;
                }
                catch (Exception ex)
                {
                    errorMessages.Add($"Dòng {row}: Lỗi xử lý dữ liệu. {ex.Message}");
                }
            }

            if (successCount > 0)
            {
                await _context.SaveChangesAsync();
            }

            if (errorMessages.Any())
            {
                TempData["Success"] =
                    $"Import thành công {successCount} tài khoản. " +
                    $"Có {errorMessages.Count} dòng lỗi: " +
                    string.Join(" | ", errorMessages);
            }
            else
            {
                TempData["Success"] = $"Import thành công {successCount} tài khoản {importRole}.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [Route("~/Admin/Users/Edit/{id}")]

        public async Task<IActionResult> Edit(int id)
        {

            var user = await _context.Users.FindAsync(id);

            if (user == null)
            {

                TempData["Success"] = "Không tìm thấy tài khoản cần sửa.";
                return RedirectToAction(nameof(Index));
            }

            var model = new UserFormViewModel
            {
                UserId = user.UserId,
                StudentCode = user.StudentCode,
                FullName = user.FullName,
                Email = user.Email,
                RoleId = user.RoleId,
                ClassName = user.ClassName,
                DepartmentName = user.DepartmentName
            };

            await LoadRolesAsync(model.RoleId);
            return View("~/Views/Admin/Users/Edit.cshtml", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("~/Admin/Users/Edit/{id}")]
        public async Task<IActionResult> Edit(int id, UserFormViewModel model)
        {
            if (id != model.UserId)
            {
                return BadRequest();
            }

            if (!ModelState.IsValid)
            {
                await LoadRolesAsync(model.RoleId);
                return View("~/Views/Admin/Users/Edit.cshtml", model);
            }

            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                TempData["Success"] = "Không tìm thấy tài khoản cần sửa.";
                return RedirectToAction(nameof(Index));
            }

            var studentCode = model.StudentCode.Trim();

            var existedUser = await _context.Users
                .AnyAsync(u => u.StudentCode == studentCode && u.UserId != id);

            if (existedUser)
            {
                ModelState.AddModelError("StudentCode", "Mã tài khoản đã tồn tại.");
                await LoadRolesAsync(model.RoleId);
                return View("~/Views/Admin/Users/Edit.cshtml", model);
            }

            user.StudentCode = studentCode;
            user.FullName = model.FullName.Trim();
            user.Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim();
            user.RoleId = model.RoleId;
            user.ClassName = string.IsNullOrWhiteSpace(model.ClassName) ? null : model.ClassName.Trim();
            user.DepartmentName = string.IsNullOrWhiteSpace(model.DepartmentName) ? null : model.DepartmentName.Trim();
            if (!string.IsNullOrWhiteSpace(model.Password))
            {
                user.PasswordHash = model.Password.Trim();
            }

            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã cập nhật tài khoản {user.StudentCode} thành công.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                user.PasswordHash = "123456";
                _context.Update(user);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Đã khôi phục mật khẩu của {user.FullName} về mặc định (123456).";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Lock(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                if (user.StudentCode?.ToLower() == "admin")
                {
                    TempData["Success"] = "Không thể khóa tài khoản admin gốc.";
                    return RedirectToAction(nameof(Index));
                }

                user.IsActive = false;
                _context.Update(user);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Đã khóa tài khoản {user.StudentCode}.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unlock(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                user.IsActive = true;
                _context.Update(user);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Đã mở khóa tài khoản {user.StudentCode}.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                if (user.StudentCode?.ToLower() == "admin")
                {
                    TempData["Success"] = "Không thể xóa tài khoản admin gốc.";
                    return RedirectToAction(nameof(Index));
                }

                _context.Users.Remove(user);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Đã xóa tài khoản {user.StudentCode} khỏi hệ thống.";
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task LoadRolesAsync(int? selectedRoleId = null)
        {
            var roles = await _context.Roles
                .OrderBy(r => r.RoleName)
                .ToListAsync();

            ViewBag.RoleId = new SelectList(roles, "RoleId", "RoleName", selectedRoleId);
        }
    }
}