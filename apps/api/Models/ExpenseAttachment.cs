namespace AuraPrintsApi.Models;

public class ExpenseAttachment
{
    public int Id { get; set; }
    public int ExpenseId { get; set; }
    public string FileName { get; set; } = "";
    public string MimeType { get; set; } = "";
    public string Data { get; set; } = ""; // Base64
    public string CreatedAt { get; set; } = "";
}