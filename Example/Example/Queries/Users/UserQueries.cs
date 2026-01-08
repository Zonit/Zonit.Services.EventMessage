namespace Example.Queries.Users;

using Zonit.Messaging.Commands;

// ===== GET USER BY ID =====

/// <summary>
/// Query pobieraj¹cy u¿ytkownika po ID.
/// </summary>
public record GetUserByIdQuery(Guid UserId) : IRequest<UserDto?>;

/// <summary>
/// DTO u¿ytkownika.
/// </summary>
public record UserDto(
    Guid Id,
    string UserName,
    string Email,
    DateTime CreatedAt,
    bool IsActive
);

/// <summary>
/// Handler dla GetUserByIdQuery.
/// </summary>
public class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, UserDto?>
{
    public async Task<UserDto?> HandleAsync(GetUserByIdQuery query, CancellationToken cancellationToken = default)
    {
        await Task.Delay(20, cancellationToken);

        // Symulacja - zwróæ null dla pustego Guid
        if (query.UserId == Guid.Empty)
        {
            Console.WriteLine($"[GetUserById] User not found: {query.UserId}");
            return null;
        }

        Console.WriteLine($"[GetUserById] Found user: {query.UserId}");

        return new UserDto(
            Id: query.UserId,
            UserName: "JohnDoe",
            Email: "john.doe@example.com",
            CreatedAt: DateTime.UtcNow.AddDays(-30),
            IsActive: true
        );
    }
}

// ===== GET USERS LIST =====

/// <summary>
/// Query pobieraj¹cy listê u¿ytkowników z paginacj¹.
/// </summary>
public record GetUsersListQuery(
    int Page,
    int PageSize,
    string? SearchTerm
) : IRequest<UsersListResponse>;

/// <summary>
/// Response z list¹ u¿ytkowników.
/// </summary>
public record UsersListResponse(
    List<UserDto> Users,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);

/// <summary>
/// Handler dla GetUsersListQuery.
/// </summary>
public class GetUsersListQueryHandler : IRequestHandler<GetUsersListQuery, UsersListResponse>
{
    public async Task<UsersListResponse> HandleAsync(GetUsersListQuery query, CancellationToken cancellationToken = default)
    {
        await Task.Delay(50, cancellationToken);

        // Symulacja danych
        var allUsers = Enumerable.Range(1, 100)
            .Select(i => new UserDto(
                Id: Guid.NewGuid(),
                UserName: $"User{i}",
                Email: $"user{i}@example.com",
                CreatedAt: DateTime.UtcNow.AddDays(-i),
                IsActive: i % 5 != 0
            ))
            .ToList();

        // Filtrowanie
        var filtered = string.IsNullOrEmpty(query.SearchTerm)
            ? allUsers
            : allUsers.Where(u => u.UserName.Contains(query.SearchTerm, StringComparison.OrdinalIgnoreCase)).ToList();

        // Paginacja
        var paged = filtered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        var totalPages = (int)Math.Ceiling(filtered.Count / (double)query.PageSize);

        Console.WriteLine($"[GetUsersList] Found {filtered.Count} users, returning page {query.Page}/{totalPages}");

        return new UsersListResponse(
            Users: paged,
            TotalCount: filtered.Count,
            Page: query.Page,
            PageSize: query.PageSize,
            TotalPages: totalPages
        );
    }
}

// ===== CHECK USER EXISTS =====

/// <summary>
/// Query sprawdzaj¹cy czy u¿ytkownik o danym emailu istnieje.
/// </summary>
public record CheckUserExistsQuery(string Email) : IRequest<bool>;

/// <summary>
/// Handler dla CheckUserExistsQuery.
/// </summary>
public class CheckUserExistsQueryHandler : IRequestHandler<CheckUserExistsQuery, bool>
{
    public async Task<bool> HandleAsync(CheckUserExistsQuery query, CancellationToken cancellationToken = default)
    {
        await Task.Delay(10, cancellationToken);

        // Symulacja - emaile z "existing" w nazwie istniej¹
        var exists = query.Email.Contains("existing", StringComparison.OrdinalIgnoreCase);

        Console.WriteLine($"[CheckUserExists] Email '{query.Email}' exists: {exists}");

        return exists;
    }
}
