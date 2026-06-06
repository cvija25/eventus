using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Identity.API.Entities;

public class User
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    [BsonElement("email")]
    public required string Email { get; set; }

    [BsonElement("passwordHash")]
    public required string PasswordHash { get; set; }

    [BsonElement("role")]
    public required string Role { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }
}
