using AuraPrintsApi.Models;

namespace AuraPrintsApi.Repositories;

public interface IAdminRepository
{
    // Wochen
    Week CreateWeek(CreateWeekRequest req);
    Week UpdateWeek(int number, UpdateWeekRequest req);
    void DeleteWeek(int number);

    // Tasks
    AppTask CreateTask(CreateTaskRequest req);
    AppTask UpdateTask(int id, UpdateTaskRequest req);
    void DeleteTask(int id);
    void ReorderTasks(int weekNumber, ReorderTasksRequest req);
}