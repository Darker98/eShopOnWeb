namespace Microsoft.eShopWeb.Infrastructure.Services.Maxio;

// Every DTO below mirrors a named schema in Specification/components/schemas/*.yaml one-for-one.
// Field names are the wire snake_case names; MaxioJson.Options converts PascalCase<->snake_case,
// so the property names here are intentionally literal transliterations, not renamed for style.

internal sealed class CustomerAttributesForCreateDto
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? Reference { get; set; }
}

internal sealed class CreateCustomerRequestDto
{
    public CustomerAttributesForCreateDto Customer { get; set; } = new();
}

internal sealed class CustomerDto
{
    public int Id { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? Reference { get; set; }
}

internal sealed class CustomerResponseDto
{
    public CustomerDto Customer { get; set; } = new();
}

internal sealed class ProductDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Handle { get; set; } = string.Empty;
    public long PriceInCents { get; set; }
    public int Interval { get; set; }
    public string IntervalUnit { get; set; } = string.Empty;
    public bool RequireCreditCard { get; set; }
}

internal sealed class ProductResponseDto
{
    public ProductDto Product { get; set; } = new();
}

internal sealed class ComponentDto
{
    public int Id { get; set; }
    public string? Handle { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
}

internal sealed class ComponentResponseDto
{
    public ComponentDto Component { get; set; } = new();
}

internal sealed class SubscriptionComponentDto
{
    public int UnitBalance { get; set; }
    public string Kind { get; set; } = string.Empty;
}

internal sealed class SubscriptionComponentResponseDto
{
    public SubscriptionComponentDto Component { get; set; } = new();
}

internal sealed class SubscriptionDto
{
    public int Id { get; set; }
    public string State { get; set; } = string.Empty;
    public long BalanceInCents { get; set; }
    public System.DateTimeOffset? CurrentPeriodEndsAt { get; set; }
    public System.DateTimeOffset? NextAssessmentAt { get; set; }
    public bool? CancelAtEndOfPeriod { get; set; }
    public System.DateTimeOffset? DelayedCancelAt { get; set; }
    public System.DateTimeOffset? CanceledAt { get; set; }
    public CustomerDto? Customer { get; set; }
    public ProductDto? Product { get; set; }
}

internal sealed class SubscriptionResponseDto
{
    public SubscriptionDto Subscription { get; set; } = new();
}

internal sealed class PaymentProfileAttributesForCreateDto
{
    public string ChargifyToken { get; set; } = string.Empty;
}

internal sealed class CreateSubscriptionAttributesDto
{
    public string ProductHandle { get; set; } = string.Empty;
    public int? CustomerId { get; set; }
    public PaymentProfileAttributesForCreateDto? PaymentProfileAttributes { get; set; }

    /// <summary>"remittance" defers balance collection instead of demanding a card on file - see Collection-Method.yaml
    /// and the "Basic" createSubscription example in openapi.yaml, used for signups without payment capture.</summary>
    public string? PaymentCollectionMethod { get; set; }
}

internal sealed class CreateSubscriptionRequestDto
{
    public CreateSubscriptionAttributesDto Subscription { get; set; } = new();
}

internal sealed class UpdateSubscriptionAttributesDto
{
    public string? ProductHandle { get; set; }
    public bool? ProductChangeDelayed { get; set; }
}

internal sealed class UpdateSubscriptionRequestDto
{
    public UpdateSubscriptionAttributesDto Subscription { get; set; } = new();
}

internal sealed class MigrationAttributesDto
{
    public string ProductHandle { get; set; } = string.Empty;
    public bool PreservePeriod { get; set; }
}

internal sealed class MigrationRequestDto
{
    public MigrationAttributesDto Migration { get; set; } = new();
}

internal sealed class MigrationPreviewDto
{
    public int ProratedAdjustmentInCents { get; set; }
    public int ChargeInCents { get; set; }
    public int PaymentDueInCents { get; set; }
    public int CreditAppliedInCents { get; set; }
}

internal sealed class MigrationPreviewResponseDto
{
    public MigrationPreviewDto Migration { get; set; } = new();
}

/// <summary>Body for hold.json - an empty object is a plain pause with no scheduled auto-resume.</summary>
internal sealed class PauseRequestDto
{
    public object Hold { get; set; } = new();
}

internal sealed class CancellationAttributesDto
{
    public string? CancellationMessage { get; set; }
}

internal sealed class CancellationRequestDto
{
    public CancellationAttributesDto Subscription { get; set; } = new();
}

internal sealed class CreateUsageAttributesDto
{
    public decimal Quantity { get; set; }
    public string? Memo { get; set; }
}

internal sealed class CreateUsageRequestDto
{
    public CreateUsageAttributesDto Usage { get; set; } = new();
}

internal sealed class UsageDto
{
    // Usage.yaml explicitly declares id as format: int64, unlike every other resource's plain
    // "integer" (int32) id - confirmed empirically too: a real sandbox usage id overflowed Int32.
    public long Id { get; set; }
    public string? Memo { get; set; }

    // Usage.yaml declares quantity as [integer, string] - MaxioJson.Options.NumberHandling
    // (AllowReadingFromString) lets this decimal property accept either wire shape.
    public decimal Quantity { get; set; }
}

internal sealed class UsageResponseDto
{
    public UsageDto Usage { get; set; } = new();
}
