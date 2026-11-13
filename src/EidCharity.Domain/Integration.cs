namespace EidCharity.Domain.Integration;

public static class MessageTypes
{
    /// <summary>Projects → Collections: approved mapping/eligibility (reference data the collector cannot edit).</summary>
    public const string MappingPublished = "projects.mapping.published";
    /// <summary>Collections → Projects: transaction / receipt / refund / reallocation results.</summary>
    public const string CollectionResult = "collections.result";
}

public enum OutboxState { Pending = 0, Succeeded = 1, Failed = 2 }

public class OutboxMessage
{
    public long Id { get; set; }
    public string Type { get; set; } = "";
    public string Payload { get; set; } = "";
    /// <summary>Unique key; re-sending never duplicates the effect.</summary>
    public string IdempotencyKey { get; set; } = "";
    public string? Reference { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public OutboxState State { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
    public DateTime? NextAttemptAt { get; set; }
}

/// <summary>Consumer-side de-duplication.</summary>
public class InboxMessage
{
    public string IdempotencyKey { get; set; } = "";
    public string Type { get; set; } = "";
    public DateTime ProcessedAt { get; set; }
}
