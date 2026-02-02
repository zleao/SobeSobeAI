namespace SobeSobe.Core.ValueObjects;

public record Card
{
    public required string Suit { get; init; }
    public required string Rank { get; init; }

    public static readonly string[] ValidSuits = ["Hearts", "Diamonds", "Clubs", "Spades"];
    public static readonly string[] ValidRanks = ["Ace", "7", "King", "Queen", "Jack", "6", "5", "4", "3", "2"];

    public bool IsValid()
    {
        return ValidSuits.Contains(Suit) && ValidRanks.Contains(Rank);
    }

    public int GetRankValue()
    {
        return Rank switch
        {
            "Ace" => 10,
            "7" => 9,
            "King" => 8,
            "Queen" => 7,
            "Jack" => 6,
            "6" => 5,
            "5" => 4,
            "4" => 3,
            "3" => 2,
            "2" => 1,
            _ => 0
        };
    }

    public override string ToString() => $"{Rank} of {Suit}";
}
