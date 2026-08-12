using System.ComponentModel.DataAnnotations;

namespace TaskManagerAPI.Models
{
    public enum TaskStatusFilter
    {
        All,
        Pending,
        Completed
    }

    public enum TaskSortBy
    {
        CreatedAt,
        DueDate,
        Priority,
        Title
    }

    public enum SortDirection
    {
        Asc,
        Desc
    }

    public sealed class TaskQuery
    {
        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;

        [Range(1, 100)]
        public int PageSize { get; set; } = 10;

        public TaskStatusFilter Status { get; set; } = TaskStatusFilter.All;

        [MaxLength(100)]
        public string? Title { get; set; }

        public TaskSortBy SortBy { get; set; } = TaskSortBy.CreatedAt;

        public SortDirection SortDirection { get; set; } = SortDirection.Desc;
    }

    public sealed record PagedResponse<T>(
        IReadOnlyList<T> Items,
        int Page,
        int PageSize,
        int TotalItems,
        int TotalPages);
}
