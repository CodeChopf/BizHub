using AuraPrintsApi.Models;

namespace AuraPrintsApi.Repositories;

public interface ICalendarRepository
{
    List<CalendarEvent> GetAll();
    CalendarEvent Add(string title, string date, string? endDate, string? time, string? description, string color, string type);
    void Update(int id, string title, string date, string? endDate, string? time, string? description, string color, string type);
    void Delete(int id);
}
