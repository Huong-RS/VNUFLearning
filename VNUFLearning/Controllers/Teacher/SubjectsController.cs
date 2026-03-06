using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VNUFLearning.Data;
using VNUFLearning.Models;

namespace VNUFLearning.Controllers.Teacher
{
    public class SubjectsController : Controller
    {
        private readonly VnufLearningContext _context;

        // 1. Hàm khởi tạo: Kết nối với Database
        public SubjectsController(VnufLearningContext context)
        {
            _context = context;
        }

        // 3. Chức năng mở màn hình thêm mới (GET: Subjects/Create)
        public IActionResult Create()
        {
            return View();
        }

        // 4. Chức năng xử lý lưu vào Database (POST: Subjects/Create)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Subject subject)
        {
            if (ModelState.IsValid)
            {
                _context.Add(subject); // Thêm vào bộ nhớ tạm
                await _context.SaveChangesAsync(); // Lưu thật vào SQL Server
                return RedirectToAction(nameof(Index)); // Quay về trang danh sách
            }
            return View(subject);
        }
        // GET: Subjects/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var subject = await _context.Subjects.FindAsync(id);
            if (subject == null) return NotFound();

            return View(subject);
        }

        // POST: Subjects/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Subject subject)
        {
            if (id != subject.SubjectId) return NotFound();

            if (ModelState.IsValid)
            {
                _context.Update(subject);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(subject);
        }
        // GET: Subjects/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var subject = await _context.Subjects
                .FirstOrDefaultAsync(m => m.SubjectId == id);

            if (subject == null) return NotFound();

            return View(subject);
        }

        // POST: Subjects/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var subject = await _context.Subjects.FindAsync(id);
            _context.Subjects.Remove(subject);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        public async Task<IActionResult> Index(string keyword)
        {
            var query = _context.Subjects
                                .Include(s => s.Questions)
                                .AsQueryable();

            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(s => s.SubjectName.Contains(keyword));
            }

            return View(await query.ToListAsync());
        }

    }
}