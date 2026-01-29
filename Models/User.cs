namespace WorkloadProductivity.Models
{
    public class User
    {
        public Guid Id { get; set; }
        public string Name { get; set; }

        public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
    }
}
