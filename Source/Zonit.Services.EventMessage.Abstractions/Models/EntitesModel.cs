namespace Zonit.Services.EventMessage;

public class EntitesModel
{
    /// <summary>
    /// ID organizacji
    /// </summary>
    public Guid? OrganizationId { get; init; }

    /// <summary>
    /// ID projektu
    /// </summary>
    public Guid? ProjectId { get; init; }

    /// <summary>
    /// ID użytkownika
    /// </summary>
    public Guid? UserId { get; init; }
}
