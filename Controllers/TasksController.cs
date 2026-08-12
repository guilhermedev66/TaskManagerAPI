using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManagerAPI.Models;
using TaskManagerAPI.Services;

namespace TaskManagerAPI.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class TasksController : ControllerBase
    {
        private readonly TaskService _taskService;

        public TasksController(TaskService taskService)
        {
            _taskService = taskService;
        }

        private bool TryGetCurrentUserId(out int userId)
        {
            var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(claim, out userId);
        }

        [HttpGet]
        public async Task<ActionResult<PagedResponse<TaskItem>>> GetTasks(
            [FromQuery] TaskQuery query,
            CancellationToken cancellationToken)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            var tasks = await _taskService.GetAllAsync(userId, query, cancellationToken);
            return Ok(tasks);
        }

        [HttpGet("completed")]
        public async Task<ActionResult<IEnumerable<TaskItem>>> GetCompletedTasks(CancellationToken cancellationToken)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            var tasks = await _taskService.GetCompletedAsync(userId, cancellationToken);
            return Ok(tasks);
        }

        [HttpGet("pending")]
        public async Task<ActionResult<IEnumerable<TaskItem>>> GetPendingTasks(CancellationToken cancellationToken)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            var tasks = await _taskService.GetPendingAsync(userId, cancellationToken);
            return Ok(tasks);
        }

        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<TaskItem>>> SearchTasksByTitle([FromQuery] string title, CancellationToken cancellationToken)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                return Problem(
                    detail: "O parâmetro 'title' é obrigatório.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var tasks = await _taskService.SearchByTitleAsync(userId, title, cancellationToken);
            return Ok(tasks);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<TaskItem>> GetTaskById(int id, CancellationToken cancellationToken)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            var task = await _taskService.GetByIdAsync(userId, id, cancellationToken);
            if (task is null)
            {
                return NotFound();
            }

            return Ok(task);
        }

        [HttpPost]
        public async Task<ActionResult<TaskItem>> CreateTask([FromBody] CreateTaskRequest request, CancellationToken cancellationToken)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            if (request.DueDate.HasValue && request.DueDate.Value < DateTime.UtcNow)
            {
                return Problem(
                    detail: "DueDate não pode estar no passado.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var task = await _taskService.CreateAsync(userId, request, cancellationToken);
            return CreatedAtAction(nameof(GetTaskById), new { id = task.Id }, task);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateTask(int id, [FromBody] UpdateTaskRequest request, CancellationToken cancellationToken)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            if (request.DueDate.HasValue && request.DueDate.Value < DateTime.UtcNow && !request.IsCompleted)
            {
                return Problem(
                    detail: "DueDate não pode estar no passado para tarefas pendentes.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var updated = await _taskService.UpdateAsync(userId, id, request, cancellationToken);
            if (!updated)
            {
                return NotFound();
            }

            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteTask(int id, CancellationToken cancellationToken)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            var deleted = await _taskService.DeleteAsync(userId, id, cancellationToken);
            if (!deleted)
            {
                return NotFound();
            }

            return NoContent();
        }
    }
}
