namespace StudentWorkLibrary.Models
{
    public class Specialty
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty; // Например: "09.03.01"
        public int InstitutionId { get; set; }
        public Institution? Institution { get; set; }
    }
}