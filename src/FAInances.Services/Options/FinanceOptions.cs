using System.ComponentModel.DataAnnotations;

namespace FAInances.Services.Options;

public class FinanceOptions
{
    public const string SectionName = "Finance";

    /// <summary>Currency code shown next to amounts, e.g. "EUR".</summary>
    [Required]
    [StringLength(5, MinimumLength = 1)]
    public string Currency { get; set; } = "EUR";
}
