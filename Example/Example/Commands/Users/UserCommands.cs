namespace Example.Commands.Users;

using Zonit.Messaging.Commands;

// ===== CREATE USER =====

/// <summary>
/// Command tworz¹cy nowego u¿ytkownika.
/// Zwraca Guid - ID utworzonego u¿ytkownika.
/// </summary>
public record CreateUserCommand(
    string UserName,
    string Email,
    string Password
) : IRequest<Guid>;

/// <summary>
/// Handler dla CreateUserCommand.
/// </summary>
public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, Guid>
{
    public async Task<Guid> HandleAsync(CreateUserCommand command, CancellationToken cancellationToken = default)
    {
        // Symulacja tworzenia u¿ytkownika w bazie
        await Task.Delay(50, cancellationToken);

        var userId = Guid.NewGuid();

        Console.WriteLine($"[CreateUser] Created user '{command.UserName}' with ID: {userId}");

        return userId;
    }
}

// ===== UPDATE USER =====

/// <summary>
/// Wynik operacji aktualizacji.
/// </summary>
public enum UpdateUserResult
{
    Success,
    NotFound,
    ValidationError,
    EmailAlreadyExists
}

/// <summary>
/// Command aktualizuj¹cy dane u¿ytkownika.
/// Zwraca UpdateUserResult - szczegó³owy status operacji.
/// </summary>
public record UpdateUserCommand(
    Guid UserId,
    string? NewUserName,
    string? NewEmail
) : IRequest<UpdateUserResult>;

/// <summary>
/// Handler dla UpdateUserCommand.
/// </summary>
public class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, UpdateUserResult>
{
    public async Task<UpdateUserResult> HandleAsync(UpdateUserCommand command, CancellationToken cancellationToken = default)
    {
        await Task.Delay(30, cancellationToken);

        // Symulacja walidacji
        if (command.UserId == Guid.Empty)
            return UpdateUserResult.NotFound;

        if (command.NewEmail?.Contains("@") == false)
            return UpdateUserResult.ValidationError;

        Console.WriteLine($"[UpdateUser] Updated user {command.UserId}");

        return UpdateUserResult.Success;
    }
}

// ===== DELETE USER =====

/// <summary>
/// Command usuwaj¹cy u¿ytkownika.
/// Zwraca bool - true jeœli usuniêto, false jeœli nie znaleziono.
/// </summary>
public record DeleteUserCommand(Guid UserId) : IRequest<bool>;

/// <summary>
/// Handler dla DeleteUserCommand.
/// </summary>
public class DeleteUserCommandHandler : IRequestHandler<DeleteUserCommand, bool>
{
    public async Task<bool> HandleAsync(DeleteUserCommand command, CancellationToken cancellationToken = default)
    {
        await Task.Delay(20, cancellationToken);

        if (command.UserId == Guid.Empty)
        {
            Console.WriteLine($"[DeleteUser] User not found: {command.UserId}");
            return false;
        }

        Console.WriteLine($"[DeleteUser] Deleted user: {command.UserId}");
        return true;
    }
}

// ===== CHANGE PASSWORD =====

/// <summary>
/// Wynik zmiany has³a.
/// </summary>
public enum ChangePasswordResult
{
    Success,
    UserNotFound,
    InvalidCurrentPassword,
    WeakNewPassword
}

/// <summary>
/// Command zmieniaj¹cy has³o u¿ytkownika.
/// </summary>
public record ChangePasswordCommand(
    Guid UserId,
    string CurrentPassword,
    string NewPassword
) : IRequest<ChangePasswordResult>;

/// <summary>
/// Handler dla ChangePasswordCommand.
/// </summary>
public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, ChangePasswordResult>
{
    public async Task<ChangePasswordResult> HandleAsync(ChangePasswordCommand command, CancellationToken cancellationToken = default)
    {
        await Task.Delay(40, cancellationToken);

        if (command.UserId == Guid.Empty)
            return ChangePasswordResult.UserNotFound;

        if (command.NewPassword.Length < 8)
            return ChangePasswordResult.WeakNewPassword;

        Console.WriteLine($"[ChangePassword] Password changed for user: {command.UserId}");
        return ChangePasswordResult.Success;
    }
}
