namespace Shared.Dtos;

/// <summary>Карточка пользователя для администратора.</summary>
public class UserDto
{
    public int Id { get; set; }
    public string Login { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public int? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public bool IsBlocked { get; set; }
    public int FailedAttempts { get; set; }
    public DateTime? LockoutUntil { get; set; }
}

/// <summary>Создание нового пользователя (только Администратор).</summary>
public class CreateUserRequest
{
    public string Login { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int RoleId { get; set; }
    public int? DepartmentId { get; set; }
}

/// <summary>Редактирование ФИО, роли и отдела пользователя.</summary>
public class UpdateUserRequest
{
    public string FullName { get; set; } = string.Empty;
    public int RoleId { get; set; }
    public int? DepartmentId { get; set; }
}

/// <summary>Сброс пароля пользователя администратором.</summary>
public class ResetPasswordRequest
{
    public string NewPassword { get; set; } = string.Empty;
}

/// <summary>Ответ на сброс пароля (для передачи пользователю).</summary>
public class ResetPasswordResponse
{
    public bool Success { get; set; }
    public string NewPassword { get; set; } = string.Empty;
}
