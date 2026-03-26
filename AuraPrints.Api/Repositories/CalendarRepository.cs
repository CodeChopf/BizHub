using AuraPrintsApi.Data;
using AuraPrintsApi.Models;

namespace AuraPrintsApi.Repositories;

public class CalendarRepository : ICalendarRepository
{
    private readonly DatabaseContext _context;

    public CalendarRepository(DatabaseContext context)
    {
        _context = context;
    }

    public List<CalendarEvent> GetAll()
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT id, title, date, end_date, time, description, color, type, created_at FROM calendar_events ORDER BY date ASC, time ASC";
        var items = new List<CalendarEvent>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            items.Add(new CalendarEvent
            {
                Id          = reader.GetInt32(0),
                Title       = reader.GetString(1),
                Date        = reader.GetString(2),
                EndDate     = reader.IsDBNull(3) ? null : reader.GetString(3),
                Time        = reader.IsDBNull(4) ? null : reader.GetString(4),
                Description = reader.IsDBNull(5) ? null : reader.GetString(5),
                Color       = reader.GetString(6),
                Type        = reader.GetString(7),
                CreatedAt   = reader.GetString(8),
            });
        }
        return items;
    }

    public CalendarEvent Add(string title, string date, string? endDate, string? time, string? description, string color, string type)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        cmd.CommandText = @"
            INSERT INTO calendar_events (title, date, end_date, time, description, color, type, created_at)
            VALUES (@title, @date, @end, @time, @desc, @color, @type, @now);
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@title", title);
        cmd.Parameters.AddWithValue("@date",  date);
        cmd.Parameters.AddWithValue("@end",   (object?)endDate ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@time",  (object?)time ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@desc",  (object?)description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@color", color);
        cmd.Parameters.AddWithValue("@type",  type);
        cmd.Parameters.AddWithValue("@now",   now);
        var id = (long)(cmd.ExecuteScalar() ?? 0L);
        return GetAll().First(e => e.Id == (int)id);
    }

    public void Update(int id, string title, string date, string? endDate, string? time, string? description, string color, string type)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
            UPDATE calendar_events
            SET title=@title, date=@date, end_date=@end, time=@time,
                description=@desc, color=@color, type=@type
            WHERE id=@id";
        cmd.Parameters.AddWithValue("@title", title);
        cmd.Parameters.AddWithValue("@date",  date);
        cmd.Parameters.AddWithValue("@end",   (object?)endDate ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@time",  (object?)time ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@desc",  (object?)description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@color", color);
        cmd.Parameters.AddWithValue("@type",  type);
        cmd.Parameters.AddWithValue("@id",    id);
        cmd.ExecuteNonQuery();
    }

    public void Delete(int id)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "DELETE FROM calendar_events WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }
}
