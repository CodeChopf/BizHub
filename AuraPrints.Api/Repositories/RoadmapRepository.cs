using AuraPrintsApi.Data;
using AuraPrintsApi.Models;

namespace AuraPrintsApi.Repositories;

public class RoadmapRepository : IRoadmapRepository
{
    private readonly DatabaseContext _context;

    public RoadmapRepository(DatabaseContext context)
    {
        _context = context;
    }

    public AppData GetAll()
    {
        using var con = _context.CreateConnection();
        con.Open();

        var weeks = new List<Week>();

        using var wCmd = con.CreateCommand();
        wCmd.CommandText = "SELECT number, title, phase, badge_pc, badge_phys, note FROM weeks ORDER BY number";
        using var wReader = wCmd.ExecuteReader();
        while (wReader.Read())
        {
            weeks.Add(new Week
            {
                Number = wReader.GetInt32(0),
                Title = wReader.GetString(1),
                Phase = wReader.GetString(2),
                BadgePc = wReader.GetString(3),
                BadgePhys = wReader.GetString(4),
                Note = wReader.IsDBNull(5) ? null : wReader.GetString(5)
            });
        }

        using var tCmd = con.CreateCommand();
        tCmd.CommandText = "SELECT id, week_number, type, text, hours FROM tasks ORDER BY week_number, sort_order";
        using var tReader = tCmd.ExecuteReader();
        while (tReader.Read())
        {
            var weekNum = tReader.GetInt32(1);
            var week = weeks.First(w => w.Number == weekNum);
            week.Tasks.Add(new AppTask
            {
                Id = tReader.GetInt32(0),
                Type = tReader.GetString(2),
                Text = tReader.GetString(3),
                Hours = tReader.GetString(4)
            });
        }

        return new AppData { Weeks = weeks };
    }
}