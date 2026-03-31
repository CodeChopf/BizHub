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

    public AppData GetAll(int projectId)
    {
        using var con = _context.CreateConnection();
        con.Open();

        var weeks = new List<Week>();

        using var wCmd = con.CreateCommand();
        wCmd.CommandText = "SELECT number, title, phase, badge_pc, badge_phys, note FROM weeks WHERE project_id = @pid ORDER BY number";
        wCmd.Parameters.AddWithValue("@pid", projectId);
        using var wReader = wCmd.ExecuteReader();
        while (wReader.Read())
        {
            weeks.Add(new Week
            {
                Number    = wReader.GetInt32(0),
                Title     = wReader.GetString(1),
                Phase     = wReader.GetString(2),
                BadgePc   = wReader.GetString(3),
                BadgePhys = wReader.GetString(4),
                Note      = wReader.IsDBNull(5) ? null : wReader.GetString(5)
            });
        }

        if (weeks.Count > 0)
        {
            using var tCmd = con.CreateCommand();
            tCmd.CommandText = @"
                SELECT t.id, t.week_number, t.type, t.text, t.hours
                FROM tasks t
                JOIN weeks w ON w.number = t.week_number AND w.project_id = @pid
                ORDER BY t.week_number, t.sort_order";
            tCmd.Parameters.AddWithValue("@pid", projectId);
            using var tReader = tCmd.ExecuteReader();
            while (tReader.Read())
            {
                var weekNum = tReader.GetInt32(1);
                var week = weeks.FirstOrDefault(w => w.Number == weekNum);
                if (week == null) continue;
                week.Tasks.Add(new AppTask
                {
                    Id    = tReader.GetInt32(0),
                    Type  = tReader.GetString(2),
                    Text  = tReader.GetString(3),
                    Hours = tReader.GetString(4)
                });
            }
        }

        return new AppData { Weeks = weeks };
    }
}