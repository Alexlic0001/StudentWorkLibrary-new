namespace StudentWorkLibrary.Models
{
    public class WorkType
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty; // "Дипломная работа"
        public string? Description { get; set; }
    }
}