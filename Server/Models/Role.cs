namespace Server.Models;

/// <summary>
/// Роль пользователя системы: Администратор, Директор, Главный экономист,
/// Руководитель отдела, Бухгалтер.
/// </summary>
public class Role
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public ICollection<User> Users { get; set; } = new List<User>();
}
