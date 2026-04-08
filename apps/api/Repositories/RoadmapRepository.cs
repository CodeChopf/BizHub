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

        var taskMap = new Dictionary<int, AppTask>();

        if (weeks.Count > 0)
        {
            using var tCmd = con.CreateCommand();
            tCmd.CommandText = @"
                SELECT t.id, t.week_number, t.type, t.text, t.hours
                FROM tasks t
                WHERE t.project_id = @pid
                ORDER BY t.week_number, t.sort_order";
            tCmd.Parameters.AddWithValue("@pid", projectId);
            using var tReader = tCmd.ExecuteReader();
            while (tReader.Read())
            {
                var weekNum = tReader.GetInt32(1);
                var week = weeks.FirstOrDefault(w => w.Number == weekNum);
                if (week == null) continue;
                var task = new AppTask
                {
                    Id    = tReader.GetInt32(0),
                    Type  = tReader.GetString(2),
                    Text  = tReader.GetString(3),
                    Hours = tReader.GetString(4)
                };
                week.Tasks.Add(task);
                taskMap[task.Id] = task;
            }

            if (taskMap.Count > 0)
            {
                using var sCmd = con.CreateCommand();
                sCmd.CommandText = @"
                    SELECT id, task_id, text, hours
                    FROM subtasks
                    WHERE project_id = @pid
                    ORDER BY task_id, sort_order";
                sCmd.Parameters.AddWithValue("@pid", projectId);
                using var sReader = sCmd.ExecuteReader();
                while (sReader.Read())
                {
                    var taskId = sReader.GetInt32(1);
                    if (!taskMap.TryGetValue(taskId, out var parentTask)) continue;
                    parentTask.Subtasks.Add(new AppSubtask
                    {
                        Id    = sReader.GetInt32(0),
                        Text  = sReader.GetString(2),
                        Hours = sReader.GetString(3)
                    });
                }

                using var tagCmd = con.CreateCommand();
                tagCmd.CommandText = @"
                    SELECT tta.task_id, tt.id, tt.name, tt.color
                    FROM task_tag_assignments tta
                    JOIN task_tags tt ON tt.id = tta.tag_id
                    WHERE tt.project_id = @pid
                    ORDER BY tta.task_id, tt.sort_order, tt.name";
                tagCmd.Parameters.AddWithValue("@pid", projectId);
                using var tagReader = tagCmd.ExecuteReader();
                while (tagReader.Read())
                {
                    var taskId = tagReader.GetInt32(0);
                    if (!taskMap.TryGetValue(taskId, out var parentTask)) continue;
                    parentTask.Tags.Add(new TaskTag
                    {
                        Id    = tagReader.GetInt32(1),
                        Name  = tagReader.GetString(2),
                        Color = tagReader.GetString(3)
                    });
                }
            }
        }

        return new AppData { Weeks = weeks };
    }
}