using SobeSobe.Core.Enums;

namespace SobeSobe.Api.DTOs;

public class SelectTrumpResponse
{
    public Guid RoundId { get; set; }
    public TrumpSuit TrumpSuit { get; set; }
    public bool TrumpSelectedBeforeDealing { get; set; }
    public int TrickValue { get; set; }
}
