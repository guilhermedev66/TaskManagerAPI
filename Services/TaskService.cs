using Microsoft.EntityFrameworkCore;
using TaskManagerAPI.Data;
using TaskManagerAPI.Models;

namespace TaskManagerAPI.Services
{
    public class TaskService
    {
        private readonly AppDbContext _context;

        public TaskService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<PagedResponse<TaskItem>> GetAllAsync(
            int userId,
            TaskQuery query,
            CancellationToken cancellationToken)
        {
            var tasks = _context.Tasks
                .AsNoTracking()
                .Where(t => t.UserId == userId);

            if (!string.IsNullOrWhiteSpace(query.Title))
            {
                var title = query.Title.Trim();
                tasks = tasks.Where(t => EF.Functions.Like(t.Title, $"%{title}%"));
            }

            tasks = query.Status switch
            {
                TaskStatusFilter.Pending => tasks.Where(t => !t.IsCompleted),
                TaskStatusFilter.Completed => tasks.Where(t => t.IsCompleted),
                _ => tasks
            };

            var totalItems = await tasks.CountAsync(cancellationToken);
            var orderedTasks = ApplyOrdering(tasks, query.SortBy, query.SortDirection);
            var items = await orderedTasks
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync(cancellationToken);
            var totalPages = (int)Math.Ceiling(totalItems / (double)query.PageSize);

            return new PagedResponse<TaskItem>(items, query.Page, query.PageSize, totalItems, totalPages);
        }

        private static IOrderedQueryable<TaskItem> ApplyOrdering(
            IQueryable<TaskItem> tasks,
            TaskSortBy sortBy,
            SortDirection direction)
        {
            return (sortBy, direction) switch
            {
                (TaskSortBy.DueDate, SortDirection.Asc) => tasks.OrderBy(t => t.DueDate).ThenBy(t => t.Id),
                (TaskSortBy.DueDate, SortDirection.Desc) => tasks.OrderByDescending(t => t.DueDate).ThenByDescending(t => t.Id),
                (TaskSortBy.Priority, SortDirection.Asc) => tasks.OrderBy(t => t.Priority).ThenBy(t => t.Id),
                (TaskSortBy.Priority, SortDirection.Desc) => tasks.OrderByDescending(t => t.Priority).ThenByDescending(t => t.Id),
                (TaskSortBy.Title, SortDirection.Asc) => tasks.OrderBy(t => t.Title).ThenBy(t => t.Id),
                (TaskSortBy.Title, SortDirection.Desc) => tasks.OrderByDescending(t => t.Title).ThenByDescending(t => t.Id),
                (TaskSortBy.CreatedAt, SortDirection.Asc) => tasks.OrderBy(t => t.CreatedAt).ThenBy(t => t.Id),
                _ => tasks.OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id)
            };
        }

        public async Task<List<TaskItem>> GetCompletedAsync(int userId, CancellationToken cancellationToken)
        {
            return await _context.Tasks
                .AsNoTracking()
                .Where(t => t.UserId == userId && t.IsCompleted)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<TaskItem>> GetPendingAsync(int userId, CancellationToken cancellationToken)
        {
            return await _context.Tasks
                .AsNoTracking()
                .Where(t => t.UserId == userId && !t.IsCompleted)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<TaskItem>> SearchByTitleAsync(int userId, string title, CancellationToken cancellationToken)
        {
            var normalizedTitle = title.Trim().ToLowerInvariant();
            return await _context.Tasks
                .AsNoTracking()
                .Where(t => t.UserId == userId && EF.Functions.Like(t.Title, $"%{normalizedTitle}%"))
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<TaskItem?> GetByIdAsync(int userId, int id, CancellationToken cancellationToken)
        {
            return await _context.Tasks
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId, cancellationToken);
        }

        public async Task<TaskItem> CreateAsync(int userId, CreateTaskRequest request, CancellationToken cancellationToken)
        {
            var task = new TaskItem
            {
                Title = request.Title.Trim(),
                Description = request.Description?.Trim(),
                Priority = request.Priority,
                DueDate = request.DueDate,
                CreatedAt = DateTime.UtcNow,
                IsCompleted = false,
                UserId = userId
            };

            _context.Tasks.Add(task);
            await _context.SaveChangesAsync(cancellationToken);
            return task;
        }

        public async Task<bool> UpdateAsync(int userId, int id, UpdateTaskRequest request, CancellationToken cancellationToken)
        {
            var existingTask = await _context.Tasks
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId, cancellationToken);
            if (existingTask is null)
            {
                return false;
            }

            existingTask.Title = request.Title.Trim();
            existingTask.Description = request.Description?.Trim();
            existingTask.Priority = request.Priority;
            existingTask.DueDate = request.DueDate;
            existingTask.IsCompleted = request.IsCompleted;
            await _context.SaveChangesAsync(cancellationToken);

            return true;
        }

        public async Task<bool> DeleteAsync(int userId, int id, CancellationToken cancellationToken)
        {
            var task = await _context.Tasks
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId, cancellationToken);
            if (task is null)
            {
                return false;
            }

            _context.Tasks.Remove(task);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}
