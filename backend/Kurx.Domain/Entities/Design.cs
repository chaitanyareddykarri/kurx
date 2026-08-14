using Kurx.Domain.Enums;

namespace Kurx.Domain.Entities;

public class DesignTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? OrgId { get; set; }                    // null = system/platform template
    public Guid? EventId { get; set; }                  // null = org-wide; set = event-specific override (D2)
    public TemplateKind Kind { get; set; }              // Certificate | Invite
    public TemplateMode Mode { get; set; }              // System | Custom
    public string Name { get; set; } = null!;
    public string? BaseLayout { get; set; }             // code key for system designs
    public string? FileKey { get; set; }                // uploaded PDF/PNG for custom
    public string PlacementsJson { get; set; } = "[]";  // [{field,x,y,w,h,align,size,enabled}] — percents
    public string? AccentColor { get; set; }
    public string? SignatoryJson { get; set; }          // {name,title,signature_key}
    public string[]? SuggestedCategorySlugs { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class GeneratedCard
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public GeneratedCardKind Kind { get; set; }
    public Guid? GroupId { get; set; }
    public Guid TemplateId { get; set; }
    public string ImageKey { get; set; } = null!;       // 1080×1350
    public string? SquareImageKey { get; set; }         // 1080×1080
    public string? PdfKey { get; set; }                 // A4
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Certificate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public Guid TicketId { get; set; }
    public Guid UserId { get; set; }
    public Guid? TemplateId { get; set; }
    public string VerifyCode { get; set; } = null!;     // unique 10-char base32
    public string? PdfKey { get; set; }
    public CertificateKind Kind { get; set; } = CertificateKind.Participation;
    public CertificateStatus Status { get; set; } = CertificateStatus.Generated;
    public bool IsPublic { get; set; } = true;
    public bool IsRevoked { get; set; }                 // visible on verify, never hidden (D-036 — contrast D-018)
    public string? RevokedReason { get; set; }
    public DateTime? RevokedAt { get; set; }
    public DateTime? EmailedAt { get; set; }
    public int EmailRetryCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class EmailLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ToEmail { get; set; } = null!;
    public string Template { get; set; } = null!;
    public string Subject { get; set; } = null!;
    public EmailStatus Status { get; set; } = EmailStatus.Queued;
    public string? ProviderMessageId { get; set; }
    public string? Error { get; set; }
    public string RelatedType { get; set; } = "";
    public Guid? RelatedId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
