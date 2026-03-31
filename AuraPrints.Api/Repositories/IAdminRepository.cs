using AuraPrintsApi.Models;

namespace AuraPrintsApi.Repositories;

public interface IAdminRepository
{
    // Wochen
    Week CreateWeek(int projectId, CreateWeekRequest req);
    Week UpdateWeek(int projectId, int number, UpdateWeekRequest req);
    void DeleteWeek(int projectId, int number);

    // Tasks
    AppTask CreateTask(int projectId, CreateTaskRequest req);
    AppTask UpdateTask(int id, UpdateTaskRequest req);
    void DeleteTask(int id);
    void ReorderTasks(int projectId, int weekNumber, ReorderTasksRequest req);
}