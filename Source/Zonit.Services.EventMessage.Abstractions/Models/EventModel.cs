namespace Zonit.Services.EventMessage;

public class EventModel
{
    public required string EventName { get; init; }
    public required PayloadModel Payload { get; init; }
}

//public class EntitiesModel
//{
//    public Guid Id { get; init; }
//    public Guid? UserId { get; init; }
//    public Guid? OrganizationId { get; init; }
//    public Guid? ProjectId { get; init; }
//}
