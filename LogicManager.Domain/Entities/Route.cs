namespace LogicManager.Domain.Entities;

public class Route
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public List<Station>? Stations { get; set; }
}
