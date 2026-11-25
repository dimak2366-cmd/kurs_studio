namespace Server.Security;
public static class Roles
{
    public const string Admin = "Администратор";
    public const string Director = "Директор";
    public const string ChiefEconomist = "Главный экономист";
    public const string Head = "Руководитель отдела";
    public const string Accountant = "Бухгалтер";
    public static readonly string[] All = { Admin, Director, ChiefEconomist, Head, Accountant };
}
